// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 MinecraftCSharp contributors

namespace MinecraftCSharp.Auth;

/// <summary>
/// Identity endpoints and client id for the device code flow.
/// </summary>
/// <remarks>
/// Two families exist and they are <b>not</b> interchangeable:
/// <list type="bullet">
///   <item>
///     <b>Microsoft account</b> (client ids like <c>00000000402b5328</c>): served by
///     <c>login.live.com</c>. Sending that id to the Azure AD v2 endpoints yields
///     <c>AADSTS700016</c> ("application not found").
///   </item>
///   <item>
///     <b>Azure AD app registration</b> (GUID client ids): served by
///     <c>login.microsoftonline.com/{tenant}/oauth2/v2.0</c>.
///   </item>
/// </list>
/// Both accept the device code grant and the refresh grant, but the request bodies differ:
/// the Microsoft account endpoint also needs <c>response_type=device_code</c>.
/// </remarks>
public sealed record IdentityEndpoints(Uri DeviceCodeUri, Uri TokenUri, string ClientId, bool UsesMicrosoftAccountEndpoint)
{
    /// <summary>Community client id used by third-party launchers (Microsoft account family).</summary>
    public const string DefaultMicrosoftAccountClientId = "00000000402b5328";

    /// <summary>Microsoft account (live.com) endpoints, the default for this project.</summary>
    public static IdentityEndpoints MicrosoftAccount(string clientId = DefaultMicrosoftAccountClientId) =>
        new(
            new Uri("https://login.live.com/oauth20_connect.srf"),
            new Uri("https://login.live.com/oauth20_token.srf"),
            clientId,
            UsesMicrosoftAccountEndpoint: true);

    /// <summary>Azure AD endpoints for your own app registration.</summary>
    /// <param name="clientId">Application (client) id, a GUID.</param>
    /// <param name="tenant">Tenant segment, for example <c>consumers</c> or <c>common</c>.</param>
    public static IdentityEndpoints AzureAd(string clientId, string tenant = "consumers") =>
        new(
            new Uri($"https://login.microsoftonline.com/{tenant}/oauth2/v2.0/devicecode"),
            new Uri($"https://login.microsoftonline.com/{tenant}/oauth2/v2.0/token"),
            clientId,
            UsesMicrosoftAccountEndpoint: false);
}
