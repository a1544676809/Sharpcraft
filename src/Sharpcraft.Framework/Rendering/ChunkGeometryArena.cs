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

    /// <summary>
    /// Starting guess per chunk when the view distance is known. Deliberately generous: over-reserving costs
    /// memory once, while under-reserving costs a full rewrite of every resident chunk, which is a frame spike.
    /// </summary>
    private const ulong AssumedBytesPerChunk = 96 * 1024;

    /// <summary>
    /// Ceiling for one buffer. The device rejects anything above two hundred and fifty six megabytes, and amplified
    /// terrain needs more than that for a full view, so the arena stops at half the limit and simply skips chunks that no
    /// longer fit. Chunks are meshed nearest first, so what gets skipped is the far edge rather than the ground underfoot.
    /// </summary>
    /// <remarks>Paging several buffers would remove the ceiling; that is the planned replacement for this stop gap.</remarks>
    private const ulong MaxBufferBytes = 1024UL << 20;

    private readonly Device _device;
    private readonly Queue _queue;
    private readonly int _bytesPerVertex;
    private readonly int _expectedChunks;
    private bool _presized;
    private readonly Dictionary<long, Slice> _slices = [];
    /// <summary>Bytes of one DrawIndexedIndirect argument record.</summary>
    private const int DrawArgBytes = 20;

    /// <summary>Draw slots the indirect buffer is created with, one per resident chunk.</summary>
    private const int MaxDrawSlots = 8192;

    private readonly Dictionary<long, GeometryData> _meshes = [];

    /// <summary>
    /// One indirect draw record per chunk, so a draw is a single call that carries its own index range and base vertex.
    /// </summary>
    /// <remarks>
    /// The index buffer is bound once for the whole arena and each record points into it, which removes the per chunk
    /// SetIndexBuffer as well. A later compute pass can zero the index count of a chunk to cull it without touching the
    /// command stream, which is what makes GPU culling possible.
    /// </remarks>
    private readonly WgpuBuffer _drawArgs;

    /// <summary>Slot in the indirect buffer each resident chunk occupies.</summary>
    private readonly Dictionary<long, int> _slotOf = [];

    /// <summary>Slots freed by chunks that left, reused before the next fresh slot.</summary>
    private readonly Stack<int> _freeSlots = [];

    private int _slotsUsed;
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
    /// <param name="expectedChunks">
    /// Chunks the view distance can hold, used to presize instead of doubling repeatedly; 0 disables it.
    /// </param>
    public ChunkGeometryArena(Device device, int bytesPerVertex = VertexLayout.Stride, int expectedChunks = 0)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bytesPerVertex);

        _device = device;
        _queue = device.Queue;
        _bytesPerVertex = bytesPerVertex;
        _expectedChunks = expectedChunks;
        // With a view distance the arena can start close to its final size, which removes the early doublings
        // before enough chunks have arrived to measure an average
        // (docs/specs/network/play-movement.md 7.27).
        var initialCapacity = expectedChunks > 0
            ? Math.Max(MinimumBufferBytes, (ulong)expectedChunks * AssumedBytesPerChunk)
            : MinimumBufferBytes;

        _vertexCapacity = initialCapacity;
        _indexCapacity = Math.Max(MinimumBufferBytes, initialCapacity / 2);
        _vertex = CreateBuffer(vertexCapacity: _vertexCapacity, BufferUsage.Vertex);
        _index = CreateBuffer(vertexCapacity: _indexCapacity, BufferUsage.Index);

        _drawArgs = _device.CreateBuffer(new BufferDescriptor
        {
            Size = (ulong)(MaxDrawSlots * DrawArgBytes),
            Usage = BufferUsage.Indirect | BufferUsage.CopyDst
        });
    }

    /// <summary>Chunks currently resident.</summary>
    public int ChunkCount => _slices.Count;

    /// <summary>Bytes pushed to the GPU since creation; the measure that shows the fix worked.</summary>
    public long UploadedBytes { get; private set; }

    /// <summary>Times the buffers had to be grown and everything rewritten.</summary>
    public int FullRebuilds { get; private set; }

    /// <summary>True once the arena sized itself for the whole view distance.</summary>
    public bool Presized => _presized;

    /// <summary>Uploads one chunk's geometry, replacing whatever that chunk had.</summary>
    /// <param name="chunkKey">Packed chunk key.</param>
    /// <param name="mesh">Geometry for that chunk.</param>
    public void Apply(long chunkKey, GeometryData mesh)
    {
        if (mesh.Vertices.Length == 0)
        {
            _meshes.Remove(chunkKey);

            if (_slices.Remove(chunkKey, out var gone))
            {
                _freeSlots.Push(gone.Slot);
            }

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

        var slot = TakeSlot();

        if (slot < 0)
        {
            _meshes.Remove(chunkKey);
            return;
        }

        var (minimum, maximum) = BoundsOf(mesh);
        var slice = new Slice(_vertexUsed, _indexUsed, (uint)mesh.Indices.Length, slot, minimum, maximum);
        _queue.WriteBuffer(_vertex, slice.VertexOffset, mesh.Vertices.AsSpan());
        _queue.WriteBuffer(_index, slice.IndexOffset, mesh.Indices.AsSpan());
        WriteDrawArgs(slice);
        UploadedBytes += (long)neededVertex + (long)neededIndex;

        _vertexUsed += neededVertex;
        _indexUsed += neededIndex;
        _slices[chunkKey] = slice;
    }

    /// <summary>
    /// Highest draw slot ever handed out plus one, including holes left by chunks that left.
    /// </summary>
    /// <remarks>
    /// The cull pass must cover every slot, not just the resident count: slots above the count keep whatever the previous
    /// frame wrote, so a chunk culled once would stay invisible after later chunks were removed.
    /// </remarks>
    public int SlotHighWater => _slotsUsed;

    /// <summary>Optional GPU culler whose argument buffer drives the draws.</summary>
    public ChunkCuller? Culler { get; set; }

    /// <summary>Chunks the last culled draw skipped.</summary>
    public int CulledLastFrame { get; private set; }

    /// <summary>Draws every resident chunk, one range each.</summary>
    /// <param name="pass">Render pass to record into.</param>
    public void Draw(RenderPassEncoder pass) => Draw(pass, default);

    /// <summary>
    /// Draws the resident chunks, skipping any whose bounds fall outside the frustum.
    /// </summary>
    /// <param name="pass">Render pass to record into.</param>
    /// <param name="planes">Six inward facing frustum planes, or fewer to draw everything.</param>
    /// <remarks>
    /// This is the CPU reference for the compute cull pass: same visibility test, same result, so the two can be compared
    /// and the GPU path can be dropped back to this one when compute is unavailable.
    /// </remarks>
    public void Draw(RenderPassEncoder pass, ReadOnlySpan<System.Numerics.Vector4> planes)
    {
        if (_slices.Count == 0)
        {
            return;
        }

        pass.SetVertexBuffer(0, _vertex, 0, _vertexCapacity);

        // One binding for the whole index buffer: each record carries its own first index and base vertex.
        pass.SetIndexBuffer(_index, WGPUIndexFormat.Uint32, 0, _indexCapacity);

        CulledLastFrame = 0;

        foreach (var slice in _slices.Values)
        {
            if (planes.Length >= 6 && !FrustumPlanes.IsVisible(planes, slice.Minimum, slice.Maximum))
            {
                CulledLastFrame++;
                continue;
            }

            pass.DrawIndexedIndirect(_drawArgs, (ulong)(slice.Slot * DrawArgBytes));
        }
    }

    /// <summary>Draws every resident chunk through the culler's argument buffer.</summary>
    /// <param name="pass">Render pass to record into.</param>
    /// <remarks>The cull pass has already zeroed the index count of the invisible chunks.</remarks>
    public void DrawWithCuller(RenderPassEncoder pass)
    {
        if (_slices.Count == 0 || Culler is not { Available: true } culler || culler.Args is not { } arguments)
        {
            Draw(pass);
            return;
        }

        pass.SetVertexBuffer(0, _vertex, 0, _vertexCapacity);
        pass.SetIndexBuffer(_index, WGPUIndexFormat.Uint32, 0, _indexCapacity);

        foreach (var slice in _slices.Values)
        {
            pass.DrawIndexedIndirect(arguments, (ulong)(slice.Slot * DrawArgBytes));
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _vertex.Dispose();
        _index.Dispose();
        _drawArgs.Dispose();

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
            Size = Math.Min(vertexCapacity, MaxBufferBytes),
            Usage = usage | BufferUsage.CopyDst
        });

    /// <summary>World space bounds of a mesh, from its positions.</summary>
    /// <param name="mesh">Geometry whose bounds to compute.</param>
    private (System.Numerics.Vector3 Minimum, System.Numerics.Vector3 Maximum) BoundsOf(GeometryData mesh)
    {
        var stride = _bytesPerVertex / sizeof(float);
        var vertices = mesh.Vertices;
        var minimum = new System.Numerics.Vector3(float.MaxValue);
        var maximum = new System.Numerics.Vector3(float.MinValue);

        for (var index = 0; index + 2 < vertices.Length; index += stride)
        {
            var x = vertices[index];
            var y = vertices[index + 1];
            var z = vertices[index + 2];

            if (x < minimum.X) { minimum.X = x; }
            if (y < minimum.Y) { minimum.Y = y; }
            if (z < minimum.Z) { minimum.Z = z; }
            if (x > maximum.X) { maximum.X = x; }
            if (y > maximum.Y) { maximum.Y = y; }
            if (z > maximum.Z) { maximum.Z = z; }
        }

        return (minimum, maximum);
    }

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

        // One deliberate presize for the whole view distance beats log2(n) doublings, each of which rewrites
        // every resident chunk (docs/specs/network/play-movement.md 7.27).
        var vertexCapacity = Math.Max(
            ArenaSizing.NextCapacity(_vertexCapacity, _vertexUsed, (ulong)_vertexUsed, _meshes.Count, _expectedChunks, _presized),
            MinimumBufferBytes);

        var indexCapacity = Math.Max(
            ArenaSizing.NextCapacity(_indexCapacity, _indexUsed, (ulong)_indexUsed, _meshes.Count, _expectedChunks, _presized),
            MinimumBufferBytes);

        if (!_presized && _expectedChunks > 0 && _meshes.Count >= ArenaSizing.SamplesForPresize)
        {
            _presized = true;
        }

        _retired.Add(_vertex);
        _retired.Add(_index);

        _vertex = CreateBuffer(vertexCapacity, BufferUsage.Vertex);
        _index = CreateBuffer(indexCapacity, BufferUsage.Index);

        _vertexCapacity = Math.Min(vertexCapacity, MaxBufferBytes);
        _indexCapacity = Math.Min(indexCapacity, MaxBufferBytes);
        _vertexUsed = 0;
        _indexUsed = 0;
        _slices.Clear();
        _slotOf.Clear();
        _freeSlots.Clear();
        _slotsUsed = 0;

        foreach (var (key, mesh) in _meshes)
        {
            var vertexBytes = (ulong)mesh.Vertices.Length * sizeof(float);
            var indexBytes = (ulong)mesh.Indices.Length * sizeof(uint);

            if (_vertexUsed + vertexBytes > _vertexCapacity || _indexUsed + indexBytes > _indexCapacity)
            {
                // Out of room at the ceiling: leave this chunk out rather than failing the buffer creation. Chunks are
                // meshed nearest first, so the ones dropped are at the far edge of the view.
                continue;
            }

            var slot = TakeSlot();

            if (slot < 0)
            {
                continue;
            }

            var (minimum, maximum) = BoundsOf(mesh);
            var slice = new Slice(_vertexUsed, _indexUsed, (uint)mesh.Indices.Length, slot, minimum, maximum);
            _queue.WriteBuffer(_vertex, slice.VertexOffset, mesh.Vertices.AsSpan());
            _queue.WriteBuffer(_index, slice.IndexOffset, mesh.Indices.AsSpan());
            WriteDrawArgs(slice);
            UploadedBytes += SizeOf(mesh);

            _vertexUsed += vertexBytes;
            _indexUsed += indexBytes;
            _slices[key] = slice;
        }
    }

    /// <summary>Takes a free draw slot, or -1 when the indirect buffer is full.</summary>
    private int TakeSlot()
    {
        if (_freeSlots.Count > 0)
        {
            return _freeSlots.Pop();
        }

        if (_slotsUsed >= MaxDrawSlots)
        {
            return -1;
        }

        return _slotsUsed++;
    }

    /// <summary>Writes one chunk's indirect draw arguments.</summary>
    /// <param name="slice">The slice whose record to write.</param>
    private void WriteDrawArgs(Slice slice)
    {
        Span<byte> record = stackalloc byte[DrawArgBytes];
        BitConverter.TryWriteBytes(record, slice.IndexCount);
        BitConverter.TryWriteBytes(record[4..], 1u);
        BitConverter.TryWriteBytes(record[8..], (uint)(slice.IndexOffset / sizeof(uint)));
        BitConverter.TryWriteBytes(record[12..], (int)(slice.VertexOffset / (ulong)_bytesPerVertex));
        BitConverter.TryWriteBytes(record[16..], 0u);
        _queue.WriteBuffer(_drawArgs, (ulong)(slice.Slot * DrawArgBytes), record);
        Culler?.WriteSlot(
            slice.Slot,
            slice.Minimum,
            slice.Maximum,
            slice.IndexCount,
            (uint)(slice.IndexOffset / sizeof(uint)),
            (int)(slice.VertexOffset / (ulong)_bytesPerVertex));
    }

    /// <summary>Where one chunk's geometry lives in the arena.</summary>
    /// <param name="VertexOffset">Byte offset into the vertex buffer.</param>
    /// <param name="IndexOffset">Byte offset into the index buffer.</param>
    /// <param name="IndexCount">Indices this chunk draws.</param>
    /// <param name="Slot">Its indirect draw record.</param>
    private readonly record struct Slice(
        ulong VertexOffset,
        ulong IndexOffset,
        uint IndexCount,
        int Slot,
        System.Numerics.Vector3 Minimum,
        System.Numerics.Vector3 Maximum);
}
