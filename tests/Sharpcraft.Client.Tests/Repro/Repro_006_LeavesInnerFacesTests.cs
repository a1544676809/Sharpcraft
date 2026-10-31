// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
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
/// Repro-006：树叶看起来"只有外面一层"。原版在"精细树叶"开启时**不剔除相邻树叶之间的面**
/// （`LeavesBlock.skipRendering` 只在关闭精细树叶时剔除同类），所以树冠从里面看也是实的。
/// 本用例钉住：3×3×3 的树叶，所有 162 个面都要发出（不剔除内部面）。
/// 关联：docs/analysis/render/transparent-blocks.md；`ChunkMesher.ShouldCullFace`。
/// </summary>
public sealed class Repro_006_LeavesKeepInnerFacesTests
{
    [Fact]
    public void ACubeOfLeavesKeepsEveryFace()
    {
        var atlas = SpriteAtlasBuilder.Stitch(
        [
            (new Identifier("minecraft", "block/oak_leaves"), Leaves(16, 16), 1),
            (SpriteAtlas.MissingSprite, Solid(16, 16), 1)
        ]);

        var layout = new BlockStateLayout(
        [
            (new Identifier("minecraft", "air"), 1),
            (new Identifier("minecraft", "oak_leaves"), 1)
        ]);

        var world = new ClientWorld(minSectionY: 0, sectionCount: 1);
        var chunk = world.AddChunk(0, 0);
        chunk.SetSection(new ChunkSection(singleState: 0, sectionY: 0));
        chunk.Light.FillSkyLight(15);

        for (var y = 8; y < 11; y++)
        {
            for (var z = 4; z < 7; z++)
            {
                for (var x = 4; x < 7; x++)
                {
                    chunk.SetBlockState(x, y, z, 1);
                }
            }
        }

        var map = new BlockSpriteMap(atlas, layout);

        using var buffer = new MeshBuffer(4096);
        ChunkMesher.MeshChunk(
            world, chunk, airState: 0, buffer, maxBlocks: 100_000, sprites: map, layout: layout);
        var mesh = buffer.ToMeshData(0);

        var area = 0d;

        for (var index = 0; index + 2 < mesh.Indices.Length; index += 3)
        {
            var a = VertexAt(mesh, mesh.Indices[index]);
            var b = VertexAt(mesh, mesh.Indices[index + 1]);
            var c = VertexAt(mesh, mesh.Indices[index + 2]);
            var ab = b - a;
            var ac = c - a;
            area += 0.5 * Vector3.Cross(ab, ac).Length();
        }

        // 27 blocks times six faces: no face between two leaf blocks is removed while fancy leaves are on.
        Assert.Equal(162d, area, 2);
    }

    private static Vector3 VertexAt(MeshData mesh, uint index)
    {
        var offset = (int)index * ChunkMesher.FloatsPerVertex;
        return new Vector3(mesh.Vertices[offset], mesh.Vertices[offset + 1], mesh.Vertices[offset + 2]);
    }

    private static RgbaImage Leaves(int width, int height)
    {
        var pixels = new byte[width * height * RgbaImage.BytesPerPixel];

        for (var i = 0; i < pixels.Length; i += RgbaImage.BytesPerPixel)
        {
            pixels[i] = 90;
            pixels[i + 1] = 120;
            pixels[i + 2] = 60;
            pixels[i + 3] = 255;
        }

        // One transparent texel is enough to make the texture a cutout, so the leaves never count as occluders.
        pixels[3] = 0;

        return new RgbaImage(width, height, pixels);
    }

    private static RgbaImage Solid(int width, int height)
    {
        var pixels = new byte[width * height * RgbaImage.BytesPerPixel];

        for (var i = 0; i < pixels.Length; i += RgbaImage.BytesPerPixel)
        {
            pixels[i] = 200;
            pixels[i + 1] = 200;
            pixels[i + 2] = 200;
            pixels[i + 3] = 255;
        }

        return new RgbaImage(width, height, pixels);
    }
}
