// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 MinecraftCSharp contributors

using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace MinecraftCSharp.Auth;

/// <summary>
/// Minecraft services: exchange an XSTS token for a session and read the player profile
/// (docs/modules/auth.md 3, steps 5-6).
/// </summary>
public sealed class MinecraftServicesClient
{
    private static readonly Uri LoginUri = new("https://api.minecraftservices.com/authentication/login_with_xbox");
    private static readonly Uri ProfileUri = new("https://api.minecraftservices.com/minecraft/profile");

    private readonly HttpClient _http;
    private readonly TimeProvider _time;

    /// <summary>Creates the client.</summary>
    public MinecraftServicesClient(HttpClient http, TimeProvider? timeProvider = null)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Builds the identity token exactly as the services API expects it.</summary>
    public static string BuildIdentityToken(XstsToken xsts)
    {
        ArgumentNullException.ThrowIfNull(xsts);
        return $"XBL3.0 x={xsts.UserHash};{xsts.Token}";
    }

    /// <summary>Logs in with an XSTS token and returns the Minecraft session.</summary>
    public async Task<MinecraftSession> LoginWithXboxAsync(XstsToken xsts, CancellationToken cancellationToken = default)
    {
        var (status, body) = await AuthHttp.PostJsonAsync(
            _http,
            LoginUri,
            new { identityToken = BuildIdentityToken(xsts) },
            cancellationToken).ConfigureAwait(false);

        using (body)
        {
            var root = body.RootElement;
            if (status != HttpStatusCode.OK)
            {
                throw new AuthException(
                    AuthError.UnexpectedResponse,
                    $"Minecraft login failed with {(int)status}",
                    (int)status);
            }

            var accessToken = AuthHttp.String(root, "access_token");
            if (string.IsNullOrEmpty(accessToken))
            {
                throw new AuthException(AuthError.UnexpectedResponse, "Minecraft login response is missing access_token");
            }

            var expiresIn = AuthHttp.Int(root, "expires_in") ?? 86_400;
            return new MinecraftSession(accessToken, _time.GetUtcNow().AddSeconds(expiresIn));
        }
    }

    /// <summary>Reads the player profile; a 404 means the account owns no game profile.</summary>
    public async Task<MinecraftProfile> GetProfileAsync(MinecraftSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        using var request = new HttpRequestMessage(HttpMethod.Get, ProfileUri);
        if (Environment.GetEnvironmentVariable("MC_TRANSPORT_DEBUG") is "1")
        {
            Console.Error.WriteLine($"  [debug] GET {ProfileUri}");
        }
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException exception)
        {
            throw new AuthException(AuthError.Transport, "profile request failed", inner: exception);
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                throw new AuthException(
                    AuthError.NoGameOwnership,
                    "the account has no Minecraft profile (game not owned or not migrated)",
                    404);
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new AuthException(
                    AuthError.UnexpectedResponse,
                    $"profile request failed with {(int)response.StatusCode}",
                    (int)response.StatusCode);
            }

            var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;

            var id = AuthHttp.String(root, "id");
            var name = AuthHttp.String(root, "name");
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(name))
            {
                throw new AuthException(AuthError.UnexpectedResponse, "profile response is missing id or name");
            }

            return new MinecraftProfile(id, name, root.TryGetProperty("skins", out _));
        }
    }
}
