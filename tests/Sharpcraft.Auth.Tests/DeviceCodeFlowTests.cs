// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Net;
using System.Text.Json;
using Xunit;

namespace Sharpcraft.Auth.Tests;

/// <summary>
/// Device code flow behaviour (docs/modules/auth.md 3-4), all offline.
/// </summary>
public sealed class DeviceCodeFlowTests
{
    private const string ChallengeBody = """
        {
          "device_code": "device-abc",
          "user_code": "ABCD-EFGH",
          "verification_uri": "https://microsoft.com/link",
          "expires_in": 900,
          "interval": 5,
          "message": "enter the code"
        }
        """;

    [Fact]
    public async Task RequestCodeParsesTheChallenge()
    {
        var stub = new StubHandler().RespondOk(ChallengeBody);
        var flow = new DeviceCodeFlow(stub.CreateClient());

        var challenge = await flow.RequestCodeAsync();

        Assert.Equal("device-abc", challenge.DeviceCode);
        Assert.Equal("ABCD-EFGH", challenge.UserCode);
        Assert.Equal("https://microsoft.com/link", challenge.VerificationUri);
        Assert.Equal(TimeSpan.FromSeconds(900), challenge.ExpiresIn);
        Assert.Equal(TimeSpan.FromSeconds(5), challenge.Interval);
        Assert.Equal("enter the code", challenge.Message);
        Assert.Contains("ABCD-EFGH", challenge.ToString());

        // The request must be form-urlencoded (the identity endpoint rejects JSON) and carry
        // our client id plus the offline_access scope.
        var sent = ParseForm(stub.Requests[0]);
        Assert.Equal(DeviceCodeFlow.DefaultClientId, sent["client_id"]);
        Assert.Contains("offline_access", sent["scope"]);
        // The Microsoft account endpoint needs response_type=device_code; AAD rejects it.
        Assert.Equal("device_code", sent["response_type"]);
    }

    [Fact]
    public async Task AzureAdPresetOmitsResponseTypeAndUsesTheV2Endpoints()
    {
        var stub = new StubHandler().RespondOk(ChallengeBody);
        var flow = new DeviceCodeFlow(stub.CreateClient(), IdentityEndpoints.AzureAd("11111111-2222-3333-4444-555555555555"));

        await flow.RequestCodeAsync();

        Assert.Contains("/oauth2/v2.0/devicecode", stub.Uris[0].AbsolutePath);
        var sent = ParseForm(stub.Requests[0]);
        Assert.False(sent.ContainsKey("response_type"));
        Assert.Equal("11111111-2222-3333-4444-555555555555", sent["client_id"]);
    }

    [Fact]
    public async Task DefaultPresetTargetsTheMicrosoftAccountEndpoints()
    {
        var stub = new StubHandler().RespondOk(ChallengeBody);
        var flow = new DeviceCodeFlow(stub.CreateClient());

        await flow.RequestCodeAsync();

        Assert.Equal("login.live.com", stub.Uris[0].Host);
        Assert.Equal("/oauth20_connect.srf", stub.Uris[0].AbsolutePath);
    }

    [Fact]
    public async Task PollWaitsThroughPendingThenSucceeds()
    {
        var delays = new List<TimeSpan>();
        var stub = new StubHandler()
            .Respond(HttpStatusCode.BadRequest, """{"error":"authorization_pending"}""")
            .Respond(HttpStatusCode.BadRequest, """{"error":"authorization_pending"}""")
            .RespondOk("""{"access_token":"msa-token","refresh_token":"refresh-token","expires_in":3600}""");

        var flow = new DeviceCodeFlow(stub.CreateClient(), delay: (d, _) =>
        {
            delays.Add(d);
            return Task.CompletedTask;
        });

        var token = await flow.PollAsync(Challenge(intervalSeconds: 5));

        Assert.Equal("msa-token", token.AccessToken);
        Assert.Equal("refresh-token", token.RefreshToken);
        Assert.Equal(2, delays.Count);
        Assert.All(delays, delay => Assert.Equal(TimeSpan.FromSeconds(5), delay));
        Assert.Equal(0, stub.Remaining);
    }

    [Fact]
    public async Task SlowDownIncreasesTheInterval()
    {
        var delays = new List<TimeSpan>();
        var stub = new StubHandler()
            .Respond(HttpStatusCode.BadRequest, """{"error":"slow_down"}""")
            .RespondOk("""{"access_token":"msa","expires_in":60}""");

        var flow = new DeviceCodeFlow(stub.CreateClient(), delay: (d, _) =>
        {
            delays.Add(d);
            return Task.CompletedTask;
        });

        await flow.PollAsync(Challenge(intervalSeconds: 5));

        Assert.Single(delays);
        Assert.Equal(TimeSpan.FromSeconds(10), delays[0]);
    }

    [Theory]
    [InlineData("expired_token", AuthError.DeviceCodeExpired)]
    [InlineData("authorization_declined", AuthError.AuthorizationDeclined)]
    [InlineData("invalid_grant", AuthError.RefreshTokenRejected)]
    public async Task ErrorsAreClassified(string providerError, AuthError expected)
    {
        var stub = new StubHandler().Respond(HttpStatusCode.BadRequest, $$"""{"error":"{{providerError}}"}""");
        var flow = new DeviceCodeFlow(stub.CreateClient(), delay: (_, _) => Task.CompletedTask);

        var failure = await Assert.ThrowsAsync<AuthException>(() => flow.PollAsync(Challenge(5)));
        Assert.Equal(expected, failure.Error);
    }

    [Fact]
    public async Task PollStopsWhenTheChallengeExpires()
    {
        // Zero lifetime: the loop must refuse immediately instead of polling forever.
        var stub = new StubHandler().Respond(HttpStatusCode.BadRequest, """{"error":"authorization_pending"}""");
        var flow = new DeviceCodeFlow(stub.CreateClient(), delay: (_, _) => Task.CompletedTask);

        var challenge = new DeviceCodeChallenge("d", "U", "https://x", TimeSpan.Zero, TimeSpan.FromSeconds(5), "");
        var failure = await Assert.ThrowsAsync<AuthException>(() => flow.PollAsync(challenge));
        Assert.Equal(AuthError.DeviceCodeExpired, failure.Error);
        // An already-expired challenge must not even poll once.
        Assert.Empty(stub.Requests);
        Assert.Equal(1, stub.Remaining);
    }

    [Fact]
    public async Task MissingTokenFieldsAreRejected()
    {
        var stub = new StubHandler().RespondOk("""{"token_type":"Bearer"}""");
        var flow = new DeviceCodeFlow(stub.CreateClient(), delay: (_, _) => Task.CompletedTask);

        var failure = await Assert.ThrowsAsync<AuthException>(() => flow.PollAsync(Challenge(5)));
        Assert.Equal(AuthError.UnexpectedResponse, failure.Error);
    }

    [Fact]
    public async Task RefreshUsesTheRefreshGrantAndKeepsTheOldTokenWhenAbsent()
    {
        var stub = new StubHandler().RespondOk("""{"access_token":"new-access","expires_in":3600}""");
        var flow = new DeviceCodeFlow(stub.CreateClient());

        var token = await flow.RefreshAsync("old-refresh");

        Assert.Equal("new-access", token.AccessToken);
        Assert.Equal("old-refresh", token.RefreshToken);

        var sent = ParseForm(stub.Requests[0]);
        Assert.Equal("refresh_token", sent["grant_type"]);
        Assert.Equal("old-refresh", sent["refresh_token"]);
        Assert.Equal(DeviceCodeFlow.DefaultClientId, sent["client_id"]);
    }

    [Fact]
    public async Task RefreshRejectionIsClassified()
    {
        var stub = new StubHandler().Respond(HttpStatusCode.BadRequest, """{"error":"invalid_grant"}""");
        var flow = new DeviceCodeFlow(stub.CreateClient());

        var failure = await Assert.ThrowsAsync<AuthException>(() => flow.RefreshAsync("stale"));
        Assert.Equal(AuthError.RefreshTokenRejected, failure.Error);
    }

    [Fact]
    public async Task PollSendsTheDeviceCodeGrantAsAForm()
    {
        var stub = new StubHandler().RespondOk("""{"access_token":"a","expires_in":60}""");
        var flow = new DeviceCodeFlow(stub.CreateClient(), delay: (_, _) => Task.CompletedTask);

        await flow.PollAsync(Challenge(5));

        var sent = ParseForm(stub.Requests[0]);
        Assert.Equal("urn:ietf:params:oauth:grant-type:device_code", sent["grant_type"]);
        Assert.Equal("device-code", sent["device_code"]);
    }

    private static Dictionary<string, string> ParseForm(string body)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in body.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=');
            var key = Uri.UnescapeDataString(pair[..separator].Replace('+', ' '));
            var value = Uri.UnescapeDataString(pair[(separator + 1)..].Replace('+', ' '));
            fields[key] = value;
        }

        return fields;
    }

    private static DeviceCodeChallenge Challenge(int intervalSeconds) =>
        new("device-code", "USER-CODE", "https://microsoft.com/link", TimeSpan.FromMinutes(15),
            TimeSpan.FromSeconds(intervalSeconds), "message");
}
