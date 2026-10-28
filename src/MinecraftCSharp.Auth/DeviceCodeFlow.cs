// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 MinecraftCSharp contributors

using System.Net;
using System.Text.Json;

namespace MinecraftCSharp.Auth;

/// <summary>
/// Microsoft identity device code flow (docs/modules/auth.md 3): request a user code, then poll
/// until the user finishes, honouring the provider's back-off and error contract.
/// </summary>
public sealed class DeviceCodeFlow
{
    /// <summary>Scopes needed for Minecraft sign-in; <c>offline_access</c> yields a refresh token.</summary>
    public const string DefaultScope = "XboxLive.signin offline_access";

    /// <summary>Community client id used by third-party launchers; override for your own app.</summary>
    public const string DefaultClientId = "00000000402b5328";

    private static readonly Uri DeviceCodeUri =
        new("https://login.microsoftonline.com/consumers/oauth2/v2.0/devicecode");

    private static readonly Uri TokenUri =
        new("https://login.microsoftonline.com/consumers/oauth2/v2.0/token");

    private readonly HttpClient _http;
    private readonly string _clientId;
    private readonly string _scope;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;
    private readonly TimeProvider _time;

    /// <summary>Creates the flow, optionally overriding the client id, clock and delay.</summary>
    public DeviceCodeFlow(
        HttpClient http,
        string clientId = DefaultClientId,
        string scope = DefaultScope,
        Func<TimeSpan, CancellationToken, Task>? delay = null,
        TimeProvider? timeProvider = null)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        if (string.IsNullOrWhiteSpace(clientId))
        {
            throw new ArgumentException("client id is required", nameof(clientId));
        }

        _clientId = clientId;
        _scope = scope;
        _delay = delay ?? ((duration, token) => Task.Delay(duration, token));
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Requests a device code and returns the instructions for the user.</summary>
    /// <exception cref="AuthException">The identity service rejected the request.</exception>
    public async Task<DeviceCodeChallenge> RequestCodeAsync(CancellationToken cancellationToken = default)
    {
        var (status, body) = await AuthHttp.PostJsonAsync(
            _http,
            DeviceCodeUri,
            new { client_id = _clientId, scope = _scope },
            cancellationToken).ConfigureAwait(false);

        using (body)
        {
            if (status != HttpStatusCode.OK)
            {
                throw new AuthException(
                    AuthError.UnexpectedResponse,
                    $"device code request failed with {(int)status}",
                    (int)status);
            }

            var root = body.RootElement;
            var deviceCode = AuthHttp.String(root, "device_code");
            var userCode = AuthHttp.String(root, "user_code");
            var verificationUri = AuthHttp.String(root, "verification_uri");
            if (string.IsNullOrEmpty(deviceCode) || string.IsNullOrEmpty(userCode) || string.IsNullOrEmpty(verificationUri))
            {
                throw new AuthException(AuthError.UnexpectedResponse, "device code response is missing required fields");
            }

            var expiresIn = AuthHttp.Int(root, "expires_in") ?? 900;
            var interval = AuthHttp.Int(root, "interval") ?? 5;
            var message = AuthHttp.String(root, "message") ?? string.Empty;

            return new DeviceCodeChallenge(
                deviceCode,
                userCode,
                verificationUri,
                TimeSpan.FromSeconds(expiresIn),
                TimeSpan.FromSeconds(interval),
                message);
        }
    }

    /// <summary>Polls until the user finishes, or the challenge expires.</summary>
    /// <exception cref="AuthException">The flow failed with a classified reason.</exception>
    public async Task<MicrosoftToken> PollAsync(DeviceCodeChallenge challenge, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(challenge);

        var interval = challenge.Interval;
        var deadline = _time.GetUtcNow() + challenge.ExpiresIn;

        while (_time.GetUtcNow() < deadline)
        {
            var (status, body) = await AuthHttp.PostJsonAsync(
                _http,
                TokenUri,
                new
                {
                    grant_type = "urn:ietf:params:oauth:grant-type:device_code",
                    client_id = _clientId,
                    device_code = challenge.DeviceCode
                },
                cancellationToken).ConfigureAwait(false);

            using (body)
            {
                var root = body.RootElement;

                if (status == HttpStatusCode.OK)
                {
                    var accessToken = AuthHttp.String(root, "access_token");
                    if (string.IsNullOrEmpty(accessToken))
                    {
                        throw new AuthException(AuthError.UnexpectedResponse, "token response is missing access_token");
                    }

                    var expiresIn = AuthHttp.Int(root, "expires_in") ?? 3600;
                    return new MicrosoftToken(
                        accessToken,
                        AuthHttp.String(root, "refresh_token"),
                        _time.GetUtcNow().AddSeconds(expiresIn));
                }

                var error = AuthHttp.String(root, "error") ?? string.Empty;
                var description = AuthHttp.String(root, "error_description") ?? error;
                switch (error)
                {
                    case "authorization_pending":
                        break;

                    case "slow_down":
                        interval += TimeSpan.FromSeconds(5);
                        break;

                    case "expired_token":
                        throw new AuthException(AuthError.DeviceCodeExpired, $"device code expired: {description}");

                    case "authorization_declined":
                        throw new AuthException(AuthError.AuthorizationDeclined, $"authorization declined: {description}");

                    case "invalid_grant":
                        throw new AuthException(AuthError.RefreshTokenRejected, $"grant rejected: {description}");

                    default:
                        throw new AuthException(
                            AuthError.UnexpectedResponse,
                            $"token request failed: {(string.IsNullOrEmpty(error) ? description : error)}",
                            (int)status);
                }
            }

            if (_time.GetUtcNow() + interval >= deadline)
            {
                break;
            }

            await _delay(interval, cancellationToken).ConfigureAwait(false);
        }

        throw new AuthException(AuthError.DeviceCodeExpired, "device code flow expired before the user finished");
    }

    /// <summary>Exchanges a refresh token for a new access token.</summary>
    public async Task<MicrosoftToken> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new ArgumentException("refresh token is required", nameof(refreshToken));
        }

        var (status, body) = await AuthHttp.PostJsonAsync(
            _http,
            TokenUri,
            new
            {
                grant_type = "refresh_token",
                client_id = _clientId,
                refresh_token = refreshToken,
                scope = _scope
            },
            cancellationToken).ConfigureAwait(false);

        using (body)
        {
            var root = body.RootElement;
            if (status != HttpStatusCode.OK)
            {
                var error = AuthHttp.String(root, "error") ?? string.Empty;
                throw error == "invalid_grant"
                    ? new AuthException(AuthError.RefreshTokenRejected, "refresh token was rejected")
                    : new AuthException(AuthError.UnexpectedResponse, $"refresh failed with {(int)status}", (int)status);
            }

            var accessToken = AuthHttp.String(root, "access_token");
            if (string.IsNullOrEmpty(accessToken))
            {
                throw new AuthException(AuthError.UnexpectedResponse, "refresh response is missing access_token");
            }

            var expiresIn = AuthHttp.Int(root, "expires_in") ?? 3600;
            return new MicrosoftToken(
                accessToken,
                AuthHttp.String(root, "refresh_token") ?? refreshToken,
                _time.GetUtcNow().AddSeconds(expiresIn));
        }
    }
}
