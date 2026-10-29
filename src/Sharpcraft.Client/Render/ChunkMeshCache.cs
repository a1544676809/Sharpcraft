// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Assets;
using Sharpcraft.Client.World;
using Sharpcraft.Common.World;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Keeps one mesh per chunk so a block change only re-meshes the chunk it touched, and can do that work on a
/// background thread so the render thread never pays for it
/// (docs/specs/network/play-movement.md 7.19 and 7.20).
/// </summary>
/// <remarks>
/// Meshing a full column costs tens of milliseconds, which is a dropped frame when it happens while drawing.
/// The synchronous <see cref="Build"/> path stays for tests and offscreen tools; the windowed path calls
/// <see cref="StartBackground"/>, drains finished meshes once per frame with <see cref="Drain"/>, and combines
/// the cached results itself, which is a cheap copy because greedy meshing keeps the vertex counts small.
/// </remarks>
public sealed class ChunkMeshCache
{
    private readonly Dictionary<long, MeshData> _meshes = [];
    private readonly List<(long Key, MeshData Mesh)> _updates = [];
    private readonly ChunkWorkerPool? _pool;

    /// <summary>Creates a standalone cache that meshes on the calling thread.</summary>
    public ChunkMeshCache()
    {
    }

    /// <summary>Creates a cache fed by a background pool.</summary>
    /// <param name="pool">Pool that produces the meshes.</param>
    public ChunkMeshCache(ChunkWorkerPool pool)
    {
        ArgumentNullException.ThrowIfNull(pool);
        _pool = pool;
    }

    /// <summary>Chunks re-meshed by the last synchronous <see cref="Build"/> call.</summary>
    public int LastRemeshedChunks { get; private set; }

    /// <summary>Chunks in the cache after the last build.</summary>
    public int LastTotalChunks => _meshes.Count;

    /// <summary>Chunks still queued for the pool or waiting to be ingested.</summary>
    public int PendingChunks => _pool is null ? 0 : _pool.MeshesPending + _pool.MeshesReady;

    /// <summary>Milliseconds the last synchronous <see cref="Build"/> call spent meshing.</summary>
    public double LastBuildMilliseconds { get; private set; }

    /// <summary>Drops every cached mesh; the next build re-meshes everything.</summary>
    public void Clear() => _meshes.Clear();

    /// <summary>
    /// Meshes the missing chunks synchronously, at most <paramref name="maxChunksPerBuild"/> of them.
    /// </summary>
    /// <param name="world">World to mesh.</param>
    /// <param name="sprites">Atlas mapping.</param>
    /// <param name="maxBlocks">Safety cap.</param>
    /// <param name="maxChunksPerBuild">Upper bound on chunks meshed in this call.</param>
    /// <param name="maxMillisecondsPerBuild">Wall-clock budget; a chunk is never cut in half.</param>
    public MeshData Build(
        ClientWorld world,
        BlockSpriteMap? sprites = null,
        int maxBlocks = 2_000_000,
        int maxChunksPerBuild = 1,
        double maxMillisecondsPerBuild = 15d)
    {
        ArgumentNullException.ThrowIfNull(world);

        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        var remeshed = 0;

        foreach (var key in world.TakeDirtyChunks())
        {
            _meshes.Remove(key);
        }

        foreach (var chunk in world.GetChunks())
        {
            var key = ClientWorld.Pack(chunk.ChunkX, chunk.ChunkZ);
            if (_meshes.ContainsKey(key))
            {
                continue;
            }

            if (remeshed >= maxChunksPerBuild)
            {
                break;
            }

            if (remeshed > 0 &&
                (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000d
                / System.Diagnostics.Stopwatch.Frequency >= maxMillisecondsPerBuild)
            {
                break;
            }

            using var buffer = new MeshBuffer(512);
            ChunkMesher.MeshChunk(world, chunk, airState: 0, buffer, maxBlocks, sprites);
            _meshes[key] = buffer.ToMeshData(0);
            remeshed++;
        }

        LastRemeshedChunks = remeshed;
        LastBuildMilliseconds = (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000d
            / System.Diagnostics.Stopwatch.Frequency;

        return Combine();
    }

    /// <summary>Queues a chunk for re-meshing on the pool.</summary>
    /// <param name="chunkKey">Packed chunk key.</param>
    public void Invalidate(long chunkKey)
    {
        // The stale mesh is kept on purpose: dropping it would leave a hole until the replacement arrives.
        _pool?.TryEnqueueMesh(chunkKey);
    }

    /// <summary>Queues every loaded chunk that has no mesh yet; called as terrain streams in.</summary>
    /// <param name="world">World to scan.</param>
    public void QueueMissing(ClientWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);

        if (_pool is null)
        {
            return;
        }

        foreach (var chunk in world.GetChunks())
        {
            var key = ClientWorld.Pack(chunk.ChunkX, chunk.ChunkZ);
            if (!_meshes.ContainsKey(key))
            {
                _pool.TryEnqueueMesh(key);
            }
        }
    }

    /// <summary>
    /// Picks up the meshes the pool finished and returns them, so the renderer can upload only those chunks
    /// instead of recomposing the whole world (docs/specs/network/play-movement.md 7.25).
    /// </summary>
    public IReadOnlyList<(long Key, MeshData Mesh)> DrainUpdates()
    {
        _updates.Clear();

        if (_pool is null)
        {
            return _updates;
        }

        while (_pool.TryTakeMesh(out var key, out var mesh))
        {
            _meshes[key] = mesh;
            _updates.Add((key, mesh));
        }

        return _updates;
    }

    /// <summary>Picks up the meshes the pool finished; true when the cached geometry changed.</summary>
    public bool Drain() => DrainUpdates().Count > 0;

    /// <summary>Combines the meshes currently cached; cheap, no meshing.</summary>
    public MeshData Snapshot() => Combine();

    private MeshData Combine()
    {
        var vertexCount = 0;
        var indexCount = 0;
        foreach (var mesh in _meshes.Values)
        {
            vertexCount += mesh.Vertices.Length;
            indexCount += mesh.Indices.Length;
        }

        var vertices = new float[vertexCount];
        var indices = new uint[indexCount];
        var vertexOffset = 0;
        var indexOffset = 0;

        foreach (var mesh in _meshes.Values)
        {
            mesh.Vertices.CopyTo(vertices, vertexOffset);
            var baseVertex = (uint)(vertexOffset / ChunkMesher.FloatsPerVertex);
            for (var index = 0; index < mesh.Indices.Length; index++)
            {
                indices[indexOffset + index] = mesh.Indices[index] + baseVertex;
            }

            vertexOffset += mesh.Vertices.Length;
            indexOffset += mesh.Indices.Length;
        }

        return new MeshData(vertices, indices, _meshes.Count);
    }
}
