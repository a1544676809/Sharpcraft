// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Net;
using System.Text;
using Sharpcraft.Auth;
using Sharpcraft.Launcher;
using Xunit;

namespace Sharpcraft.Launcher.Tests;

/// <summary>
/// Renewal order from docs/modules/auth.md 5.1: a valid session costs no request, an expired one is
/// extended without Xbox Live, and the Microsoft grant is persisted before the Xbox chain so a failed
/// chain never forces another device code.
/// </summary>
public sealed class SessionCacheTests
{
    private static readonly MinecraftProfile Profile = new("069a79f444e94726a5befca90e38aaf5", "Notch", Skins: true);
    private static readonly MinecraftSession Session =
        new("mc-access", DateTimeOffset.UtcNow.AddHours(12));

    [Fact]
    public void AValidSessionSignsInWithoutAnyRequest()
    {
        using var fixture = new Fixture();
        fixture.StoreWithSession(expiresAt: DateTimeOffset.UtcNow.AddHours(12));

        var signedIn = fixture.Run();

        Assert.True(signedIn.Result.FromCache);
        Assert.Equal("mc-access-warm", signedIn.Result.Session.AccessToken);
        Assert.Equal(0, fixture.Requests);
    }

    [Fact]
    public void AnExpiredSessionIsExtendedWithoutXboxLive()
    {
        using var fixture = new Fixture();
        fixture.StoreWithSession(expiresAt: DateTimeOffset.UtcNow.AddMinutes(1));
        fixture.Respond("api.minecraftservices.com/authentication/refresh",
            """{"access_token": "mc-access-refreshed", "expires_in": 86400}""");

        var signedIn = fixture.Run();

        Assert.Equal("mc-access-refreshed", signedIn.Result.Session.AccessToken);
        Assert.DoesNotContain(fixture.RequestedUris, uri => uri.Contains("xsts", StringComparison.Ordinal));
        Assert.Equal(1, fixture.Requests);
    }

    [Fact]
    public void ARejectedSessionFallsBackToTheXboxChain()
    {
        using var fixture = new Fixture();
        fixture.StoreWithSession(expiresAt: DateTimeOffset.UtcNow.AddMinutes(1));
        fixture.RespondStatus("api.minecraftservices.com/authentication/refresh", HttpStatusCode.Unauthorized);
        fixture.Respond("user.auth.xboxlive.com/user/authenticate", """{"Token": "user-token", "DisplayClaims": {"xui": [{"uhs": "user-hash"}]}}""");
        fixture.Respond("xsts.auth.xboxlive.com/xsts/authorize",
            """{"Token": "xsts-token", "DisplayClaims": {"xui": [{"uhs": "uhs"}]}}""");
        fixture.Respond("api.minecraftservices.com/authentication/login_with_xbox",
            """{"access_token": "mc-access-fresh", "expires_in": 86400}""");
        fixture.Respond("api.minecraftservices.com/minecraft/profile",
            """{"id": "069a79f444e94726a5befca90e38aaf5", "name": "Notch", "skins": []}""");

        var signedIn = fixture.Run();

        Assert.Equal("mc-access-fresh", signedIn.Result.Session.AccessToken);
        Assert.Contains(fixture.RequestedUris, uri => uri.Contains("xsts", StringComparison.Ordinal));
    }

    [Fact]
    public void TheMicrosoftGrantIsPersistedBeforeTheXboxChain()
    {
        using var fixture = new Fixture();
        fixture.StoreWithGrant();
        fixture.RespondStatus("xsts.auth.xboxlive.com/xsts/authorize", HttpStatusCode.BadGateway);
        fixture.Respond("user.auth.xboxlive.com/user/authenticate", """{"Token": "user-token", "DisplayClaims": {"xui": [{"uhs": "user-hash"}]}}""");

        Assert.ThrowsAny<Exception>(() => fixture.Run());

        // The grant must still be on disk: without it the user would need another device code.
        var stored = fixture.Store.Load();
        Assert.NotNull(stored);
        Assert.Equal("msa-refresh", stored!.RefreshToken);
    }

    [Fact]
    public void ReusingACachedSessionStillRefreshesTheAccountList()
    {
        using var fixture = new Fixture();
        fixture.StoreWithSession(expiresAt: DateTimeOffset.UtcNow.AddHours(12));

        var recorded = new List<(string Name, string Id)>();
        using var http = new HttpClient(new Fixture.Handler(fixture));
        AuthenticationFlowProbe.SignIn(http, fixture.Store, (name, id) => recorded.Add((name, id)));

        // The offline gate is a pure function of the account list, so a cached sign-in must still
        // refresh it (AGENTS.md principle 7.1).
        Assert.Single(recorded);
        Assert.Equal("Notch", recorded[0].Name);
        Assert.Equal("069a79f444e94726a5befca90e38aaf5", recorded[0].Id);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string _root;
        private readonly Dictionary<string, (HttpStatusCode Status, string Body)> _responses = new(StringComparer.OrdinalIgnoreCase);

        public Fixture()
        {
            _root = Path.Combine(Path.GetTempPath(), "sharpcraft-session-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            Store = new TokenStore(Path.Combine(_root, "tokens.json"));
        }

        public TokenStore Store { get; }

        public int Requests { get; private set; }

        public List<string> RequestedUris { get; } = [];

        public void Respond(string uriFragment, string body) =>
            _responses[uriFragment] = (HttpStatusCode.OK, body);

        public void RespondStatus(string uriFragment, HttpStatusCode status) =>
            _responses[uriFragment] = (status, "{}");

        public void StoreWithSession(DateTimeOffset expiresAt)
        {
            Store.Save(new MicrosoftToken("msa-access", "msa-refresh", DateTimeOffset.UtcNow.AddHours(1)),
                "service::user.auth.xboxlive.com::MBI_SSL", "client-id");
            Store.SaveSession(new MinecraftSession("mc-access-warm", expiresAt), Profile);
        }

        public void StoreWithGrant() =>
            Store.Save(new MicrosoftToken("msa-access", "msa-refresh", DateTimeOffset.UtcNow.AddHours(1)),
                "service::user.auth.xboxlive.com::MBI_SSL", "client-id");

        public (AuthenticationFlow.SignedIn Result, int Requests) Run()
        {
            using var http = new HttpClient(new Handler(this));

            // SignedIn is internal to the launcher assembly; the test asserts through the public surface.
            var result = AuthenticationFlowProbe.SignIn(http, Store);
            return (result, Requests);
        }

        internal sealed class Handler(Fixture fixture) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                fixture.Requests++;
                var uri = request.RequestUri!.ToString();
                fixture.RequestedUris.Add(uri);

                foreach (var (fragment, response) in fixture._responses)
                {
                    if (uri.Contains(fragment, StringComparison.OrdinalIgnoreCase))
                    {
                        return Task.FromResult(new HttpResponseMessage(response.Status)
                        {
                            Content = new StringContent(response.Body, Encoding.UTF8, "application/json")
                        });
                    }
                }

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = new StringContent("{}", Encoding.UTF8, "application/json")
                });
            }
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_root))
                {
                    Directory.Delete(_root, recursive: true);
                }
            }
            catch (IOException)
            {
            }
        }
    }
}

/// <summary>Exposes the internal sign-in entry point to the tests.</summary>
internal static class AuthenticationFlowProbe
{
    public static AuthenticationFlow.SignedIn SignIn(
        HttpClient http,
        TokenStore store,
        Action<string, string>? onSignedIn = null) =>
        AuthenticationFlow.EnsureSignedInAsync(
            http,
            store,
            forceInteractive: false,
            onChallenge: null,
            CancellationToken.None,
            onSignedIn).GetAwaiter().GetResult();
}
