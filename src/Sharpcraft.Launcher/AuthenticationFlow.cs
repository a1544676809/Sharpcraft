// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Auth;
using Sharpcraft.Common.Network;

namespace Sharpcraft.Launcher;

/// <summary>
/// Sign-in orchestration. Renewal walks the cheapest path first (docs/modules/auth.md 5.1): a cached
/// Minecraft session that is still valid needs no request at all, an expired one is extended against
/// the services API — the only route that avoids Xbox Live — and only then does the Microsoft grant
/// and the Xbox chain get involved, with the device code as the last resort.
/// </summary>
internal static class AuthenticationFlow
{
    internal sealed record SignedIn(MicrosoftToken Microsoft, MinecraftSession Session, MinecraftProfile Profile, bool FromCache);

    /// <summary>Stable client token identifying this installation to the services API.</summary>
    internal static string ClientTokenFor(string clientId) => "sharpcraft-" + clientId;

    internal static async Task<SignedIn> EnsureSignedInAsync(
        HttpClient http,
        TokenStore store,
        bool forceInteractive,
        Func<DeviceCodeChallenge, CancellationToken, Task>? onChallenge,
        CancellationToken cancellationToken)
    {
        var flow = new DeviceCodeFlow(http);
        var stored = forceInteractive ? null : store.Load();

        if (stored is not null)
        {
            // 1) A still-valid session needs nothing at all.
            if (TryReuseValidSession(stored) is { } valid)
            {
                return valid;
            }

            // 2) An expired session is extended without Xbox Live.
            if (await TryExtendSessionAsync(http, store, stored, cancellationToken).ConfigureAwait(false) is { } extended)
            {
                return extended;
            }

            // 3) The Microsoft grant, which runs the Xbox chain.
            if (await TryCachedGrantAsync(http, store, flow, stored, cancellationToken).ConfigureAwait(false) is { } fromGrant)
            {
                return fromGrant;
            }
        }

        // 4) Interactive sign-in.
        return await InteractiveAsync(http, store, flow, onChallenge, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reuses a cached session with no network activity at all.</summary>
    private static SignedIn? TryReuseValidSession(TokenStore.StoredTokens stored)
    {
        if (!HasSession(stored) || stored.MinecraftExpiresAt <= DateTimeOffset.UtcNow.AddMinutes(5))
        {
            return null;
        }

        return new SignedIn(
            new MicrosoftToken(stored.AccessToken, stored.RefreshToken, stored.ExpiresAt),
            new MinecraftSession(stored.MinecraftAccessToken!, stored.MinecraftExpiresAt!.Value),
            new MinecraftProfile(stored.ProfileId!, stored.ProfileName!, Skins: true),
            FromCache: true);
    }

    /// <summary>
    /// Extends an expired session against the services API. Any failure returns null so the caller
    /// falls back to the Xbox chain: a rejected token is normal, not an error to surface.
    /// </summary>
    private static async Task<SignedIn?> TryExtendSessionAsync(
        HttpClient http,
        TokenStore store,
        TokenStore.StoredTokens stored,
        CancellationToken cancellationToken)
    {
        if (!HasSession(stored))
        {
            return null;
        }

        var services = new MinecraftServicesClient(http);
        var current = new MinecraftSession(stored.MinecraftAccessToken!, stored.MinecraftExpiresAt!.Value);

        MinecraftSession refreshed;
        try
        {
            refreshed = await services
                .RefreshAsync(current, ClientTokenFor(stored.ClientId), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (AuthException)
        {
            return null;
        }

        var profile = new MinecraftProfile(stored.ProfileId!, stored.ProfileName!, Skins: true);
        store.SaveSession(refreshed, profile);

        return new SignedIn(
            new MicrosoftToken(stored.AccessToken, stored.RefreshToken, stored.ExpiresAt),
            refreshed,
            profile,
            FromCache: true);
    }

    /// <summary>Refreshes the Microsoft grant and walks the Xbox chain.</summary>
    private static async Task<SignedIn?> TryCachedGrantAsync(
        HttpClient http,
        TokenStore store,
        DeviceCodeFlow flow,
        TokenStore.StoredTokens stored,
        CancellationToken cancellationToken)
    {
        MicrosoftToken microsoft;
        try
        {
            microsoft = stored.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(2)
                ? new MicrosoftToken(stored.AccessToken, stored.RefreshToken, stored.ExpiresAt)
                : await flow.RefreshAsync(stored.RefreshToken ?? string.Empty, cancellationToken).ConfigureAwait(false);
        }
        catch (AuthException exception) when (exception.Error == AuthError.RefreshTokenRejected)
        {
            store.Delete();
            return null;
        }
        catch (ArgumentException)
        {
            store.Delete();
            return null;
        }

        return await CompleteAsync(http, store, flow, microsoft, fromCache: true, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<SignedIn> InteractiveAsync(
        HttpClient http,
        TokenStore store,
        DeviceCodeFlow flow,
        Func<DeviceCodeChallenge, CancellationToken, Task>? onChallenge,
        CancellationToken cancellationToken)
    {
        var challenge = await flow.RequestCodeAsync(cancellationToken).ConfigureAwait(false);
        if (onChallenge is not null)
        {
            await onChallenge(challenge, cancellationToken).ConfigureAwait(false);
        }

        var microsoft = await flow.PollAsync(challenge, cancellationToken).ConfigureAwait(false);

        // Persisted before the Xbox chain: a downstream failure must never cost another device code.
        Persist(store, microsoft, flow);

        return await CompleteAsync(http, store, flow, microsoft, fromCache: false, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Walks Xbox Live, then stores the session next to the grant that produced it.</summary>
    private static async Task<SignedIn> CompleteAsync(
        HttpClient http,
        TokenStore store,
        DeviceCodeFlow flow,
        MicrosoftToken microsoft,
        bool fromCache,
        CancellationToken cancellationToken)
    {
        Persist(store, microsoft, flow);

        var xbox = new XboxLiveAuthenticator(http);
        var userToken = await xbox.AuthenticateAsync(microsoft.AccessToken, cancellationToken).ConfigureAwait(false);
        var xsts = await xbox.AuthorizeAsync(userToken, cancellationToken).ConfigureAwait(false);

        var services = new MinecraftServicesClient(http);
        var session = await services.LoginWithXboxAsync(xsts, cancellationToken).ConfigureAwait(false);
        var profile = await services.GetProfileAsync(session, cancellationToken).ConfigureAwait(false);

        store.SaveSession(session, profile);
        return new SignedIn(microsoft, session, profile, fromCache);
    }

    private static bool HasSession(TokenStore.StoredTokens stored) =>
        stored.MinecraftAccessToken is { Length: > 0 } &&
        stored.MinecraftExpiresAt is not null &&
        stored.ProfileId is { Length: > 0 } &&
        stored.ProfileName is { Length: > 0 };

    private static void Persist(TokenStore store, MicrosoftToken token, DeviceCodeFlow flow)
    {
        if (token.RefreshToken is null)
        {
            return;
        }

        store.Save(token, DeviceCodeFlow.DefaultScope, flow.Endpoints.ClientId);
    }
}
