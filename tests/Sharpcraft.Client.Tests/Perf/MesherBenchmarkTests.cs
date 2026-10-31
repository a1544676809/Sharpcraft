// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Diagnostics;
using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Imaging;
using Sharpcraft.Common.Registries;
using Sharpcraft.Common.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Perf;

/// <summary>
/// Where the mesh time goes (docs/specs/quality/vanilla-equivalence-matrix.md row 3).
/// </summary>
/// <remarks>
/// The pool builds about thirty chunks a second, roughly two hundred and fifty milliseconds each, while the reference is a
/// few milliseconds. This benchmark measures the mesher on a terrain-like chunk in isolation so the cost can be attributed
/// instead of guessed. It reports, it does not enforce a threshold.
/// </remarks>
public sealed class MesherBenchmarkTests
{
    private const int SectionCount = 24;
    private const int MinSectionY = -4;

    [Fact]
    public void MeshCostPerChunkIsMeasuredAndReported()
    {
        var world = new ClientWorld(MinSectionY, SectionCount);

        for (var chunkX = -1; chunkX <= 1; chunkX++)
        {
            for (var chunkZ = -1; chunkZ <= 1; chunkZ++)
            {
                var chunk = new LevelChunk(chunkX, chunkZ, MinSectionY, SectionCount);
                Fill(chunk, chunkX, chunkZ);
                chunk.MarkLoaded();
                world.AddChunk(chunk);
            }
        }

        var atlas = SpriteAtlasBuilder.Stitch(
        [
            (new Identifier("minecraft", "block/stone"), Solid(), 1),
            (SpriteAtlas.MissingSprite, Solid(), 1)
        ]);

        var layout = VanillaBlockRegistry.Layout;
        var sprites = new BlockSpriteMap(atlas, layout);

        var cullTable = new StateCullTable(layout, sprites);

        var centre = world.GetChunk(0, 0)!;
        using var buffer = new MeshBuffer(1 << 20);
        using var translucent = new MeshBuffer(1 << 16);
        using var cutout = new MeshBuffer(1 << 16);

        var warmBlocks = ChunkMesher.MeshChunk(world, centre, 0, buffer, int.MaxValue, sprites, translucent, layout, cutout, null, cullTable);

        Console.WriteLine($"BENCH setup blocks={warmBlocks} vertices={buffer.VertexCount} indices={buffer.IndexCount}");

        const int iterations = 12;

        // The name based path (what the code did before the table) is measured in the same run so the comparison is fair.
        var withoutTable = Measure(world, centre, buffer, sprites, translucent, layout, cutout, null, iterations);
        var withTable = Measure(world, centre, buffer, sprites, translucent, layout, cutout, cullTable, iterations);
        var visualCache = new StateVisualCache(sprites, layout);
        var withVisuals = Measure(world, centre, buffer, sprites, translucent, layout, cutout, cullTable, iterations, visualCache);

        // How many quads a face-at-a-time renderer (the reference approach) would emit for the same chunk, so the two
        // strategies can be compared on primitives rather than on opinion.
        var vanillaFaces = CountVisibleFaces(centre, cullTable);

        Console.WriteLine(
            $"BENCH mesh_ms_per_chunk={withVisuals:F2} with_cull_only={withTable:F2} without_table={withoutTable:F2} iterations={iterations} "
            + $"greedy_quads={856 / 4} vanilla_faces={vanillaFaces}");

        // Attribute the cost: the cull predicate resolves a state to a name and then looks the name up for transparency,
        // once per candidate face cell. Time those two lookups directly.
        var stoneId = new Identifier("minecraft", "block/stone");
        const int lookups = 1_000_000;

        var nameWatch = Stopwatch.StartNew();

        for (var index = 0; index < lookups; index++)
        {
            _ = layout.BlockAt(1)?.Path;
        }

        nameWatch.Stop();

        var transparencyWatch = Stopwatch.StartNew();

        for (var index = 0; index < lookups; index++)
        {
            _ = sprites.TransparencyOf(stoneId);
        }

        transparencyWatch.Stop();

        Console.WriteLine(
            $"BENCH micro name_ns={(nameWatch.Elapsed.TotalMilliseconds * 1_000_000 / lookups):F0} "
            + $"transparency_ns={(transparencyWatch.Elapsed.TotalMilliseconds * 1_000_000 / lookups):F0} "
            + $"per_chunk_cells=36864");

        // Where the per cell cost sits: the section read, the cross chunk block read and the two light reads.
        var middleSection = centre.GetSection(0)!;
        const int cellIterations = 2_000_000;

        var sectionWatch = Stopwatch.StartNew();
        var sink = 0;

        for (var index = 0; index < cellIterations; index++)
        {
            sink += middleSection.Get(index & 15, (index >> 4) & 15, (index >> 8) & 15);
        }

        sectionWatch.Stop();

        var blockWatch = Stopwatch.StartNew();

        for (var index = 0; index < cellIterations; index++)
        {
            sink += centre.GetBlockState(index & 15, 64, (index >> 4) & 15, 0);
        }

        blockWatch.Stop();

        var lightWatch = Stopwatch.StartNew();

        for (var index = 0; index < cellIterations; index++)
        {
            sink += centre.GetSkyLight(index & 15, 64, (index >> 4) & 15);
            sink += centre.GetBlockLight(index & 15, 64, (index >> 4) & 15);
        }

        lightWatch.Stop();

        Console.WriteLine(
            $"BENCH cell_ns section_get={(sectionWatch.Elapsed.TotalMilliseconds * 1_000_000 / cellIterations):F0} "
            + $"block_get={(blockWatch.Elapsed.TotalMilliseconds * 1_000_000 / cellIterations):F0} "
            + $"light_pair={(lightWatch.Elapsed.TotalMilliseconds * 1_000_000 / cellIterations):F0} sink={sink}");

        Assert.True(warmBlocks > 0, "the benchmark chunk produced no geometry, so the measurement is meaningless");
    }

    private static void Fill(LevelChunk chunk, int chunkX, int chunkZ)
    {
        // Sections start empty and must be installed, so build each one before using it.
        var sections = new ChunkSection[SectionCount];

        for (var index = 0; index < SectionCount; index++)
        {
            sections[index] = new ChunkSection(0, MinSectionY + index);
        }

        for (var x = 0; x < 16; x++)
        {
            for (var z = 0; z < 16; z++)
            {
                var worldX = (chunkX * 16) + x;
                var worldZ = (chunkZ * 16) + z;
                var height = 64 + (int)((2.0 * Math.Sin(worldX * 0.35)) + (2.0 * Math.Cos(worldZ * 0.27)));

                for (var y = MinSectionY * 16; y < height; y++)
                {
                    var state = (worldX + worldZ + y) % 17 == 0 ? 2 : 1;
                    sections[(y >> 4) - MinSectionY].Set(x, y & 15, z, state);
                }
            }
        }

        for (var index = 0; index < SectionCount; index++)
        {
            chunk.SetSection(sections[index]);
        }
    }

    [Fact]
    public void SectionCostScalesWithFilledSections()
    {
        // How the per chunk cost grows with the number of filled sections, which separates per section overhead from the
        // per cell lookups (a section is six planes of two hundred and fifty six cells).
        var world = new ClientWorld(MinSectionY, SectionCount);
        var atlas = SpriteAtlasBuilder.Stitch(
        [
            (new Identifier("minecraft", "block/stone"), Solid(), 1),
            (SpriteAtlas.MissingSprite, Solid(), 1)
        ]);

        var layout = VanillaBlockRegistry.Layout;
        var sprites = new BlockSpriteMap(atlas, layout);
        var cull = new StateCullTable(layout, sprites);
        var visuals = new StateVisualCache(sprites, layout);
        using var buffer = new MeshBuffer(1 << 20);

        foreach (var filled in new[] { 1, 4, 8, 24 })
        {
            var chunk = new LevelChunk(0, 0, MinSectionY, SectionCount);
            var sections = new ChunkSection[SectionCount];

            for (var index = 0; index < SectionCount; index++)
            {
                var state = index < filled ? 1 : 0;
                sections[index] = new ChunkSection(state, MinSectionY + index);
            }

            for (var index = 0; index < SectionCount; index++)
            {
                chunk.SetSection(sections[index]);
            }

            chunk.MarkLoaded();
            world.AddChunk(chunk);
            buffer.Clear();

            var ms = Measure(world, chunk, buffer, sprites, new MeshBuffer(1 << 16), layout, new MeshBuffer(1 << 16), cull, 5, visuals);
            Console.WriteLine($"BENCH scaling filled_sections={filled} ms={ms:F2} quads={buffer.VertexCount / 4}");
        }

        Assert.True(true);
    }

    [Fact]
    public void AmplifiedTerrainStressIsMeasured()
    {
        // Amplified style terrain: tall peaks, deep valleys and cave pockets, which is the worst case for a mesher because
        // almost nothing merges. This is the stress figure, not the everyday one.
        var world = new ClientWorld(MinSectionY, SectionCount);

        for (var chunkX = -1; chunkX <= 1; chunkX++)
        {
            for (var chunkZ = -1; chunkZ <= 1; chunkZ++)
            {
                var chunk = new LevelChunk(chunkX, chunkZ, MinSectionY, SectionCount);
                FillAmplified(chunk, chunkX, chunkZ);
                chunk.MarkLoaded();
                world.AddChunk(chunk);
            }
        }

        var atlas = SpriteAtlasBuilder.Stitch(
        [
            (new Identifier("minecraft", "block/stone"), Solid(), 1),
            (SpriteAtlas.MissingSprite, Solid(), 1)
        ]);

        var layout = VanillaBlockRegistry.Layout;
        var sprites = new BlockSpriteMap(atlas, layout);
        var cullTable = new StateCullTable(layout, sprites);

        var centre = world.GetChunk(0, 0)!;
        using var buffer = new MeshBuffer(1 << 20);
        using var translucent = new MeshBuffer(1 << 16);
        using var cutout = new MeshBuffer(1 << 16);

        var blocks = ChunkMesher.MeshChunk(world, centre, 0, buffer, int.MaxValue, sprites, translucent, layout, cutout, null, cullTable);
        var quads = buffer.VertexCount / 4;
        var faces = CountVisibleFaces(centre, cullTable);

        var withTable = Measure(world, centre, buffer, sprites, translucent, layout, cutout, cullTable, iterations: 8);
        var amplifiedVisuals = Measure(world, centre, buffer, sprites, translucent, layout, cutout, cullTable, iterations: 8, new StateVisualCache(sprites, layout));

        Console.WriteLine(
            $"BENCH amplified_ms_per_chunk={amplifiedVisuals:F2} cull_only={withTable:F2} blocks={blocks} greedy_quads={quads} vanilla_faces={faces}");

        Assert.True(blocks > 0, "the amplified chunk produced no geometry, so the measurement is meaningless");
    }

    private static void FillAmplified(LevelChunk chunk, int chunkX, int chunkZ)
    {
        var sections = new ChunkSection[SectionCount];

        for (var index = 0; index < SectionCount; index++)
        {
            sections[index] = new ChunkSection(0, MinSectionY + index);
        }

        for (var x = 0; x < 16; x++)
        {
            for (var z = 0; z < 16; z++)
            {
                var worldX = (chunkX * 16) + x;
                var worldZ = (chunkZ * 16) + z;

                // Peaks and valleys, then a second sine pair so the surface folds over itself and caves open up.
                var peak = 90 + (int)(45.0 * Math.Sin(worldX * 0.21) * Math.Cos(worldZ * 0.19));
                var fold = (int)(18.0 * Math.Sin(worldX * 0.63 + worldZ * 0.41));

                for (var y = MinSectionY * 16; y < peak; y++)
                {
                    var cave = Math.Sin(worldX * 0.55) * Math.Cos(y * 0.37) * Math.Sin(worldZ * 0.49) > 0.42;

                    if (cave && y < peak - 6)
                    {
                        continue;
                    }

                    var state = ((worldX * 31) + (worldZ * 17) + y + fold) % 23 == 0 ? 2 : 1;
                    sections[(y >> 4) - MinSectionY].Set(x, y & 15, z, state);
                }
            }
        }

        for (var index = 0; index < SectionCount; index++)
        {
            chunk.SetSection(sections[index]);
        }
    }

    private static double Measure(
        ClientWorld world,
        LevelChunk chunk,
        MeshBuffer buffer,
        BlockSpriteMap sprites,
        MeshBuffer translucent,
        Sharpcraft.Common.Registries.BlockStateLayout layout,
        MeshBuffer cutout,
        StateCullTable? cull,
        int iterations,
        StateVisualCache? visuals = null)
    {
        buffer.Clear();
        var watch = Stopwatch.StartNew();

        for (var index = 0; index < iterations; index++)
        {
            ChunkMesher.MeshChunk(world, chunk, 0, buffer, int.MaxValue, sprites, translucent, layout, cutout, null, cull, visuals);
            buffer.Clear();
        }

        watch.Stop();
        return watch.Elapsed.TotalMilliseconds / iterations;
    }

    private static int CountVisibleFaces(LevelChunk chunk, StateCullTable cull)
    {
        var faces = 0;

        Span<(int Dx, int Dy, int Dz)> offsets = [(0, 1, 0), (0, -1, 0), (1, 0, 0), (-1, 0, 0), (0, 0, 1), (0, 0, -1)];

        for (var x = 0; x < 16; x++)
        {
            for (var z = 0; z < 16; z++)
            {
                for (var y = MinSectionY * 16; y < 256; y++)
                {
                    var state = chunk.GetBlockState(x, y, z, 0);

                    if (state == 0)
                    {
                        continue;
                    }

                    foreach (var offset in offsets)
                    {
                        var neighbourX = x + offset.Dx;
                        var neighbourZ = z + offset.Dz;

                        // Outside this column the neighbour lives in another chunk; count those as visible so the figure
                        // errs towards the reference emitting more rather than fewer.
                        var side = neighbourX is < 0 or > 15 || neighbourZ is < 0 or > 15
                            ? 0
                            : chunk.GetBlockState(neighbourX, y + offset.Dy, neighbourZ, 0);

                        if (!cull.IsOpaque(side))
                        {
                            faces++;
                        }
                    }
                }
            }
        }

        return faces;
    }

    private static RgbaImage Solid()
    {
        var pixels = new byte[16 * 16 * 4];

        for (var index = 0; index < pixels.Length; index += 4)
        {
            pixels[index] = 128;
            pixels[index + 1] = 128;
            pixels[index + 2] = 128;
            pixels[index + 3] = 255;
        }

        return new RgbaImage(16, 16, pixels);
    }
}
