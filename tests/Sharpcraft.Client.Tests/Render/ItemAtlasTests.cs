// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Common.Imaging;
using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

public sealed class ItemAtlasTests
{
    [Fact]
    public void PacksItemTexturesWithoutOverlappingSprites()
    {
        var images = new Dictionary<string, TextureData>(StringComparer.Ordinal)
        {
            ["item/potion"] = Solid(16, 16, 255, 0, 0),
            ["item/iron_sword"] = Solid(16, 16, 0, 255, 0)
        };

        var atlas = ItemAtlas.FromTextures(images);
        var potion = atlas.Get("item/potion");
        var sword = atlas.Get("item/iron_sword");

        Assert.NotNull(potion);
        Assert.NotNull(sword);
        Assert.Equal(16, atlas.CellWidth);
        Assert.Equal(16, atlas.CellHeight);
        Assert.NotEqual(potion, sword);
        Assert.True(potion!.U0 >= 0f && potion.U1 <= 1f);
        Assert.True(sword!.V0 >= 0f && sword.V1 <= 1f);
    }

    [Fact]
    public void MissingTextureUsesTheCheckerboardFallback()
    {
        var atlas = ItemAtlas.FromTextures(new Dictionary<string, TextureData>());
        Assert.Equal(16, atlas.Texture.Width);
        Assert.Equal(16, atlas.Texture.Height);
        Assert.Null(atlas.Get("item/missing"));
    }

    [Fact]
    public void LoadSkipsMissingResourcesAndKeepsPresentOnes()
    {
        var root = Path.Combine(Path.GetTempPath(), "sharpcraft-item-atlas-" + Guid.NewGuid().ToString("N"));
        try
        {
            var path = Path.Combine(root, "assets/minecraft/textures/item/potion.png");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var image = Solid(16, 16, 0, 0, 255);
            File.WriteAllBytes(path, PngEncoder.EncodeRgba(image.Width, image.Height, image.Rgba));

            using var stack = new AssetStack { Policy = MissingResourcePolicy.Warn, Warn = _ => { } }
                .Add(new DirectoryAssetSource(root, "item-test"));
            using var resources = new ResourceManager(stack, 1);
            var atlas = ItemAtlas.Load(resources, ["item/potion", "item/not_present"]);

            Assert.NotNull(atlas.Get("item/potion"));
            Assert.Null(atlas.Get("item/not_present"));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static TextureData Solid(int width, int height, byte r, byte g, byte b)
    {
        var rgba = new byte[width * height * 4];
        for (var i = 0; i < width * height; i++)
        {
            rgba[(i * 4)] = r;
            rgba[(i * 4) + 1] = g;
            rgba[(i * 4) + 2] = b;
            rgba[(i * 4) + 3] = 255;
        }

        return new TextureData(width, height, rgba);
    }
}
