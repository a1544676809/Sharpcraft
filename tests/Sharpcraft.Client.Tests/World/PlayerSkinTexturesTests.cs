// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Text;
using Sharpcraft.AuthLib;
using Xunit;

namespace Sharpcraft.Client.Tests.World;

/// <summary>
/// The base64 <c>textures</c> profile property and the paths the skin atlas exposes for it. These values are the
/// inputs of <c>use_player_texture</c>; see docs/modules/entity-equipment.md 11.
/// </summary>
public sealed class PlayerSkinTexturesTests
{
    private static string Property(string json) => Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

    [Fact]
    public void ParsesCapeElytraAndSlimModel()
    {
        var property = Property("""
            {
              "timestamp": 1,
              "profileId": "11111111111111111111111111111111",
              "textures": {
                "SKIN": {
                  "url": "http://textures.minecraft.net/texture/aaaa",
                  "metadata": { "model": "slim" }
                },
                "CAPE": { "url": "https://textures.minecraft.net/texture/abc123" },
                "ELYTRA": { "url": "https://textures.minecraft.net/texture/deadbeef" }
              }
            }
            """);

        Assert.True(PlayerSkinTexturesParser.TryParse(property, out var textures));
        Assert.True(textures.Slim);
        Assert.Equal("skins/ab1df493e6bf8de49a74f5d9ea8f32266343fb1d", textures.Body?.Path);
        Assert.Equal("capes/689307d2fc53af0fb941bc1bb42737ce4f3ef540", textures.Cape?.Path);
        Assert.Equal("elytra/8c00f528eca4aa2abd8617baad2a9fb47e0a2097", textures.Elytra?.Path);
        Assert.Equal("https://textures.minecraft.net/texture/abc123", textures.Cape?.Url);
    }

    [Fact]
    public void TheModelNameIsCaseSensitiveLikeTheReferenceLookup()
    {
        var property = Property("""
            { "textures": { "SKIN": { "url": "https://textures.minecraft.net/texture/aaaa", "metadata": { "model": "SLIM" } } } }
            """);

        Assert.True(PlayerSkinTexturesParser.TryParse(property, out var textures));
        Assert.False(textures.Slim);
    }

    [Fact]
    public void MissingCapeAndElytraAreNull()
    {
        var property = Property("""
            { "textures": { "SKIN": { "url": "https://textures.minecraft.net/texture/aaaa" } } }
            """);

        Assert.True(PlayerSkinTexturesParser.TryParse(property, out var textures));
        Assert.Null(textures.Cape);
        Assert.Null(textures.Elytra);
        Assert.False(textures.Slim);
    }

    [Fact]
    public void MalformedPayloadsAreRejected()
    {
        Assert.False(PlayerSkinTexturesParser.TryParse(null, out _));
        Assert.False(PlayerSkinTexturesParser.TryParse("not base64!", out _));
        Assert.False(PlayerSkinTexturesParser.TryParse(Property("{ \"timestamp\": 1 }"), out _));
    }

    [Fact]
    public void TexturePathUsesTheUnencodedCharacterHash()
    {
        Assert.Equal("capes/689307d2fc53af0fb941bc1bb42737ce4f3ef540",
            PlayerSkinTexturesParser.TexturePath("capes", "https://textures.minecraft.net/texture/abc123"));
        Assert.Throws<ArgumentException>(() => PlayerSkinTexturesParser.TexturePath("capes", "https://textures.minecraft.net/texture/"));
    }
}
