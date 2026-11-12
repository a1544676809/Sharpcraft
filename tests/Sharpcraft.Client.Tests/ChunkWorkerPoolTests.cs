// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Common.World;
using Xunit;

namespace Sharpcraft.Client.Tests;

/// <summary>
/// The background chunk pool (docs/specs/network/play-movement.md 7.24). The tests assert the contract the game
/// thread depends on: the pool size follows the machine, work leaves the calling thread, and results come back
/// through queues the caller drains.
/// </summary>
public sealed class ChunkWorkerPoolTests
{
    [Fact]
    public void TheDefaultWorkerCountFollowsTheMachine()
    {
        var expected = Math.Clamp(Environment.ProcessorCount - 1, 1, 8);

        Assert.Equal(expected, ChunkWorkerPool.DefaultWorkerCount());

        using var pool = new ChunkWorkerPool(new ClientWorld(minSectionY: 0, sectionCount: 1));
        Assert.Equal(expected, pool.WorkerCount);
    }

    [Fact]
    public void AnExplicitWorkerCountIsHonoured()
    {
        using var pool = new ChunkWorkerPool(new ClientWorld(minSectionY: 0, sectionCount: 1), workerCount: 3);

        Assert.Equal(3, pool.WorkerCount);
    }

    [Fact]
    public void MeshingRunsOffTheCallingThreadAndComesBack()
    {
        var world = new ClientWorld(minSectionY: 0, sectionCount: 1);
        var chunk = world.AddChunk(0, 0);
        chunk.SetSection(new ChunkSection(singleState: 0, sectionY: 0));

        for (var x = 0; x < 16; x++)
        {
            for (var z = 0; z < 16; z++)
            {
                chunk.SetBlockState(x, 0, z, state: 1);
            }
        }

        using var pool = new ChunkWorkerPool(world, workerCount: 2);
        Assert.True(pool.TryEnqueueMesh(ClientWorld.Pack(0, 0)));

        // The result arrives asynchronously, so the test waits for it rather than assuming a timing.
        var mesh = WaitForMesh(pool, out var key);

        Assert.NotNull(mesh);
        Assert.Equal(ClientWorld.Pack(0, 0), key);
        Assert.True(mesh!.Vertices.Length > 0);
    }

    [Fact]
    public void AMissingChunkProducesNothing()
    {
        var world = new ClientWorld(minSectionY: 0, sectionCount: 1);

        using var pool = new ChunkWorkerPool(world, workerCount: 1);
        Assert.True(pool.TryEnqueueMesh(ClientWorld.Pack(5, 5)));

        Assert.Null(WaitForMesh(pool, out _));
    }

    [Fact]
    public void TheCacheIngestsWhatThePoolProduced()
    {
        var world = new ClientWorld(minSectionY: 0, sectionCount: 1);
        var chunk = world.AddChunk(0, 0);
        chunk.SetSection(new ChunkSection(singleState: 0, sectionY: 0));
        chunk.SetBlockState(8, 0, 8, state: 1);

        using var pool = new ChunkWorkerPool(world, workerCount: 2);
        var cache = new ChunkMeshCache(pool);
        cache.QueueMissing(world);

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            cache.Drain(world);
            if (cache.Snapshot().Vertices.Length > 0)
            {
                break;
            }

            Thread.Sleep(5);
        }

        Assert.True(cache.Snapshot().Vertices.Length > 0);
        Assert.Equal(1, cache.LastTotalChunks);
    }

    private static MeshData? WaitForMesh(ChunkWorkerPool pool, out long key, double seconds = 1.5)
    {
        var deadline = DateTime.UtcNow.AddSeconds(seconds);
        while (DateTime.UtcNow < deadline)
        {
            if (pool.TryTakeMesh(out key, out var mesh, out _, out _))
            {
                return mesh;
            }

            Thread.Sleep(5);
        }

        key = 0;
        return null;
    }

    [Fact]
    public void PruningReleasesTheMeshesOfChunksThatLeft()
    {
        // A measured session held 1281 cached meshes against 637 loaded chunks and the dictionary retained 225 MB
        // more geometry than before, because a mesh that finishes after its chunk is gone is never reported by the
        // removal path that already ran.
        var world = new ClientWorld(minSectionY: 0, sectionCount: 1);
        var chunk = world.AddChunk(0, 0);
        chunk.SetSection(new ChunkSection(singleState: 0, sectionY: 0));
        chunk.SetBlockState(8, 0, 8, state: 1);

        using var pool = new ChunkWorkerPool(world, workerCount: 2);
        var cache = new ChunkMeshCache(pool);
        cache.QueueMissing(world);

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline && cache.LastTotalChunks == 0)
        {
            cache.Drain(world);
            Thread.Sleep(5);
        }

        Assert.Equal(1, cache.LastTotalChunks);

        // Drop the chunk without telling the cache, which is what a late arrival looks like from here.
        Assert.NotEmpty(world.RemoveChunksOutside(1000, 1000, radiusChunks: 0));

        Assert.Equal([ClientWorld.Pack(0, 0)], cache.PruneTo(world));
        Assert.Equal(0, cache.LastTotalChunks);
        Assert.Empty(cache.PruneTo(world));
    }
}
