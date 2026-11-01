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

    /// <summary>Largest single presize, so one allocation never becomes a frame spike of its own.</summary>
    private const ulong MaxPresizeBytes = 16UL << 20;

    /// <summary>
    /// Starting guess per chunk when the view distance is known. Deliberately generous: over-reserving costs
    /// memory once, while under-reserving costs a full rewrite of every resident chunk, which is a frame spike.
    /// </summary>
    /// <summary>
    /// Starting guess per chunk when the view distance is known.
    /// </summary>
    /// <remarks>
    /// Measured on amplified terrain: a full view holds six hundred odd chunks at roughly half a megabyte of interleaved
    /// vertices each, so the old ninety six kilobyte guess under reserved by six times and the arena paid for it in
    /// full rewrites. Over reserving costs memory once; under reserving costs a spike.
    /// </remarks>
    private const ulong AssumedBytesPerChunk = 512 * 1024;

    /// <summary>
    /// Ceiling for one buffer. The device rejects anything above two hundred and fifty six megabytes, and amplified
    /// terrain needs more than that for a full view, so the arena stops at half the limit and simply skips chunks that no
    /// longer fit. Chunks are meshed nearest first, so what gets skipped is the far edge rather than the ground underfoot.
    /// </summary>
    /// <remarks>Paging several buffers would remove the ceiling; that is the planned replacement for this stop gap.</remarks>
    private const ulong MaxBufferBytes = 96UL << 20;

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
        // Start small: pre-reserving the whole view distance at the generous per-chunk guess multiplied across the three
        // layer arenas reserved over a gigabyte on a normal view distance and exhausted GPU memory. Grow on demand and let
        // the measured-average presize (ArenaSizing) size for the view once enough chunks have arrived.
        var initialCapacity = MinimumBufferBytes;

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
            // Once a buffer is at its ceiling a rebuild cannot help; drop the chunk instead of rebuilding every frame.
            // Chunks are meshed nearest first, so what is dropped is the far edge rather than the ground underfoot.
            if (_vertexCapacity >= MaxBufferBytes || _indexCapacity >= MaxBufferBytes
                || _vertexUsed + neededVertex > MaxBufferBytes || _indexUsed + neededIndex > MaxBufferBytes)
            {
                _meshes.Remove(chunkKey);
                return;
            }

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

    /// <summary>Reusable sort keys for the translucent pass, so ordering allocates nothing per frame.</summary>
    private SortKey[] _sortKeys = [];

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

    /// <summary>Draws the resident chunks farthest first, which is the order blending needs.</summary>
    /// <param name="pass">Render pass to record into.</param>
    /// <param name="eye">Camera position, in world units.</param>
    /// <remarks>
    /// Blending is order dependent, so the translucent layer has to be drawn back to front. Sorting the draw order by the
    /// chunk centre is enough to fix the usual artefacts, and it is the recorded approximation: per face ordering would
    /// be exact but costs far more than it is worth for cubes this size.
    /// </remarks>
    public void DrawFarthestFirst(RenderPassEncoder pass, System.Numerics.Vector3 eye)
    {
        if (_slices.Count == 0)
        {
            return;
        }

        if (_sortKeys.Length < _slices.Count)
        {
            _sortKeys = new SortKey[_slices.Count];
        }

        var count = 0;

        foreach (var slice in _slices.Values)
        {
            var centreX = (slice.Minimum.X + slice.Maximum.X) * 0.5f - eye.X;
            var centreY = (slice.Minimum.Y + slice.Maximum.Y) * 0.5f - eye.Y;
            var centreZ = (slice.Minimum.Z + slice.Maximum.Z) * 0.5f - eye.Z;

            _sortKeys[count].Distance = (centreX * centreX) + (centreY * centreY) + (centreZ * centreZ);
            _sortKeys[count].Slot = slice.Slot;
            count++;
        }

        Array.Sort(_sortKeys, 0, count);

        pass.SetVertexBuffer(0, _vertex, 0, _vertexCapacity);
        pass.SetIndexBuffer(_index, WGPUIndexFormat.Uint32, 0, _indexCapacity);

        for (var index = 0; index < count; index++)
        {
            pass.DrawIndexedIndirect(_drawArgs, (ulong)(_sortKeys[index].Slot * DrawArgBytes));
        }
    }

    /// <summary>One chunk's distance from the camera and its draw slot, ordered farthest first.</summary>
    private struct SortKey : IComparable<SortKey>
    {
        public float Distance;
        public int Slot;

        /// <inheritdoc />
        public int CompareTo(SortKey other) => other.Distance.CompareTo(Distance);
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
        // Allocating a very large buffer is itself a spike, so a single presize is capped; growth continues by doubling
        // if the world really needs more.
        var vertexCapacity = Math.Min(MaxBufferBytes, Math.Max(
            ArenaSizing.NextCapacity(_vertexCapacity, _vertexUsed, (ulong)_vertexUsed, _meshes.Count, _expectedChunks, _presized),
            MinimumBufferBytes));

        var indexCapacity = Math.Min(MaxBufferBytes, Math.Max(
            ArenaSizing.NextCapacity(_indexCapacity, _indexUsed, (ulong)_indexUsed, _meshes.Count, _expectedChunks, _presized),
            MinimumBufferBytes));

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
