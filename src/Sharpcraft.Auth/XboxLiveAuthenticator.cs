// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Net;
using System.Text.Json;

namespace Sharpcraft.Auth;

/// <summary>
/// Xbox Live user authentication and XSTS authorisation (docs/modules/auth.md 3, steps 3-4).
/// </summary>
public sealed class XboxLiveAuthenticator
{
    private const long NoXboxProfileCode = 2148916233L;
    private const long ChildAccountCode = 2148916238L;

    private static readonly Uri UserAuthUri = new("https://user.auth.xboxlive.com/user/authenticate");
    private static readonly Uri XstsUri = new("https://xsts.auth.xboxlive.com/xsts/authorize");

    private readonly HttpClient _http;

    /// <summary>Creates the authenticator.</summary>
    public XboxLiveAuthenticator(HttpClient http) => _http = http ?? throw new ArgumentNullException(nameof(http));

    /// <summary>Authenticates a Microsoft access token against Xbox Live.</summary>
    public async Task<XboxUserToken> AuthenticateAsync(string microsoftAccessToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(microsoftAccessToken))
        {
            throw new ArgumentException("access token is required", nameof(microsoftAccessToken));
        }

        var (status, body) = await AuthHttp.PostJsonAsync(
            _http,
            UserAuthUri,
            new
            {
                Properties = new
                {
                    AuthMethod = "RPS",
                    SiteName = "user.auth.xboxlive.com",
                    RpsTicket = "d=" + microsoftAccessToken
                },
                RelyingParty = "http://auth.xboxlive.com",
                TokenType = "JWT"
            },
            cancellationToken).ConfigureAwait(false);

        using (body)
        {
            var root = body.RootElement;
            if (status != HttpStatusCode.OK)
            {
                throw MapFailure(status, root, "Xbox Live user authentication");
            }

            var token = AuthHttp.String(root, "Token");
            var userHash = AuthHttp.UserHash(root);
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(userHash))
            {
                throw new AuthException(AuthError.UnexpectedResponse, "Xbox Live response is missing Token or user hash");
            }

            return new XboxUserToken(token, userHash);
        }
    }

    /// <summary>Authorises the relying party (Minecraft services) with an XBL user token.</summary>
    public async Task<XstsToken> AuthorizeAsync(XboxUserToken userToken, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userToken);

        var (status, body) = await AuthHttp.PostJsonAsync(
            _http,
            XstsUri,
            new
            {
                Properties = new { SandboxId = "RETAIL", UserTokens = new[] { userToken.Token } },
                RelyingParty = "rp://api.minecraftservices.com/",
                TokenType = "JWT"
            },
            cancellationToken).ConfigureAwait(false);

        using (body)
        {
            var root = body.RootElement;
            if (status != HttpStatusCode.OK)
            {
                throw MapFailure(status, root, "XSTS authorisation");
            }

            var token = AuthHttp.String(root, "Token");
            var userHash = AuthHttp.UserHash(root) ?? userToken.UserHash;
            if (string.IsNullOrEmpty(token))
            {
                throw new AuthException(AuthError.UnexpectedResponse, "XSTS response is missing Token");
            }

            return new XstsToken(token, userHash);
        }
    }

    private static AuthException MapFailure(HttpStatusCode status, JsonElement root, string stage)
    {
        var xerr = AuthHttp.Long(root, "XErr");
        return xerr switch
        {
            NoXboxProfileCode => new AuthException(
                AuthError.NoXboxProfile,
                $"{stage} failed: the account has no Xbox profile yet",
                xerr),
            ChildAccountCode => new AuthException(
                AuthError.AccountRestricted,
                $"{stage} failed: the account needs guardian consent",
                xerr),
            null => new AuthException(AuthError.UnexpectedResponse, $"{stage} failed with {(int)status}", (int)status),
            _ => new AuthException(AuthError.UnexpectedResponse, $"{stage} failed with XErr {xerr}", xerr)
        };
    }
}
