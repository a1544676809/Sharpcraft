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
/// Issue 266: unoccluded model and cube faces must have identical vertex brightness across all four corners, and
/// negative-Z cube faces must map each baked vertex to the matching world corner without swapping corners.
/// </summary>
public sealed class Repro_266Tests
{
    private const int BlockX = 8;
    private const int BlockY = 6;
    private const int BlockZ = 8;

    private static BlockSpriteMap Sprites()
    {
        var opaque = new byte[16 * 16 * 4];
        Array.Fill(opaque, (byte)255);

        var entries = new List<(Identifier Location, RgbaImage Image, int Frames)>
        {
            (Identifier.Parse("minecraft:block/stone"), new RgbaImage(16, 16, opaque), 1),
            (Identifier.Parse("minecraft:block/grass_block_top"), new RgbaImage(16, 16, opaque), 1),
            (Identifier.Parse("minecraft:block/grass_block_side"), new RgbaImage(16, 16, opaque), 1),
            (Identifier.Parse("minecraft:block/dirt"), new RgbaImage(16, 16, opaque), 1),
            (SpriteAtlas.MissingSprite, new RgbaImage(16, 16, opaque), 1)
        };

        return new BlockSpriteMap(SpriteAtlasBuilder.Stitch(entries));
    }

    private static BlockStateLayout Layout() =>
        new(
        [
            (Identifier.Parse("minecraft:air"), 1),
            (Identifier.Parse("minecraft:grass_block"), 1),
            (Identifier.Parse("minecraft:stone"), 1)
        ]);

    private static BlockModelSet SingleBlockModelSet(BlockStateLayout layout)
    {
        const string faces =
            "\"up\":{\"texture\":\"#all\"},"
            + "\"down\":{\"texture\":\"#all\"},"
            + "\"north\":{\"texture\":\"#all\"},"
            + "\"south\":{\"texture\":\"#all\"},"
            + "\"west\":{\"texture\":\"#all\"},"
            + "\"east\":{\"texture\":\"#all\"}";

        var json = $"{{\"elements\":[{{\"from\":[0,0,0],\"to\":[16,16,16],\"faces\":{{{faces}}}}}]}}";
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);

        var library = new BlockModelLibrary(
            id => id.Path == "stone" ? Identifier.Parse("minecraft:block/test_model") : null,
            new BlockModelElements(_ => bytes),
            (_, _) => Identifier.Parse("minecraft:block/stone"));

        return BlockModelSet.Build(library, layout)!;
    }

    private static void FillFullSkyLight(LevelChunk chunk, int minSectionY)
    {
        var sky = new byte[2048];
        Array.Fill(sky, (byte)0xFF);
        var block = new byte[2048];
        var sectionIndex = 0 - minSectionY;
        chunk.Light.SetSkyLight(sectionIndex, sky);
        chunk.Light.SetBlockLight(sectionIndex, block);
    }

    private static (int R, int G, int B, int Sky, int Block) ReadLighting(ReadOnlySpan<float> vertices, int vertex)
    {
        var colour = BitConverter.SingleToUInt32Bits(vertices[(vertex * ChunkMesher.FloatsPerVertex) + 3]);
        var packed = BitConverter.SingleToUInt32Bits(vertices[(vertex * ChunkMesher.FloatsPerVertex) + 5]);
        return (
            (int)(colour & 0xFF),
            (int)((colour >> 8) & 0xFF),
            (int)((colour >> 16) & 0xFF),
            (int)((packed >> 16) & 0xFF),
            (int)((packed >> 24) & 0xFF));
    }

    [Fact]
    public void UnoccludedModelFullFaceHasEqualCornerBrightnessAndLight()
    {
        var layout = Layout();
        var world = new ClientWorld(minSectionY: -1, sectionCount: 3);
        var chunk = world.AddChunk(0, 0);
        var section = new ChunkSection(0, 0);
        section.Set(BlockX, BlockY + 1, BlockZ, 2);
        chunk.SetSection(section);
        FillFullSkyLight(chunk, world.MinSectionY);
        chunk.MarkLoaded();

        using var buffer = new MeshBuffer();
        ChunkMesher.MeshChunk(
            world,
            chunk,
            airState: 0,
            buffer,
            maxBlocks: 2_000_000,
            sprites: Sprites(),
            layout: layout,
            models: SingleBlockModelSet(layout),
            ambientOcclusion: true);

        Assert.Equal(6 * 4, buffer.VertexCount);
        var vertices = buffer.Vertices;

        for (var quad = 0; quad < 6; quad++)
        {
            var first = quad * 4;
            var reference = ReadLighting(vertices, first);
            for (var corner = 1; corner < 4; corner++)
            {
                Assert.Equal(reference, ReadLighting(vertices, first + corner));
            }
        }
    }

    [Fact]
    public void TwoBlockStonePillarTopBlockSideFacesHaveUniformBrightness()
    {
        var layout = Layout();
        var world = new ClientWorld(minSectionY: -1, sectionCount: 3);
        var chunk = world.AddChunk(0, 0);
        var section = new ChunkSection(0, 0);

        for (var dx = -2; dx <= 2; dx++)
        {
            for (var dz = -2; dz <= 2; dz++)
            {
                section.Set(BlockX + dx, BlockY, BlockZ + dz, 1);
            }
        }

        section.Set(BlockX, BlockY + 1, BlockZ, 2);
        section.Set(BlockX, BlockY + 2, BlockZ, 2);
        chunk.SetSection(section);
        FillFullSkyLight(chunk, world.MinSectionY);
        chunk.MarkLoaded();

        using var buffer = new MeshBuffer();
        ChunkMesher.MeshChunk(
            world,
            chunk,
            airState: 0,
            buffer,
            maxBlocks: 2_000_000,
            sprites: Sprites(),
            layout: layout,
            ambientOcclusion: true);

        var vertices = buffer.Vertices;
        var checkedSideQuads = 0;

        for (var quad = 0; quad * 4 + 3 < buffer.VertexCount; quad++)
        {
            var first = quad * 4;
            var minY = float.MaxValue;
            var maxY = float.MinValue;
            var minX = float.MaxValue;
            var maxX = float.MinValue;
            var minZ = float.MaxValue;
            var maxZ = float.MinValue;

            for (var corner = 0; corner < 4; corner++)
            {
                var at = (first + corner) * ChunkMesher.FloatsPerVertex;
                minX = Math.Min(minX, vertices[at]);
                maxX = Math.Max(maxX, vertices[at]);
                minY = Math.Min(minY, vertices[at + 1]);
                maxY = Math.Max(maxY, vertices[at + 1]);
                minZ = Math.Min(minZ, vertices[at + 2]);
                maxZ = Math.Max(maxZ, vertices[at + 2]);
            }

            var isTopStoneSideFace =
                minY == BlockY + 2 && maxY == BlockY + 3 &&
                ((minX == maxX && (minX == BlockX || minX == BlockX + 1)) ||
                 (minZ == maxZ && (minZ == BlockZ || minZ == BlockZ + 1)));

            if (!isTopStoneSideFace)
            {
                continue;
            }

            checkedSideQuads++;
            var reference = ReadLighting(vertices, first);
            for (var corner = 1; corner < 4; corner++)
            {
                Assert.Equal(reference, ReadLighting(vertices, first + corner));
            }
        }

        Assert.Equal(4, checkedSideQuads);
    }

    [Fact]
    public void NorthCubeFaceWithBottomOccluderDarkensBottomCornersWithoutSwapping()
    {
        var layout = Layout();
        var world = new ClientWorld(minSectionY: -1, sectionCount: 3);
        var chunk = world.AddChunk(0, 0);
        var section = new ChunkSection(0, 0);

        section.Set(BlockX, BlockY, BlockZ, 1);
        section.Set(BlockX, BlockY, BlockZ - 1, 1);
        section.Set(BlockX, BlockY + 1, BlockZ, 2);
        chunk.SetSection(section);
        FillFullSkyLight(chunk, world.MinSectionY);
        chunk.MarkLoaded();

        using var buffer = new MeshBuffer();
        ChunkMesher.MeshChunk(
            world,
            chunk,
            airState: 0,
            buffer,
            maxBlocks: 2_000_000,
            sprites: Sprites(),
            layout: layout,
            ambientOcclusion: true);

        var vertices = buffer.Vertices;
        var foundNorthFace = false;

        for (var quad = 0; quad * 4 + 3 < buffer.VertexCount; quad++)
        {
            var first = quad * 4;
            var isNorthFace = true;
            for (var corner = 0; corner < 4; corner++)
            {
                var at = (first + corner) * ChunkMesher.FloatsPerVertex;
                isNorthFace &= vertices[at] >= BlockX && vertices[at] <= BlockX + 1
                    && vertices[at + 1] >= BlockY + 1 && vertices[at + 1] <= BlockY + 2
                    && vertices[at + 2] == BlockZ;
            }

            if (!isNorthFace)
            {
                continue;
            }

            foundNorthFace = true;
            for (var corner = 0; corner < 4; corner++)
            {
                var y = vertices[((first + corner) * ChunkMesher.FloatsPerVertex) + 1];
                var lighting = ReadLighting(vertices, first + corner);
                var expected = y == BlockY + 1 ? 163 : 204;
                Assert.Equal(expected, lighting.R);
            }
        }

        Assert.True(foundNorthFace);
    }
}
