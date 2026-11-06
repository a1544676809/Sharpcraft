// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Sharpcraft.Common.Network;

namespace Sharpcraft.Client.Game;

/// <summary>
/// Login entry for the standalone client: offline sessions skip the session-server join, online sessions
/// register the join over HTTP directly. It deliberately does not depend on <c>Sharpcraft.Auth</c>: that library
/// is host plumbing (AGENTS.md principle 7/8), and the client only consumes the identity it is handed.
/// </summary>
internal static class GameConnector
{
    private static readonly Uri JoinUri = new("https://sessionserver.mojang.com/session/minecraft/join");

    /// <summary>Runs the login phase for the given identity.</summary>
    internal static Task<(LoginOutcome Outcome, MinecraftConnection? Connection)> LoginAsync(
        string address,
        HttpClient http,
        GameIdentity identity,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(address);
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(identity);

        // Offline (vanilla --offlineDeveloperMode) or no token: an offline-mode server accepts any name and asks
        // for no join, so the join callback is a no-op exactly like vanilla's OFFLINE user API.
        if (identity.Offline || string.IsNullOrWhiteSpace(identity.AccessToken))
        {
            return MinecraftLoginClient.LoginAsync(
                address,
                (identity.Name, identity.Uuid),
                string.Empty,
                static (_, _, _) => Task.CompletedTask,
                cancellationToken: cancellationToken);
        }

        return MinecraftLoginClient.LoginAsync(
            address,
            (identity.Name, identity.Uuid),
            identity.AccessToken,
            (token, hash, ct) => JoinAsync(http, token, identity.Uuid, hash, ct),
            cancellationToken: cancellationToken);
    }

    /// <summary>Registers the join with the session server (the online-mode step).</summary>
    private static async Task JoinAsync(
        HttpClient http,
        string accessToken,
        string profileId,
        string serverHash,
        CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(new
        {
            accessToken,
            selectedProfile = profileId.Replace("-", string.Empty, StringComparison.Ordinal),
            serverId = serverHash
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, JoinUri)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException(
                $"session join failed: HTTP {(int)response.StatusCode} {text}");
        }
    }
}
