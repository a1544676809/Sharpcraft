// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Net;
using System.Text.Json;
using Xunit;

namespace Sharpcraft.Auth.Tests;

/// <summary>
/// Xbox Live, XSTS and Minecraft services steps (docs/modules/auth.md 3-4).
/// </summary>
public sealed class XboxAndServicesTests
{
    [Fact]
    public async Task XboxUserAuthSendsTheRpsTicketPrefixAndParsesTheHash()
    {
        var stub = new StubHandler().RespondOk("""
            {"Token":"xbl-token","DisplayClaims":{"xui":[{"uhs":"1234567890"}]}}
            """);
        var authenticator = new XboxLiveAuthenticator(stub.CreateClient());

        var token = await authenticator.AuthenticateAsync("msa-token");

        Assert.Equal("xbl-token", token.Token);
        Assert.Equal("1234567890", token.UserHash);

        using var sent = JsonDocument.Parse(stub.Requests[0]);
        var properties = sent.RootElement.GetProperty("Properties");
        Assert.Equal("RPS", properties.GetProperty("AuthMethod").GetString());
        Assert.Equal("d=msa-token", properties.GetProperty("RpsTicket").GetString());
        Assert.Equal("http://auth.xboxlive.com", sent.RootElement.GetProperty("RelyingParty").GetString());
    }

    [Fact]
    public async Task XstsUsesTheMinecraftRelyingParty()
    {
        var stub = new StubHandler().RespondOk("""
            {"Token":"xsts-token","DisplayClaims":{"xui":[{"uhs":"1234567890"}]}}
            """);
        var authenticator = new XboxLiveAuthenticator(stub.CreateClient());

        var xsts = await authenticator.AuthorizeAsync(new XboxUserToken("xbl-token", "1234567890"));

        Assert.Equal("xsts-token", xsts.Token);
        Assert.Equal("1234567890", xsts.UserHash);

        using var sent = JsonDocument.Parse(stub.Requests[0]);
        Assert.Equal("rp://api.minecraftservices.com/", sent.RootElement.GetProperty("RelyingParty").GetString());
        Assert.Equal("RETAIL", sent.RootElement.GetProperty("Properties").GetProperty("SandboxId").GetString());
        Assert.Equal("xbl-token", sent.RootElement.GetProperty("Properties").GetProperty("UserTokens")[0].GetString());
    }

    [Theory]
    [InlineData(2148916233L, AuthError.NoXboxProfile)]
    [InlineData(2148916238L, AuthError.AccountRestricted)]
    [InlineData(2148916235L, AuthError.UnexpectedResponse)]
    public async Task XstsErrorsAreClassified(long xerr, AuthError expected)
    {
        var stub = new StubHandler().Respond(
            HttpStatusCode.Unauthorized,
            $$"""{"Identity":"0","XErr":{{xerr}},"Message":"","Redirect":"https://xbox.com"}""");
        var authenticator = new XboxLiveAuthenticator(stub.CreateClient());

        var failure = await Assert.ThrowsAsync<AuthException>(
            () => authenticator.AuthorizeAsync(new XboxUserToken("t", "u")));

        Assert.Equal(expected, failure.Error);
        Assert.Equal(xerr, failure.ServiceCode);
    }

    [Fact]
    public async Task IdentityTokenIsBuiltTheWayTheServicesApiExpects()
    {
        var xsts = new XstsToken("xsts-token", "user-hash");
        Assert.Equal("XBL3.0 x=user-hash;xsts-token", MinecraftServicesClient.BuildIdentityToken(xsts));

        var stub = new StubHandler().RespondOk("""{"access_token":"mc-token","expires_in":86400}""");
        var client = new MinecraftServicesClient(stub.CreateClient());

        var session = await client.LoginWithXboxAsync(xsts);

        Assert.Equal("mc-token", session.AccessToken);
        using var sent = JsonDocument.Parse(stub.Requests[0]);
        Assert.Equal("XBL3.0 x=user-hash;xsts-token", sent.RootElement.GetProperty("identityToken").GetString());
    }

    [Fact]
    public async Task ProfileIsParsed()
    {
        var stub = new StubHandler().RespondOk("""
            {"id":"0123456789abcdef0123456789abcdef","name":"Steve","skins":[{"id":"x"}],"capes":[]}
            """);
        var client = new MinecraftServicesClient(stub.CreateClient());

        var profile = await client.GetProfileAsync(new MinecraftSession("mc-token", DateTimeOffset.UtcNow));

        Assert.Equal("0123456789abcdef0123456789abcdef", profile.Id);
        Assert.Equal("Steve", profile.Name);
        Assert.True(profile.Skins);
    }

    [Fact]
    public async Task MissingProfileMeansNoOwnership()
    {
        var stub = new StubHandler().Respond(HttpStatusCode.NotFound, """{"error":"NOT_FOUND"}""");
        var client = new MinecraftServicesClient(stub.CreateClient());

        var failure = await Assert.ThrowsAsync<AuthException>(
            () => client.GetProfileAsync(new MinecraftSession("mc-token", DateTimeOffset.UtcNow)));

        Assert.Equal(AuthError.NoGameOwnership, failure.Error);
    }

    [Fact]
    public async Task SessionExpiryComesFromExpiresIn()
    {
        var time = new FixedTime(new DateTimeOffset(2026, 10, 28, 12, 0, 0, TimeSpan.Zero));
        var stub = new StubHandler().RespondOk("""{"access_token":"mc-token","expires_in":86400}""");
        var client = new MinecraftServicesClient(stub.CreateClient(), time);

        var session = await client.LoginWithXboxAsync(new XstsToken("t", "u"));

        Assert.Equal(time.GetUtcNow().AddSeconds(86400), session.ExpiresAt);
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
