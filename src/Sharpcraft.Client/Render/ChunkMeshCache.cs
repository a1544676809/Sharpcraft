// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Assets;
using Sharpcraft.Client.World;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Keeps one mesh per chunk so a block change only re-meshes the chunk it touched
/// (docs/specs/network/play-movement.md 7.19).
/// </summary>
/// <remarks>
/// Rebuilding the whole world costs a full scan over every loaded block, which is wasteful when a single
/// block changed. The vertex data itself is tiny because greedy meshing merges faces, so concatenating the
/// cached chunk meshes afterwards is cheap; the saving is in skipping the scan.
/// </remarks>
public sealed class ChunkMeshCache
{
    private readonly Dictionary<long, MeshData> _meshes = [];

    /// <summary>Chunks re-meshed by the last <see cref="Build"/> call.</summary>
    public int LastRemeshedChunks { get; private set; }

    /// <summary>Chunks in the cache after the last build.</summary>
    public int LastTotalChunks => _meshes.Count;

    /// <summary>Milliseconds the last <see cref="Build"/> call spent meshing.</summary>
    public double LastBuildMilliseconds { get; private set; }

    /// <summary>Drops everything; the next build re-meshes the whole world.</summary>
    public void Clear() => _meshes.Clear();

    /// <summary>Forgets a chunk so the next build re-meshes it.</summary>
    /// <param name="chunkKey">Packed chunk key from <see cref="ClientWorld.Pack"/>.</param>
    public void Forget(long chunkKey) => _meshes.Remove(chunkKey);

    /// <summary>
    /// Builds the combined mesh, re-meshing only the chunks the world reports as dirty.
    /// </summary>
    /// <param name="world">World to mesh.</param>
    /// <param name="sprites">Atlas mapping.</param>
    /// <param name="maxBlocks">Safety cap.</param>
    public MeshData Build(ClientWorld world, BlockSpriteMap? sprites = null, int maxBlocks = 2_000_000)
    {
        ArgumentNullException.ThrowIfNull(world);

        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        var dirty = world.TakeDirtyChunks();
        var remeshed = 0;

        foreach (var key in dirty)
        {
            _meshes.Remove(key);
        }

        // Every loaded chunk that is missing from the cache gets meshed, which covers first build and streaming.
        foreach (var chunk in world.GetChunks())
        {
            var key = ClientWorld.Pack(chunk.ChunkX, chunk.ChunkZ);
            if (_meshes.ContainsKey(key))
            {
                continue;
            }

            using var buffer = new MeshBuffer(512);
            ChunkMesher.MeshChunk(world, chunk, airState: 0, buffer, maxBlocks, sprites);
            _meshes[key] = buffer.ToMeshData(0);
            remeshed++;
        }

        LastRemeshedChunks = remeshed;
        LastBuildMilliseconds = (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000d / System.Diagnostics.Stopwatch.Frequency;

        return Combine();
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
