// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>Entity skin atlas layout (docs/modules/entity-render.md): a 2D grid that never exceeds the GPU limit.</summary>
public sealed class EntitySkinAtlasTests
{
    private static TextureData Image(int width, int height, byte fill) => new(width, height, Filled(width * height * 4, fill));

    private static byte[] Filled(int length, byte value)
    {
        var data = new byte[length];
        Array.Fill(data, value);
        return data;
    }

    [Fact]
    public void ManyTallSkinsStayWithinTheMaximumTextureSize()
    {
        var images = new Dictionary<string, TextureData>(StringComparer.Ordinal);
        for (var i = 0; i < 300; i++)
        {
            images[$"entity/mob_{i}"] = new TextureData(64, 128, new byte[64 * 128 * 4]);
        }

        var atlas = EntitySkinAtlas.FromTextures(images);

        // A single vertical column would be 300 * 128 = 38400 tall; the grid must keep both dimensions under 16384.
        Assert.True(atlas.Texture.Width <= 16384, $"width {atlas.Texture.Width}");
        Assert.True(atlas.Texture.Height <= 16384, $"height {atlas.Texture.Height}");
    }

    [Fact]
    public void SpriteRectsMapToTheirGridCell()
    {
        var images = new Dictionary<string, TextureData>(StringComparer.Ordinal)
        {
            ["a"] = Image(64, 64, 1),
            ["b"] = Image(64, 64, 2),
            ["c"] = Image(64, 64, 3)
        };

        var atlas = EntitySkinAtlas.FromTextures(images);
        foreach (var path in new[] { "a", "b", "c" })
        {
            var sprite = atlas.Get(path);
            Assert.NotNull(sprite);
            Assert.Equal((float)sprite!.X / atlas.Texture.Width, sprite.U0, 5);
            Assert.Equal((float)sprite.Y / atlas.Texture.Height, sprite.V0, 5);
            Assert.Equal((float)(sprite.X + 64) / atlas.Texture.Width, sprite.U1, 5);
            Assert.Equal((float)(sprite.Y + 64) / atlas.Texture.Height, sprite.V1, 5);
            Assert.True(sprite.U0 < sprite.U1 && sprite.V0 < sprite.V1);
        }
    }
}
