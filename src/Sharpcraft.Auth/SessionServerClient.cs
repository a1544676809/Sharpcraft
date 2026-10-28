// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Sharpcraft.Auth;

/// <summary>
/// Session server registration ("joinServer"), the step that tells Mojang which server the
/// player is joining (docs/specs/network/encryption.md 1).
/// </summary>
public sealed class SessionServerClient
{
    private static readonly Uri JoinUri = new("https://sessionserver.mojang.com/session/minecraft/join");

    private readonly HttpClient _http;

    /// <summary>Creates the client.</summary>
    public SessionServerClient(HttpClient http) => _http = http ?? throw new ArgumentNullException(nameof(http));

    /// <summary>Builds the join payload; exposed for tests.</summary>
    public static object BuildPayload(string accessToken, string profileId, string serverHash) => new
    {
        accessToken,
        selectedProfile = profileId,
        serverId = serverHash
    };

    /// <summary>Registers the join with the session server.</summary>
    /// <param name="session">Minecraft session from the login step.</param>
    /// <param name="profileId">Profile id without dashes.</param>
    /// <param name="serverHash">Signed server hash computed during the encryption exchange.</param>
    /// <exception cref="AuthException">The session server rejected the join.</exception>
    public async Task JoinAsync(
        MinecraftSession session,
        string profileId,
        string serverHash,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
        ArgumentException.ThrowIfNullOrWhiteSpace(serverHash);

        using var request = new HttpRequestMessage(HttpMethod.Post, JoinUri)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(
                    BuildPayload(session.AccessToken, profileId, serverHash),
                    AuthHttp.JsonOptions),
                System.Text.Encoding.UTF8,
                "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException exception)
        {
            throw new AuthException(AuthError.Transport, "join request failed", inner: exception);
        }

        using (response)
        {
            if (response.IsSuccessStatusCode)
            {
                return;
            }

            var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var description = text.Length > 0 ? text : $"HTTP {(int)response.StatusCode}";
            throw response.StatusCode switch
            {
                HttpStatusCode.Forbidden => new AuthException(
                    AuthError.UnexpectedResponse,
                    $"join rejected (invalid session or hash mismatch): {description}",
                    (int)response.StatusCode),
                HttpStatusCode.Unauthorized => new AuthException(
                    AuthError.RefreshTokenRejected,
                    $"join rejected (session expired): {description}",
                    (int)response.StatusCode),
                _ => new AuthException(
                    AuthError.UnexpectedResponse,
                    $"join failed: {description}",
                    (int)response.StatusCode)
            };
        }
    }
}
