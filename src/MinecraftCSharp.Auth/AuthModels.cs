// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 MinecraftCSharp contributors

namespace MinecraftCSharp.Auth;

/// <summary>Distinguishable failure kinds of the authentication chain (docs/modules/auth.md 4).</summary>
public enum AuthError
{
    /// <summary>Unexpected or unmapped failure.</summary>
    Unknown,

    /// <summary>The device code expired before the user finished.</summary>
    DeviceCodeExpired,

    /// <summary>The user declined the authorisation.</summary>
    AuthorizationDeclined,

    /// <summary>The refresh token is no longer valid; interaction is required.</summary>
    RefreshTokenRejected,

    /// <summary>The account has no Xbox profile.</summary>
    NoXboxProfile,

    /// <summary>The account is restricted (for example a child account needing consent).</summary>
    AccountRestricted,

    /// <summary>The account does not own Minecraft.</summary>
    NoGameOwnership,

    /// <summary>The HTTP endpoint returned an unexpected status.</summary>
    UnexpectedResponse,

    /// <summary>The transport failed.</summary>
    Transport
}

/// <summary>Authentication failure with a classified reason and no token material.</summary>
public sealed class AuthException : Exception
{
    /// <summary>Creates a failure.</summary>
    public AuthException(AuthError error, string message, long? serviceCode = null, Exception? inner = null)
        : base(message, inner)
    {
        Error = error;
        ServiceCode = serviceCode;
    }

    /// <summary>Classified reason.</summary>
    public AuthError Error { get; }

    /// <summary>Service-specific numeric code when the provider supplied one.</summary>
    public long? ServiceCode { get; }
}

/// <summary>Device code challenge shown to the user.</summary>
/// <param name="DeviceCode">Opaque code used for polling (never shown).</param>
/// <param name="UserCode">Short code the user types on the verification page.</param>
/// <param name="VerificationUri">Page the user opens.</param>
/// <param name="ExpiresIn">How long the challenge stays valid.</param>
/// <param name="Interval">Minimum polling interval.</param>
/// <param name="Message">Provider-rendered instructions.</param>
public sealed record DeviceCodeChallenge(
    string DeviceCode,
    string UserCode,
    string VerificationUri,
    TimeSpan ExpiresIn,
    TimeSpan Interval,
    string Message)
{
    /// <inheritdoc />
    public override string ToString() =>
        $"open {VerificationUri} and enter {UserCode} (valid {ExpiresIn.TotalMinutes:0} min)";
}

/// <summary>Microsoft account tokens.</summary>
/// <param name="AccessToken">Access token for downstream services.</param>
/// <param name="RefreshToken">Refresh token, sensitive; never log.</param>
/// <param name="ExpiresAt">Absolute expiry in UTC.</param>
public sealed record MicrosoftToken(string AccessToken, string? RefreshToken, DateTimeOffset ExpiresAt);

/// <summary>Xbox Live user token plus its user hash.</summary>
/// <param name="Token">XBL user token.</param>
/// <param name="UserHash">User hash used to build the XSTS identity token.</param>
public sealed record XboxUserToken(string Token, string UserHash);

/// <summary>XSTS authorisation result.</summary>
/// <param name="Token">XSTS token.</param>
/// <param name="UserHash">User hash to embed in the identity token.</param>
public sealed record XstsToken(string Token, string UserHash);

/// <summary>Minecraft session obtained from the services API.</summary>
/// <param name="AccessToken">Session token passed to the client.</param>
/// <param name="ExpiresAt">Absolute expiry in UTC.</param>
public sealed record MinecraftSession(string AccessToken, DateTimeOffset ExpiresAt);

/// <summary>Player profile from the services API.</summary>
/// <param name="Id">Profile id without dashes, as the API returns it.</param>
/// <param name="Name">Player name.</param>
/// <param name="Skins">Whether the profile exposes skins.</param>
public sealed record MinecraftProfile(string Id, string Name, bool Skins);
