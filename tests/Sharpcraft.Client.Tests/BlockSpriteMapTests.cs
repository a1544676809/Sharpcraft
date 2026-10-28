// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Assets;
using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Imaging;
using Sharpcraft.Common.Registries;
using Xunit;

namespace Sharpcraft.Client.Tests;

/// <summary>
/// Block state to sprite mapping (docs/modules/render.md §6.1): the vanilla naming convention, the
/// missing-sprite fallback, and the precomputed layout path that keeps per-face lookups free.
/// </summary>
public sealed class BlockSpriteMapTests
{
    [Fact]
    public void BlockIdMapsToTheVanillaTextureConvention()
    {
        var atlas = Atlas();
        var map = new BlockSpriteMap(atlas);

        var stone = map.ForBlock(new Identifier("minecraft", "stone"));
        var grass = map.ForBlock(new Identifier("minecraft", "grass_block"));

        Assert.Equal(atlas.Sprites[new Identifier("minecraft", "block/stone")], stone);
        Assert.Equal(atlas.Sprites[new Identifier("minecraft", "block/grass_block")], grass);
        Assert.NotEqual(stone, grass);
    }

    [Fact]
    public void UnknownBlockFallsBackToTheMissingSprite()
    {
        var atlas = Atlas();
        var map = new BlockSpriteMap(atlas);

        var unknown = map.ForBlock(new Identifier("minecraft", "definitely_not_a_block"));

        Assert.Equal(atlas.Sprites[SpriteAtlas.MissingSprite], unknown);
        Assert.NotEqual(atlas.Sprites[new Identifier("minecraft", "block/stone")], unknown);
    }

    [Fact]
    public void TexturePathLookupCoversRenamedTextures()
    {
        var atlas = Atlas();
        var map = new BlockSpriteMap(atlas);

        // A block whose texture does not share the block name, e.g. grass_block -> grass_block_top.
        Assert.Equal(
            atlas.Sprites[new Identifier("minecraft", "block/grass_block_top")],
            map.ForTexture("grass_block_top"));
    }

    [Fact]
    public void LayoutPrecomputesStateLookups()
    {
        var atlas = Atlas();
        var layout = new BlockStateLayout(
        [
            (new Identifier("minecraft", "air"), 1),
            (new Identifier("minecraft", "stone"), 1),
            (new Identifier("minecraft", "grass_block"), 4)
        ]);

        var map = new BlockSpriteMap(atlas, layout);

        Assert.True(map.HasLayout);
        Assert.Equal(6, layout.TotalStates);

        var stoneBase = layout.GetBaseId(new Identifier("minecraft", "stone"));
        var grassBase = layout.GetBaseId(new Identifier("minecraft", "grass_block"));

        // Every state of grass_block shares the block's sprite.
        for (var offset = 0; offset < 4; offset++)
        {
            Assert.Equal(
                atlas.Sprites[new Identifier("minecraft", "block/grass_block")],
                map.ForState(grassBase + offset));
        }

        Assert.Equal(atlas.Sprites[new Identifier("minecraft", "block/stone")], map.ForState(stoneBase));

        // Air has no sprite in the atlas, so it is the missing sprite rather than a crash.
        Assert.Equal(atlas.Sprites[SpriteAtlas.MissingSprite], map.ForState(layout.GetBaseId(new Identifier("minecraft", "air"))));
    }

    [Fact]
    public void WithoutALayoutEveryStateIsMissing()
    {
        var atlas = Atlas();
        var map = new BlockSpriteMap(atlas);

        Assert.False(map.HasLayout);
        Assert.Equal(atlas.Sprites[SpriteAtlas.MissingSprite], map.ForState(1));
        Assert.Equal(atlas.Sprites[SpriteAtlas.MissingSprite], map.ForState(9999));
        Assert.Equal(atlas.Sprites[SpriteAtlas.MissingSprite], map.ForState(-5));
    }

    private static SpriteAtlas Atlas() => SpriteAtlasBuilder.Stitch(
    [
        (new Identifier("minecraft", "block/stone"), Solid(16, 16, 128, 128, 128), 1),
        (new Identifier("minecraft", "block/grass_block"), Solid(16, 16, 90, 140, 60), 1),
        (new Identifier("minecraft", "block/grass_block_top"), Solid(16, 16, 100, 200, 70), 1),
        (SpriteAtlas.MissingSprite, Solid(16, 16, 255, 0, 255), 1)
    ]);

    private static RgbaImage Solid(int width, int height, byte r, byte g, byte b)
    {
        var pixels = new byte[width * height * RgbaImage.BytesPerPixel];
        for (var i = 0; i < pixels.Length; i += RgbaImage.BytesPerPixel)
        {
            pixels[i] = r;
            pixels[i + 1] = g;
            pixels[i + 2] = b;
            pixels[i + 3] = 255;
        }

        return new RgbaImage(width, height, pixels);
    }
}
