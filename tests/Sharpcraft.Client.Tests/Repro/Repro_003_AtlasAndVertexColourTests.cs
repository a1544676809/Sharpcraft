// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Imaging;
using Sharpcraft.Common.Registries;
using Sharpcraft.Common.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Repro;

/// <summary>
/// Repro-003：图集相邻精灵“渗色”。最初的修复是把 UV 向内缩半个纹素，但那是用缩小采样范围换取安全，
/// 代价是每个方块最外一圈纹素永远采不到，方块边界出现细黑线。ADR-0015 改为给每个精灵留一个像素的 padding，
/// UV 覆盖整个精灵；本用例钉住这两点，防止有人把 padding 去掉或又把 UV 缩回去。
/// 关联：docs/modules/render.md §6.1；`SpriteAtlas`。
/// </summary>
public sealed class Repro_003_AtlasUvInsetTests
{
    [Fact]
    public void SpriteUvsCoverTheSpriteAndThePaddingRingRepeatsItsEdge()
    {
        var atlas = SpriteAtlasBuilder.Stitch(
        [
            (new Identifier("minecraft", "block/stone"), Solid(16, 16), 1),
            (SpriteAtlas.MissingSprite, Solid(16, 16), 1)
        ]);

        var sprite = atlas.Sprites[new Identifier("minecraft", "block/stone")];

        // The UV rectangle must match the sprite cell exactly, with no inset.
        Assert.Equal(sprite.X, sprite.U0 * atlas.Width, 4);
        Assert.Equal(sprite.X + sprite.Width, sprite.U1 * atlas.Width, 4);
        Assert.Equal(sprite.Y, sprite.V0 * atlas.Height, 4);
        Assert.Equal(sprite.Y + sprite.Height, sprite.V1 * atlas.Height, 4);

        // Bleed protection lives in the padding ring: the texel just outside the sprite repeats its edge.
        var inside = (((sprite.Y * atlas.Width) + sprite.X) * RgbaImage.BytesPerPixel);
        var outside = (((sprite.Y * atlas.Width) + sprite.X - 1) * RgbaImage.BytesPerPixel);

        for (var channel = 0; channel < RgbaImage.BytesPerPixel; channel++)
        {
            Assert.Equal(atlas.Pixels[inside + channel], atlas.Pixels[outside + channel]);
        }
    }

    private static RgbaImage Solid(int width, int height)
    {
        var pixels = new byte[width * height * RgbaImage.BytesPerPixel];
        for (var i = 0; i < pixels.Length; i += RgbaImage.BytesPerPixel)
        {
            pixels[i + 3] = 255;
        }

        return new RgbaImage(width, height, pixels);
    }
}

/// <summary>
/// Repro-004：顶点颜色曾经继续乘"状态哈希色"，有了纹理后把石头染成棕色。修复是"有纹理时顶点颜色只承载光照"。
/// 关联：docs/modules/render.md §6.1；`ChunkMesher`。
/// </summary>
public sealed class Repro_004_TexturedVertexColourIsLightOnlyTests
{
    [Fact]
    public void TexturedVerticesCarryNoStateTint()
    {
        var world = SingleBlockWorld();
        var atlas = SpriteAtlasBuilder.Stitch(
        [
            (new Identifier("minecraft", "block/stone"), Solid(16, 16), 1),
            (SpriteAtlas.MissingSprite, Solid(16, 16), 1)
        ]);
        var layout = new BlockStateLayout(
        [
            (new Identifier("minecraft", "air"), 1),
            (new Identifier("minecraft", "stone"), 1)
        ]);

        var mesh = ChunkMesher.Mesh(world, airState: 0, maxBlocks: 1000, sprites: new BlockSpriteMap(atlas, layout));

        for (var i = 0; i < mesh.Vertices.Length; i += ChunkMesher.FloatsPerVertex)
        {
            Assert.Equal(mesh.Vertices[i + 3], mesh.Vertices[i + 4], 5);
            Assert.Equal(mesh.Vertices[i + 4], mesh.Vertices[i + 5], 5);
        }
    }

    private static ClientWorld SingleBlockWorld()
    {
        var world = new ClientWorld(minSectionY: 0, sectionCount: 1);
        var chunk = world.AddChunk(0, 0);
        chunk.SetSection(new ChunkSection(singleState: 0, sectionY: 0));
        chunk.Light.FillSkyLight(15);
        chunk.SetBlockState(0, 0, 0, 1);
        return world;
    }

    private static RgbaImage Solid(int width, int height)
    {
        var pixels = new byte[width * height * RgbaImage.BytesPerPixel];
        for (var i = 0; i < pixels.Length; i += RgbaImage.BytesPerPixel)
        {
            pixels[i] = 143;
            pixels[i + 1] = 143;
            pixels[i + 2] = 143;
            pixels[i + 3] = 255;
        }

        return new RgbaImage(width, height, pixels);
    }
}
