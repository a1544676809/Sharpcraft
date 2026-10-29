// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Brew.Wgpu;
using Brew.Wgpu.Native;
using WgpuBuffer = Brew.Wgpu.Buffer;

namespace Sharpcraft.Framework.Rendering;

/// <summary>
/// Holds per-chunk geometry in two large persistent buffers so a changed chunk costs one small upload instead
/// of a full world copy (docs/specs/network/play-movement.md 7.25).
/// </summary>
/// <remarks>
/// The first implementation recomposed and re-uploaded the whole world whenever any chunk arrived, which on
/// amplified terrain meant thirty six megabytes of copying and uploading per frame. Here each chunk owns a
/// slice of a bump-allocated arena and is drawn with its own range, so an arriving chunk writes only its own
/// bytes. Indices stay zero based and the draw supplies the chunk's base vertex, which also means a streamed
/// chunk never has to rewrite anybody else's indices.
/// <para>
/// Buffers grow by doubling, which is the only case that rewrites everything, and it happens a handful of
/// times per session rather than per frame.
/// </para>
/// </remarks>
public sealed class ChunkGeometryArena : IDisposable
{
    private const ulong MinimumBufferBytes = 1 << 20;

    private readonly Device _device;
    private readonly Queue _queue;
    private readonly int _bytesPerVertex;
    private readonly Dictionary<long, Slice> _slices = [];
    private readonly Dictionary<long, GeometryData> _meshes = [];
    private readonly List<WgpuBuffer> _retired = [];

    private WgpuBuffer _vertex;
    private WgpuBuffer _index;
    private ulong _vertexCapacity;
    private ulong _indexCapacity;
    private ulong _vertexUsed;
    private ulong _indexUsed;

    /// <summary>Creates an empty arena.</summary>
    /// <param name="device">Device that owns the buffers.</param>
    /// <param name="bytesPerVertex">Stride of one vertex; defaults to the renderer's layout.</param>
    public ChunkGeometryArena(Device device, int bytesPerVertex = VertexLayout.Stride)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bytesPerVertex);

        _device = device;
        _queue = device.Queue;
        _bytesPerVertex = bytesPerVertex;
        _vertexCapacity = MinimumBufferBytes;
        _indexCapacity = MinimumBufferBytes;
        _vertex = CreateBuffer(vertexCapacity: _vertexCapacity, BufferUsage.Vertex);
        _index = CreateBuffer(vertexCapacity: _indexCapacity, BufferUsage.Index);
    }

    /// <summary>Chunks currently resident.</summary>
    public int ChunkCount => _slices.Count;

    /// <summary>Bytes pushed to the GPU since creation; the measure that shows the fix worked.</summary>
    public long UploadedBytes { get; private set; }

    /// <summary>Times the buffers had to be grown and everything rewritten.</summary>
    public int FullRebuilds { get; private set; }

    /// <summary>Uploads one chunk's geometry, replacing whatever that chunk had.</summary>
    /// <param name="chunkKey">Packed chunk key.</param>
    /// <param name="mesh">Geometry for that chunk.</param>
    public void Apply(long chunkKey, GeometryData mesh)
    {
        if (mesh.Vertices.Length == 0)
        {
            _meshes.Remove(chunkKey);
            _slices.Remove(chunkKey);
            return;
        }

        // Same footprint as last time: rewrite in place, which is the common case for a block edit.
        if (_slices.TryGetValue(chunkKey, out var existing) &&
            _meshes.TryGetValue(chunkKey, out var previous) &&
            previous.Vertices.Length == mesh.Vertices.Length &&
            previous.Indices.Length == mesh.Indices.Length)
        {
            _queue.WriteBuffer(_vertex, existing.VertexOffset, mesh.Vertices.AsSpan());
            _queue.WriteBuffer(_index, existing.IndexOffset, mesh.Indices.AsSpan());
            UploadedBytes += SizeOf(mesh);
            _meshes[chunkKey] = mesh;
            return;
        }

        _meshes[chunkKey] = mesh;

        var neededVertex = (ulong)mesh.Vertices.Length * sizeof(float);
        var neededIndex = (ulong)mesh.Indices.Length * sizeof(uint);

        if (_vertexUsed + neededVertex > _vertexCapacity ||
            _indexUsed + neededIndex > _indexCapacity)
        {
            RebuildAll();
            return;
        }

        var slice = new Slice(_vertexUsed, _indexUsed, (uint)mesh.Indices.Length);
        _queue.WriteBuffer(_vertex, slice.VertexOffset, mesh.Vertices.AsSpan());
        _queue.WriteBuffer(_index, slice.IndexOffset, mesh.Indices.AsSpan());
        UploadedBytes += (long)neededVertex + (long)neededIndex;

        _vertexUsed += neededVertex;
        _indexUsed += neededIndex;
        _slices[chunkKey] = slice;
    }

    /// <summary>Draws every resident chunk, one range each.</summary>
    /// <param name="pass">Render pass to record into.</param>
    public void Draw(RenderPassEncoder pass)
    {
        if (_slices.Count == 0)
        {
            return;
        }

        pass.SetVertexBuffer(0, _vertex, 0, _vertexCapacity);

        foreach (var slice in _slices.Values)
        {
            pass.SetIndexBuffer(_index, WGPUIndexFormat.Uint32, slice.IndexOffset, (ulong)slice.IndexCount * sizeof(uint));
            pass.DrawIndexed(slice.IndexCount, 1, 0, (int)(slice.VertexOffset / (ulong)_bytesPerVertex), 1);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _vertex.Dispose();
        _index.Dispose();

        foreach (var buffer in _retired)
        {
            buffer.Dispose();
        }

        _retired.Clear();
        _slices.Clear();
        _meshes.Clear();
    }

    private WgpuBuffer CreateBuffer(ulong vertexCapacity, BufferUsage usage) =>
        _device.CreateBuffer(new BufferDescriptor
        {
            Size = vertexCapacity,
            Usage = usage | BufferUsage.CopyDst
        });

    private static long SizeOf(GeometryData mesh) =>
        ((long)mesh.Vertices.Length * sizeof(float)) + ((long)mesh.Indices.Length * sizeof(uint));

    /// <summary>
    /// Grows the buffers to fit every resident chunk and rewrites them. Only reached when the arena outgrows
    /// its capacity, so the cost is amortised over many chunks rather than paid per change.
    /// </summary>
    private void RebuildAll()
    {
        FullRebuilds++;

        ulong vertexTotal = 0;
        ulong indexTotal = 0;
        foreach (var mesh in _meshes.Values)
        {
            vertexTotal += (ulong)mesh.Vertices.Length * sizeof(float);
            indexTotal += (ulong)mesh.Indices.Length * sizeof(uint);
        }

        var vertexCapacity = Math.Max(vertexTotal * 2, MinimumBufferBytes);
        var indexCapacity = Math.Max(indexTotal * 2, MinimumBufferBytes);

        _retired.Add(_vertex);
        _retired.Add(_index);

        _vertex = CreateBuffer(vertexCapacity, BufferUsage.Vertex);
        _index = CreateBuffer(indexCapacity, BufferUsage.Index);

        _vertexCapacity = vertexCapacity;
        _indexCapacity = indexCapacity;
        _vertexUsed = 0;
        _indexUsed = 0;
        _slices.Clear();

        foreach (var (key, mesh) in _meshes)
        {
            var slice = new Slice(_vertexUsed, _indexUsed, (uint)mesh.Indices.Length);
            _queue.WriteBuffer(_vertex, slice.VertexOffset, mesh.Vertices.AsSpan());
            _queue.WriteBuffer(_index, slice.IndexOffset, mesh.Indices.AsSpan());
            UploadedBytes += SizeOf(mesh);

            _vertexUsed += (ulong)mesh.Vertices.Length * sizeof(float);
            _indexUsed += (ulong)mesh.Indices.Length * sizeof(uint);
            _slices[key] = slice;
        }
    }

    /// <summary>Where one chunk's geometry lives in the arena.</summary>
    /// <param name="VertexOffset">Byte offset into the vertex buffer.</param>
    /// <param name="IndexOffset">Byte offset into the index buffer.</param>
    /// <param name="IndexCount">Indices this chunk draws.</param>
    private readonly record struct Slice(ulong VertexOffset, ulong IndexOffset, uint IndexCount);
}
