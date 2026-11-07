// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Sharpcraft.Client.World;

/// <summary>One texture named by a player profile: the URL to download and the atlas path it is registered under.</summary>
/// <param name="Path">Atlas path, for example <c>capes/&lt;sha1&gt;</c>.</param>
/// <param name="Url">Absolute HTTP(S) URL from the profile property.</param>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.resources.SkinManager.TextureCache", "ref://mc-26.3-analysis/src/net/minecraft/client/resources/SkinManager.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "One cached profile texture; download is handled by PlayerSkinCache")]
public readonly record struct PlayerSkinTexture(string Path, string Url);

/// <summary>
/// The parts of a player profile the renderer consumes (reference <c>PlayerSkin</c> plus the texture-property codec).
/// The body texture is kept for the player model; cape and elytra are the only inputs of <c>use_player_texture</c>.
/// </summary>
/// <param name="Body">Skin texture, or null when the property had no SKIN entry.</param>
/// <param name="Cape">Cape texture, or null.</param>
/// <param name="Elytra">Elytra texture, or null.</param>
/// <param name="Slim">True when the SKIN metadata model is <c>slim</c>.</param>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.world.entity.player.PlayerSkin", "ref://mc-26.3-analysis/src/net/minecraft/world/entity/player/PlayerSkin.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "Body/cape/elytra paths plus slim model flag")]
public readonly record struct PlayerSkinTextures(
    PlayerSkinTexture? Body,
    PlayerSkinTexture? Cape,
    PlayerSkinTexture? Elytra,
    bool Slim);

/// <summary>
/// Decodes the base64 <c>textures</c> profile property. The payload is the same JSON shape the session service
/// returns: a top-level <c>textures</c> object whose SKIN/CAPE/ELYTRA entries carry a URL.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.resources.SkinManager", "ref://mc-26.3-analysis/src/net/minecraft/client/resources/SkinManager.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Adapter, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "Decodes the textures profile property instead of authlib's MinecraftProfileTextures")]
public static class PlayerSkinTexturesParser
{
    /// <summary>Parses a profile property value; returns false for null, malformed base64 or malformed JSON.</summary>
    public static bool TryParse(string? property, out PlayerSkinTextures textures)
    {
        textures = default;

        if (string.IsNullOrWhiteSpace(property))
        {
            return false;
        }

        byte[] json;
        try
        {
            json = Convert.FromBase64String(property);
        }
        catch (FormatException)
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("textures", out var texturesElement) ||
                texturesElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            var slim = false;
            if (texturesElement.TryGetProperty("SKIN", out var skinElement) &&
                skinElement.ValueKind == JsonValueKind.Object &&
                skinElement.TryGetProperty("metadata", out var metadata) &&
                metadata.ValueKind == JsonValueKind.Object &&
                metadata.TryGetProperty("model", out var model) &&
                model.ValueKind == JsonValueKind.String)
            {
                slim = string.Equals(model.GetString(), "slim", StringComparison.OrdinalIgnoreCase);
            }

            textures = new PlayerSkinTextures(
                ReadTexture(texturesElement, "SKIN", "skins"),
                ReadTexture(texturesElement, "CAPE", "capes"),
                ReadTexture(texturesElement, "ELYTRA", "elytra"),
                slim);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>Builds the atlas path for a texture URL exactly like the reference texture cache does.</summary>
    /// <param name="kind">Atlas root: <c>skins</c>, <c>capes</c> or <c>elytra</c>.</param>
    /// <param name="url">Texture URL whose last path segment is the texture hash.</param>
    public static string TexturePath(string kind, string url)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(url);

        var slash = url.LastIndexOf('/');
        var hash = slash >= 0 ? url[(slash + 1)..] : url;
        if (hash.Length == 0)
        {
            throw new ArgumentException("texture URL has no hash segment", nameof(url));
        }

        // The reference cache hashes the unencoded characters (UTF-16 code units), not the UTF-8 bytes.
        var digest = SHA1.HashData(Encoding.Unicode.GetBytes(hash));
        return kind + "/" + Convert.ToHexStringLower(digest);
    }

    private static PlayerSkinTexture? ReadTexture(JsonElement textures, string key, string kind)
    {
        if (!textures.TryGetProperty(key, out var element) ||
            element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty("url", out var urlElement) ||
            urlElement.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var url = urlElement.GetString();
        if (string.IsNullOrWhiteSpace(url) ||
            !Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return null;
        }

        return new PlayerSkinTexture(TexturePath(kind, url), url);
    }
}
