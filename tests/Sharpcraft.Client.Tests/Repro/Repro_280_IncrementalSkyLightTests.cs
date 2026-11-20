// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Lighting;
using Sharpcraft.Client.World;
using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Registries;
using Sharpcraft.Common.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Repro;

/// <summary>
/// Issue 280 turned the sky light engine from a full recompute into an incremental one. These tests are the equivalence
/// proof: the brute force recompute is kept as <c>RecomputeAll</c>, and every random layout and change has to end up with
/// the same light cell for cell in both.
/// </summary>
public sealed class Repro_280_IncrementalSkyLightTests
{
    private const int GroundY = 7;
    private const int AirY = 8;
    private const int MaxY = 31;

    /// <summary>Builds a scene whose blocks come from a seeded generator, so a failure is reproducible.</summary>
    private static (ClientWorld World, int Stone, int Air, int Leaves, int Glass) Scene(int seed)
    {
        var layout = VanillaBlockRegistry.Layout;
        layout.TryGetBaseId(Identifier.Parse("minecraft:stone"), out var stone);
        layout.TryGetBaseId(Identifier.Parse("minecraft:air"), out var air);
        layout.TryGetBaseId(Identifier.Parse("minecraft:oak_leaves"), out var leaves);
        layout.TryGetBaseId(Identifier.Parse("minecraft:glass"), out var glass);

        var world = new ClientWorld(minSectionY: 0, sectionCount: 2);
        var random = new Random(seed);

        for (var chunkX = -1; chunkX <= 0; chunkX++)
        {
            for (var chunkZ = -1; chunkZ <= 0; chunkZ++)
            {
                var chunk = world.AddChunk(chunkX, chunkZ);
                chunk.SetSection(new ChunkSection(singleState: air, sectionY: 0));
                chunk.SetSection(new ChunkSection(singleState: air, sectionY: 1));

                for (var localX = 0; localX < 16; localX++)
                {
                    for (var localZ = 0; localZ < 16; localZ++)
                    {
                        // A solid ground, then a random mix of walls, leaves and glass above it, so both the column
                        // sources and the sideways paths differ from column to column.
                        chunk.SetBlockState(localX, GroundY, localZ, stone);
                        chunk.SetBlockState(localX, AirY, localZ, air);

                        if (random.NextDouble() < 0.45)
                        {
                            var height = random.Next(1, 5);
                            for (var i = 0; i < height; i++)
                            {
                                var block = random.Next(3) switch
                                {
                                    0 => stone,
                                    1 => leaves,
                                    _ => glass,
                                };
                                chunk.SetBlockState(localX, AirY + i, localZ, block);
                            }
                        }
                    }
                }

                chunk.MarkLoaded();
            }
        }

        return (world, stone, air, leaves, glass);
    }

    private static int[] Snapshot(ClientWorld world)
    {
        var values = new List<int>();
        foreach (var chunk in world.GetChunks().OrderBy(c => c.ChunkX).ThenBy(c => c.ChunkZ))
        {
            for (var localX = 0; localX < 16; localX++)
            {
                for (var localZ = 0; localZ < 16; localZ++)
                {
                    for (var y = 0; y <= MaxY; y++)
                    {
                        values.Add(chunk.Light.GetSkyLight(localX, y, localZ));
                    }
                }
            }
        }

        return [.. values];
    }

    private static void AssertSame(ClientWorld expected, ClientWorld actual, string context)
    {
        var left = Snapshot(expected);
        var right = Snapshot(actual);

        // Both snapshots walk the chunks in the same order, so the arrays line up index by index.
        for (var i = 0; i < left.Length; i++)
        {
            if (left[i] != right[i])
            {
                Assert.Fail($"{context}: cell {i} differs, brute force {left[i]} incremental {right[i]}");
            }
        }
    }

    /// <summary>
    /// Fifty random layouts with one random change each: the incremental repair must land on the exact same light field
    /// as re-running the whole brute force recompute from scratch.
    /// </summary>
    [Fact]
    public void TheIncrementalRepairMatchesTheBruteForceRecompute()
    {
        for (var seed = 1; seed <= 50; seed++)
        {
            var (incrementalWorld, stone, air, _, glass) = Scene(seed);
            var (naiveWorld, _, _, _, _) = Scene(seed);

            incrementalWorld.SkyLight.RecomputeAll();
            naiveWorld.SkyLight.RecomputeAll();
            AssertSame(naiveWorld, incrementalWorld, $"seed {seed} before the change");

            var random = new Random(seed * 7919);
            var x = random.Next(0, 16);
            var z = random.Next(0, 16);
            var y = random.Next(AirY, AirY + 6);
            var state = random.Next(3) switch
            {
                0 => air,
                1 => stone,
                _ => glass,
            };

            incrementalWorld.SetBlockState(x, y, z, state);
            naiveWorld.SetBlockState(x, y, z, state);

            // The naive world is repaired by the full recompute, the incremental world by CheckBlock alone.
            naiveWorld.SkyLight.RecomputeAll();

            AssertSame(naiveWorld, incrementalWorld, $"seed {seed} after changing ({x},{y},{z}) to {state}");
        }
    }

    /// <summary>
    /// A height cache that is not invalidated shows up here: removing a wall lights columns whose blocks did not change.
    /// </summary>
    [Fact]
    public void AChangeLightsColumnsThatWereNotTouched()
    {
        var (world, stone, air, _, _) = Scene(seed: 3);
        world.SkyLight.RecomputeAll();

        for (var z = 0; z < 16; z++)
        {
            for (var i = 0; i < 5; i++)
            {
                world.SetBlockState(8, AirY + i, z, stone);
            }
        }

        world.SetBlockState(8, AirY + 4, 8, air);

        // Column x = 9 did not change, but light has to reach it through the gap.
        var lit = world.GetChunk(0, 0)!.Light.GetSkyLight(9, AirY + 4, 8);
        Assert.True(lit > 0, "the neighbour column stayed dark, so the cache or the propagation is wrong");
    }

    /// <summary>
    /// A chunk that arrives again can have different blocks, so its cached heights must not survive; the observable check
    /// is that a change inside the reloaded chunk still lands on the same light field as the brute force recompute.
    /// </summary>
    [Fact]
    public void AReloadedChunkIsRelitLikeTheBruteForce()
    {
        var (incrementalWorld, stone, air, _, glass) = Scene(seed: 5);
        var (naiveWorld, _, _, _, _) = Scene(seed: 5);

        incrementalWorld.SkyLight.RecomputeAll();
        naiveWorld.SkyLight.RecomputeAll();

        // The chunk arrives again with a pillar in the column at (4,4). Both worlds get the same blocks, but the
        // incremental world edits them behind the engine's back, which is exactly the state a reload leaves behind, so
        // the cache has to be dropped (ClientWorld.AddChunk does that through ForgetChunk).
        foreach (var world in new[] { incrementalWorld, naiveWorld })
        {
            var chunk = world.AddChunk(0, 0);
            chunk.SetSection(new ChunkSection(singleState: air, sectionY: 0));
            chunk.SetSection(new ChunkSection(singleState: air, sectionY: 1));
            for (var localX = 0; localX < 16; localX++)
            {
                for (var localZ = 0; localZ < 16; localZ++)
                {
                    chunk.SetBlockState(localX, GroundY, localZ, stone);
                }
            }

            for (var y = AirY; y <= AirY + 6; y++)
            {
                chunk.SetBlockState(4, y, 4, stone);
            }

            chunk.MarkLoaded();
        }

        // The reloaded column is lit by the brute force pass on the naive world and by the engine's own pass on the
        // incremental one, so both start from the same light.
        incrementalWorld.SkyLight.ForgetChunk(0, 0);
        incrementalWorld.SkyLight.RecomputeAll();
        naiveWorld.SkyLight.RecomputeAll();
        AssertSame(naiveWorld, incrementalWorld, "after reloading the chunk");

        // Now one block goes, and the incremental engine has to agree with a fresh recompute.
        incrementalWorld.SetBlockState(4, AirY + 6, 4, glass);
        naiveWorld.SetBlockState(4, AirY + 6, 4, glass);
        naiveWorld.SkyLight.RecomputeAll();

        AssertSame(naiveWorld, incrementalWorld, "after changing a block in the reloaded chunk");
    }
}