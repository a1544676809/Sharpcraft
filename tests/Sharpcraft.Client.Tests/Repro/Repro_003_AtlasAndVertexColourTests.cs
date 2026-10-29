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
/// Repro-003：最近邻采样落在图集格子边界时会取到隔壁精灵的纹素，画面表现为方块边缘"渗色"。
/// 修复是 UV 向内缩半个纹素；本用例钉住缩进量，防止有人把内缩去掉当成"坐标更直观"。
/// 关联：docs/modules/render.md §6.1；`SpriteAtlas`。
/// </summary>
public sealed class Repro_003_AtlasUvInsetTests
{
    [Fact]
    public void SpriteUvsAreInsetByHalfATexelOnEveryEdge()
    {
        var atlas = SpriteAtlasBuilder.Stitch(
        [
            (new Identifier("minecraft", "block/stone"), Solid(16, 16), 1),
            (SpriteAtlas.MissingSprite, Solid(16, 16), 1)
        ]);

        var sprite = atlas.Sprites[new Identifier("minecraft", "block/stone")];

        // Convert the UV edges back to texel space and compare with the cell rectangle.
        var left = (sprite.U0 * atlas.Width) - sprite.X;
        var right = (sprite.U1 * atlas.Width) - (sprite.X + sprite.Width);

        var top = (sprite.V0 * atlas.Height) - sprite.Y;
        var bottom = (sprite.V1 * atlas.Height) - (sprite.Y + sprite.Height);

        Assert.Equal(0.5, left, 4);
        Assert.Equal(0.5, top, 4);
        Assert.Equal(-0.5, right, 4);
        Assert.Equal(-0.5, bottom, 4);
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
