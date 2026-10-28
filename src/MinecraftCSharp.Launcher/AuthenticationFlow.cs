// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 MinecraftCSharp contributors

using MinecraftCSharp.Auth;
using MinecraftCSharp.Common.Network;

namespace MinecraftCSharp.Launcher;

/// <summary>
/// Sign-in orchestration: reuse the local token cache when possible, fall back to the device
/// code flow, and walk the downstream chain to a Minecraft session (docs/modules/auth.md 3).
/// </summary>
internal static class AuthenticationFlow
{
    internal sealed record SignedIn(MicrosoftToken Microsoft, MinecraftSession Session, MinecraftProfile Profile, bool FromCache);

    internal static async Task<SignedIn> EnsureSignedInAsync(
        HttpClient http,
        TokenStore store,
        bool forceInteractive,
        Func<DeviceCodeChallenge, CancellationToken, Task>? onChallenge,
        CancellationToken cancellationToken)
    {
        var flow = new DeviceCodeFlow(http);

        if (!forceInteractive && store.Load() is { } stored)
        {
            try
            {
                var microsoft = stored.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(2)
                    ? new MicrosoftToken(stored.AccessToken, stored.RefreshToken, stored.ExpiresAt)
                    : await flow.RefreshAsync(stored.RefreshToken ?? string.Empty, cancellationToken).ConfigureAwait(false);

                var cached = await CompleteChainAsync(http, microsoft, cancellationToken).ConfigureAwait(false);
                Persist(store, microsoft, flow);
                return cached with { FromCache = true };
            }
            catch (AuthException exception) when (exception.Error == AuthError.RefreshTokenRejected)
            {
                // The cached grant is dead; drop it and fall through to interactive sign-in.
                store.Delete();
            }
            catch (ArgumentException)
            {
                store.Delete();
            }
        }

        var challenge = await flow.RequestCodeAsync(cancellationToken).ConfigureAwait(false);
        if (onChallenge is not null)
        {
            await onChallenge(challenge, cancellationToken).ConfigureAwait(false);
        }

        var fresh = await flow.PollAsync(challenge, cancellationToken).ConfigureAwait(false);
        var signedIn = await CompleteChainAsync(http, fresh, cancellationToken).ConfigureAwait(false);
        Persist(store, fresh, flow);
        return signedIn with { FromCache = false };
    }

    private static async Task<SignedIn> CompleteChainAsync(
        HttpClient http,
        MicrosoftToken microsoft,
        CancellationToken cancellationToken)
    {
        var xbox = new XboxLiveAuthenticator(http);
        var userToken = await xbox.AuthenticateAsync(microsoft.AccessToken, cancellationToken).ConfigureAwait(false);
        var xsts = await xbox.AuthorizeAsync(userToken, cancellationToken).ConfigureAwait(false);

        var services = new MinecraftServicesClient(http);
        var session = await services.LoginWithXboxAsync(xsts, cancellationToken).ConfigureAwait(false);
        var profile = await services.GetProfileAsync(session, cancellationToken).ConfigureAwait(false);
        return new SignedIn(microsoft, session, profile, FromCache: false);
    }

    private static void Persist(TokenStore store, MicrosoftToken token, DeviceCodeFlow flow)
    {
        if (token.RefreshToken is null)
        {
            return;
        }

        store.Save(token, DeviceCodeFlow.DefaultScope, flow.Endpoints.ClientId);
    }
}
