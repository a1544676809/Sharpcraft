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
public sealed class ChunkMeshCache : IDisposable
{
    private readonly Dictionary<long, MeshData> _meshes = [];
    private readonly Queue<long> _queue = new();
    private readonly HashSet<long> _queued = [];
    private readonly List<(long Key, MeshData Mesh)> _finished = [];

    private readonly object _gate = new();
    private Thread? _worker;
    private volatile bool _stop;
    private ClientWorld? _world;
    private BlockSpriteMap? _sprites;
    private int _maxBlocks = 2_000_000;

    /// <summary>Chunks re-meshed by the last synchronous <see cref="Build"/> call.</summary>
    public int LastRemeshedChunks { get; private set; }

    /// <summary>Chunks in the cache after the last build.</summary>
    public int LastTotalChunks => _meshes.Count;

    /// <summary>Chunks still queued or being meshed by the worker.</summary>
    public int PendingChunks
    {
        get
        {
            lock (_gate)
            {
                return _queued.Count;
            }
        }
    }

    /// <summary>Milliseconds the last synchronous <see cref="Build"/> call spent meshing.</summary>
    public double LastBuildMilliseconds { get; private set; }

    /// <summary>Drops every cached mesh; the next build re-meshes everything.</summary>
    public void Clear()
    {
        _meshes.Clear();

        lock (_gate)
        {
            _queue.Clear();
            _queued.Clear();
            _finished.Clear();
        }
    }

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

    /// <summary>Starts the background mesher and queues every chunk that has no mesh yet.</summary>
    /// <param name="world">World to mesh; it must guard its own collections against concurrent reads.</param>
    /// <param name="sprites">Atlas mapping.</param>
    /// <param name="maxBlocks">Safety cap.</param>
    public void StartBackground(ClientWorld world, BlockSpriteMap? sprites = null, int maxBlocks = 2_000_000)
    {
        ArgumentNullException.ThrowIfNull(world);

        _world = world;
        _sprites = sprites;
        _maxBlocks = maxBlocks;
        EnqueueMissing(world);

        _worker = new Thread(WorkerLoop)
        {
            IsBackground = true,
            Name = "chunk-mesher"
        };
        _worker.Start();
    }

    /// <summary>Queues a chunk for re-meshing on the worker.</summary>
    /// <param name="chunkKey">Packed chunk key.</param>
    public void Invalidate(long chunkKey)
    {
        // The stale mesh is kept on purpose: dropping it would leave a hole until the worker delivers the
        // replacement, which shows as flicker (and once produced an empty mesh that could not be uploaded).
        lock (_gate)
        {
            if (_queued.Add(chunkKey))
            {
                _queue.Enqueue(chunkKey);
            }
        }
    }

    /// <summary>Picks up whatever the worker finished; true when the cached geometry changed.</summary>
    public bool Drain()
    {
        List<(long Key, MeshData Mesh)> ready;

        lock (_gate)
        {
            if (_finished.Count == 0)
            {
                return false;
            }

            ready = [.. _finished];
            _finished.Clear();
        }

        foreach (var (key, mesh) in ready)
        {
            _meshes[key] = mesh;
        }

        return true;
    }

    /// <summary>Combines the meshes currently cached; cheap, no meshing.</summary>
    public MeshData Snapshot() => Combine();

    /// <inheritdoc />
    public void Dispose()
    {
        _stop = true;

        lock (_gate)
        {
            Monitor.PulseAll(_gate);
        }

        _worker?.Join(2000);
        _worker = null;
    }

    /// <summary>Queues every loaded chunk that has no mesh yet; called as terrain streams in.</summary>
    /// <param name="world">World to scan.</param>
    public void QueueMissing(ClientWorld world) => EnqueueMissing(world);

    private void EnqueueMissing(ClientWorld world)
    {
        lock (_gate)
        {
            foreach (var chunk in world.GetChunks())
            {
                var key = ClientWorld.Pack(chunk.ChunkX, chunk.ChunkZ);
                if (_meshes.ContainsKey(key) || !_queued.Add(key))
                {
                    continue;
                }

                _queue.Enqueue(key);
            }
        }
    }

    private void WorkerLoop()
    {
        while (!_stop)
        {
            long key;

            lock (_gate)
            {
                while (_queue.Count == 0 && !_stop)
                {
                    Monitor.Wait(_gate, 100);
                }

                if (_stop)
                {
                    return;
                }

                key = _queue.Dequeue();
            }

            var world = _world;
            if (world is null)
            {
                return;
            }

            using var buffer = new MeshBuffer(512);

            var chunk = FindChunk(world, key);
            if (chunk is null)
            {
                lock (_gate)
                {
                    _queued.Remove(key);
                }

                continue;
            }

            // No world lock: meshing one column takes tens of milliseconds, and holding a lock for that long
            // makes every block update and every incoming chunk wait for it, which showed up as multi-second
            // frame stalls. The chunk table is concurrent and block writes are word-sized, so a mesh may see a
            // slightly stale neighbour for one rebuild and be corrected on the next dirty mark.
            ChunkMesher.MeshChunk(world, chunk, airState: 0, buffer, _maxBlocks, _sprites);

            var mesh = buffer.ToMeshData(0);

            lock (_gate)
            {
                _queued.Remove(key);
                _finished.Add((key, mesh));
            }
        }
    }

    private static LevelChunk? FindChunk(ClientWorld world, long key)
    {
        foreach (var chunk in world.GetChunks())
        {
            if (ClientWorld.Pack(chunk.ChunkX, chunk.ChunkZ) == key)
            {
                return chunk;
            }
        }

        return null;
    }

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
