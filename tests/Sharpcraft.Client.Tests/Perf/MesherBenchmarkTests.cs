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
        var watch = Stopwatch.StartNew();

        for (var index = 0; index < iterations; index++)
        {
            ChunkMesher.MeshChunk(world, centre, 0, buffer, int.MaxValue, sprites, translucent, layout, cutout, null, cullTable);
        }

        watch.Stop();
        var perChunk = watch.Elapsed.TotalMilliseconds / iterations;

        Console.WriteLine($"BENCH mesh_ms_per_chunk={perChunk:F2} iterations={iterations} (with state cull table)");

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
