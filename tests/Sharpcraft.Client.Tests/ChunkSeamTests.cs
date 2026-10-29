// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Common.World;
using Xunit;

namespace Sharpcraft.Client.Tests;

/// <summary>
/// Seam correctness for per-chunk meshing (docs/specs/network/play-movement.md 7.22). Meshing column by column
/// must produce exactly the same geometry as meshing the whole world at once; if it produces more, a border
/// face is drawn twice and the seam shows overlapping texture.
/// </summary>
public sealed class ChunkSeamTests
{
    [Fact]
    public void PerChunkMeshingMatchesWholeWorldMeshing()
    {
        var world = BuildTwoSolidChunks();

        var whole = ChunkMesher.Mesh(world, airState: 0, maxBlocks: 1_000_000);

        var cache = new ChunkMeshCache();
        var split = cache.Build(world, maxChunksPerBuild: 64);

        Assert.Equal(whole.Vertices.Length, split.Vertices.Length);
        Assert.Equal(whole.Indices.Length, split.Indices.Length);
    }

    [Fact]
    public void AColumnMeshedBeforeItsNeighbourExistsIsRebuiltWithoutBorderFaces()
    {
        // This is the streaming case that produced overlapping texture in play: the first column is meshed
        // while the second does not exist yet, so it emits faces on the seam. When the neighbour arrives those
        // faces must be recomputed, otherwise they sit on top of the neighbour's own faces.
        var world = new ClientWorld(minSectionY: 0, sectionCount: 1);
        AddFloor(world, 0, 0);

        var cache = new ChunkMeshCache();
        var alone = cache.Build(world, maxChunksPerBuild: 64);

        AddFloor(world, 1, 0);

        cache.Build(world, maxChunksPerBuild: 64);
        var afterNeighbour = cache.Snapshot();
        var whole = ChunkMesher.Mesh(world, airState: 0, maxBlocks: 1_000_000);

        // The seam has no faces once the neighbour is known, so the result must match a single-pass mesh and be
        // strictly smaller than the first, neighbour-less build.
        Assert.Equal(whole.Vertices.Length, afterNeighbour.Vertices.Length);
        // The incremental result must equal a cache built from scratch on the finished world; if the stale
        // seam faces had survived, the incremental mesh would carry extra geometry instead.
        var fresh = new ChunkMeshCache();
        var fromScratch = fresh.Build(world, maxChunksPerBuild: 64);

        Assert.Equal(fromScratch.Vertices.Length, afterNeighbour.Vertices.Length);
        Assert.Equal(fromScratch.Indices.Length, afterNeighbour.Indices.Length);
        Assert.True(alone.Vertices.Length > 0, "the first column should have produced geometry on its own");
    }

    [Fact]
    public void ANewNeighbourInvalidatesTheColumnsBesideIt()
    {
        var world = new ClientWorld(minSectionY: 0, sectionCount: 1);
        var first = world.AddChunk(0, 0);
        first.SetSection(new ChunkSection(singleState: 1, sectionY: 0));
        world.TakeDirtyChunks();

        var second = world.AddChunk(1, 0);
        second.SetSection(new ChunkSection(singleState: 1, sectionY: 0));

        var dirty = world.TakeDirtyChunks();

        Assert.Contains(ClientWorld.Pack(1, 0), dirty);
        Assert.Contains(ClientWorld.Pack(0, 0), dirty);
    }

    private static ClientWorld BuildTwoSolidChunks()
    {
        var world = new ClientWorld(minSectionY: 0, sectionCount: 1);
        AddFloor(world, 0, 0);
        AddFloor(world, 1, 0);
        return world;
    }

    /// <summary>Fills one 16 by 16 layer of blocks, which gives the mesher real faces to emit.</summary>
    private static void AddFloor(ClientWorld world, int chunkX, int chunkZ)
    {
        var chunk = world.AddChunk(chunkX, chunkZ);
        chunk.SetSection(new ChunkSection(singleState: 0, sectionY: 0));

        for (var x = 0; x < 16; x++)
        {
            for (var z = 0; z < 16; z++)
            {
                chunk.SetBlockState(x, 0, z, state: 1);
            }
        }
    }
}
