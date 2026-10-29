// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Collections.Concurrent;
using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Common.Network;
using Sharpcraft.Common.World;

namespace Sharpcraft.Client.World;

/// <summary>
/// Runs chunk parsing and chunk meshing on a pool of background workers
/// (docs/specs/network/play-movement.md 7.24).
/// </summary>
/// <remarks>
/// Two jobs dominate world loading: decoding the chunk packet and building its mesh. Both are pure work over
/// data the game thread has already accepted, so both belong off it. Parsing is preferred over meshing when
/// both are queued, because a decoded chunk is what makes the world usable.
/// <para>
/// The worker count adapts to the machine: one fewer than the logical processor count, clamped so a two-core
/// laptop still gets a worker and a many-core machine does not spend all its threads here. The count is a
/// constructor parameter so it can become a setting without touching callers.
/// </para>
/// <para>
/// Nothing here writes to the world. Parsed chunks and finished meshes are handed back to the game thread,
/// which owns every mutation.
/// </para>
/// </remarks>
public sealed class ChunkWorkerPool : IDisposable
{
    private readonly ClientWorld _world;
    private readonly ConcurrentQueue<byte[]> _parseQueue = new();
    private readonly ConcurrentQueue<long> _meshQueue = new();
    private readonly ConcurrentQueue<ParsedChunk> _parsed = new();
    private readonly ConcurrentQueue<(long Key, MeshData Mesh)> _meshes = new();

    private readonly SemaphoreSlim _signal = new(0);
    private readonly Thread[] _workers;
    private readonly int _maxQueuedParses;
    private readonly int _maxQueuedMeshes;

    private volatile BlockSpriteMap? _sprites;
    private volatile bool _stop;
    private int _parsePending;
    private int _meshPending;

    /// <summary>Creates a pool sized for this machine.</summary>
    /// <param name="world">World the workers read; never written from here.</param>
    /// <param name="sprites">Atlas mapping used for meshing.</param>
    /// <param name="workerCount">Worker count, or null for the adaptive default.</param>
    /// <param name="maxQueuedParses">Parse queue cap; beyond it the caller parses inline.</param>
    /// <param name="maxQueuedMeshes">Mesh queue cap; beyond it meshing is deferred.</param>
    public ChunkWorkerPool(
        ClientWorld world,
        BlockSpriteMap? sprites = null,
        int? workerCount = null,
        int maxQueuedParses = 64,
        int maxQueuedMeshes = 1024)
    {
        ArgumentNullException.ThrowIfNull(world);

        _world = world;
        _sprites = sprites;
        _maxQueuedParses = maxQueuedParses;
        _maxQueuedMeshes = maxQueuedMeshes;
        WorkerCount = Math.Clamp(workerCount ?? DefaultWorkerCount(), 1, 16);

        _workers = new Thread[WorkerCount];
        for (var index = 0; index < _workers.Length; index++)
        {
            _workers[index] = new Thread(WorkerLoop)
            {
                IsBackground = true,
                Name = $"chunk-worker-{index}"
            };
            _workers[index].Start();
        }
    }

    /// <summary>Workers actually started.</summary>
    public int WorkerCount { get; }

    /// <summary>Parses waiting for a worker.</summary>
    public int ParsesPending => Volatile.Read(ref _parsePending);

    /// <summary>Meshes waiting for a worker.</summary>
    public int MeshesPending => Volatile.Read(ref _meshPending);

    /// <summary>Chunks successfully decoded since creation.</summary>
    public int ParsedOk { get; private set; }

    /// <summary>Chunk packets that failed to decode; each one is a hole that never fills.</summary>
    public int ParsedFailed { get; private set; }

    /// <summary>Meshes produced since creation.</summary>
    public int MeshesBuilt { get; private set; }

    /// <summary>Meshes that came out empty; expected above the surface, suspicious below it.</summary>
    public int MeshesEmpty { get; private set; }

    /// <summary>Mesh jobs whose chunk was not in the world, usually a border key for an unloaded column.</summary>
    public int MeshesMissingChunk { get; private set; }

    /// <summary>Chunks decoded and waiting for the game thread to install them.</summary>
    public int ParsedReady => _parsed.Count;

    /// <summary>Meshes finished and waiting for the game thread to upload them.</summary>
    public int MeshesReady => _meshes.Count;

    /// <summary>
    /// The adaptive default: every logical processor except one, so the game thread keeps a core, clamped to a
    /// sane range.
    /// </summary>
    public static int DefaultWorkerCount() => Math.Clamp(Environment.ProcessorCount - 1, 1, 8);

    /// <summary>Updates the atlas mapping used by later meshes.</summary>
    /// <param name="sprites">New mapping, or null for untextured meshing.</param>
    public void SetSprites(BlockSpriteMap? sprites) => _sprites = sprites;

    /// <summary>Queues a chunk packet body for decoding.</summary>
    /// <param name="body">Packet body, including the packet id; the pool takes ownership.</param>
    /// <returns>False when the queue is full, in which case the caller should parse it itself.</returns>
    public bool TryEnqueueParse(byte[] body)
    {
        ArgumentNullException.ThrowIfNull(body);

        if (Volatile.Read(ref _parsePending) >= _maxQueuedParses)
        {
            return false;
        }

        Interlocked.Increment(ref _parsePending);
        _parseQueue.Enqueue(body);
        _signal.Release();
        return true;
    }

    /// <summary>Queues a chunk for meshing.</summary>
    /// <param name="chunkKey">Packed chunk key.</param>
    /// <returns>False when the mesh queue is full.</returns>
    public bool TryEnqueueMesh(long chunkKey)
    {
        if (Volatile.Read(ref _meshPending) >= _maxQueuedMeshes)
        {
            return false;
        }

        Interlocked.Increment(ref _meshPending);
        _meshQueue.Enqueue(chunkKey);
        _signal.Release();
        return true;
    }

    /// <summary>Takes one decoded chunk; the caller installs it into the world.</summary>
    /// <param name="parsed">Decoded chunk when available.</param>
    public bool TryTakeParsed(out ParsedChunk parsed) => _parsed.TryDequeue(out parsed!);

    /// <summary>Takes one finished mesh.</summary>
    /// <param name="chunkKey">Packed chunk key.</param>
    /// <param name="mesh">Finished mesh.</param>
    public bool TryTakeMesh(out long chunkKey, out MeshData mesh)
    {
        if (_meshes.TryDequeue(out var item))
        {
            chunkKey = item.Key;
            mesh = item.Mesh;
            return true;
        }

        chunkKey = 0;
        mesh = MeshData.Empty;
        return false;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _stop = true;
        _signal.Release(_workers.Length);

        foreach (var worker in _workers)
        {
            worker.Join(2000);
        }

        _signal.Dispose();
    }

    private void WorkerLoop()
    {
        while (!_stop)
        {
            _signal.Wait(100);

            if (_stop)
            {
                return;
            }

            if (_parseQueue.TryDequeue(out var body))
            {
                Interlocked.Decrement(ref _parsePending);
                Parse(body);
                continue;
            }

            if (_meshQueue.TryDequeue(out var key))
            {
                Interlocked.Decrement(ref _meshPending);
                Mesh(key);
            }
        }
    }

    private void Parse(byte[] body)
    {
        var status = ChunkPacketParser.TryRead(
            body,
            _world.SectionCount,
            _world.MinSectionY,
            airState: 0,
            out var parsed);

        if (status == DecodeStatus.Success && parsed is not null)
        {
            ParsedOk++;
            _parsed.Enqueue(parsed);
        }
        else
        {
            // Worth counting rather than ignoring: a chunk that never decodes leaves a permanent hole in the
            // world, which is exactly what a missing column looks like in game
            // (docs/specs/network/play-movement.md 7.28). The first few are described in the log so the shape
            // of the failing packet can be identified.
            ParsedFailed++;

            if (ParsedFailed <= 3)
            {
                // Dump the packet so the desync can be examined offline instead of by guesswork.
                try
                {
                    var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"chunkfail_{ParsedFailed}.bin");
                    System.IO.File.WriteAllBytes(path, body);
                    Console.WriteLine($"chunk packet dumped to {path}");
                }
                catch (System.IO.IOException)
                {
                    // Diagnostics only; never let this break streaming.
                }
            }

            if (ParsedFailed <= 5)
            {
                var reader = new PayloadReader(body);
                reader.TryReadVarInt(out _);
                reader.TryReadInt(out var chunkX);
                reader.TryReadInt(out var chunkZ);

                Console.WriteLine(
                    $"chunk packet failed: status={status} bytes={body.Length} at ({chunkX},{chunkZ}) reason={ChunkPacketParser.LastFailure} {PalettedContainerCodec.LastFailure} {LightDataCodec.LastFailure}");
            }
        }
    }

    private void Mesh(long key)
    {
        var chunk = FindChunk(key);
        if (chunk is null)
        {
            MeshesMissingChunk++;
            return;
        }

        using var buffer = new MeshBuffer(512);
        ChunkMesher.MeshChunk(_world, chunk, airState: 0, buffer, 2_000_000, _sprites);
        var mesh = buffer.ToMeshData(0);

        MeshesBuilt++;
        if (mesh.Vertices.Length == 0)
        {
            MeshesEmpty++;
        }

        _meshes.Enqueue((key, mesh));
    }

    private LevelChunk? FindChunk(long key)
    {
        var chunkX = (int)(key >> 32);
        var chunkZ = (int)(key & 0xFFFFFFFF);
        return _world.GetChunk(chunkX, chunkZ);
    }
}
