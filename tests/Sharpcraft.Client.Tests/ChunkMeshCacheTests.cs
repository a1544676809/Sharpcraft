// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Common.World;
using Xunit;

namespace Sharpcraft.Client.Tests;

/// <summary>
/// Incremental re-meshing (docs/specs/network/play-movement.md 7.19). The point of the cache is that a block
/// change costs one chunk, not the whole world, so the tests assert the re-mesh count rather than the pixels.
/// </summary>
public sealed class ChunkMeshCacheTests
{
    [Fact]
    public void AChangeOnAChunkBorderDirtiesBothColumns()
    {
        var world = new ClientWorld(minSectionY: 0, sectionCount: 1);
        var chunk = world.AddChunk(0, 0);
        chunk.SetSection(new ChunkSection(singleState: 0, sectionY: 0));

        // x = 15 is the seam with the chunk to the east.
        world.SetBlockState(15, 8, 8, state: 1);

        var dirty = world.TakeDirtyChunks();

        Assert.Contains(ClientWorld.Pack(0, 0), dirty);
        Assert.Contains(ClientWorld.Pack(1, 0), dirty);
    }

    [Fact]
    public void AChangeInsideAChunkDirtiesOnlyThatChunk()
    {
        var world = new ClientWorld(minSectionY: 0, sectionCount: 1);
        var chunk = world.AddChunk(0, 0);
        chunk.SetSection(new ChunkSection(singleState: 0, sectionY: 0));

        // Adding the chunk already marked it, so start from a clean slate before the block change.
        world.TakeDirtyChunks();
        world.SetBlockState(8, 8, 8, state: 1);

        var dirty = world.TakeDirtyChunks();

        Assert.Equal([ClientWorld.Pack(0, 0)], dirty);
    }

    [Fact]
    public void OnlyTheDirtyChunkIsRemeshed()
    {
        var world = new ClientWorld(minSectionY: 0, sectionCount: 1);
        var first = world.AddChunk(0, 0);
        first.SetSection(new ChunkSection(singleState: 1, sectionY: 0));
        var second = world.AddChunk(1, 0);
        second.SetSection(new ChunkSection(singleState: 2, sectionY: 0));

        var cache = new ChunkMeshCache();
        cache.Build(world, maxChunksPerBuild: 64);
        Assert.Equal(2, cache.LastRemeshedChunks);

        // Nothing changed: the next build must not touch the mesher at all.
        cache.Build(world, maxChunksPerBuild: 64);
        Assert.Equal(0, cache.LastRemeshedChunks);

        // One interior block changes, so exactly one chunk is re-meshed.
        world.SetBlockState(8, 8, 8, state: 3);
        cache.Build(world, maxChunksPerBuild: 64);
        Assert.Equal(1, cache.LastRemeshedChunks);
        Assert.Equal(2, cache.LastTotalChunks);
    }

    [Fact]
    public void TheCombinedMeshKeepsIndicesInsideTheirOwnVertices()
    {
        var world = new ClientWorld(minSectionY: 0, sectionCount: 1);
        var first = world.AddChunk(0, 0);
        first.SetSection(new ChunkSection(singleState: 1, sectionY: 0));
        var second = world.AddChunk(1, 0);
        second.SetSection(new ChunkSection(singleState: 2, sectionY: 0));

        var cache = new ChunkMeshCache();
        var mesh = cache.Build(world, maxChunksPerBuild: 64);

        var vertexCount = mesh.Vertices.Length / ChunkMesher.FloatsPerVertex;
        Assert.All(mesh.Indices, index => Assert.InRange(index, 0u, (uint)vertexCount - 1));
    }
}
