// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Brew.Wgpu;
using Brew.Wgpu.Native;

namespace Sharpcraft.Framework.Rendering;

/// <summary>
/// Per-chunk GPU geometry: one vertex and index buffer per resident chunk, drawn with CPU frustum culling. This replaces
/// the single large arena buffers (ADR-0020): memory follows the resident meshes, so view distances 12-32 need no global
/// buffer ceiling and no far-chunk dropping.
/// </summary>
public sealed class ChunkGeometryStore : IDisposable
{
    private sealed class Entry
    {
        public required BufferHandle Vertex;
        public required BufferHandle Index;
        public required uint IndexCount;
        public required Vector3 Minimum;
        public required Vector3 Maximum;
        public required long Bytes;
    }

    private readonly IRenderBackend _backend;
    private readonly int _bytesPerVertex;
    private readonly Dictionary<long, Entry> _entries = [];
    private readonly List<(long Key, float Distance)> _sortKeys = [];

    /// <summary>Creates an empty store.</summary>
    public ChunkGeometryStore(IRenderBackend backend, Device device, int bytesPerVertex = VertexLayout.Stride)
    {
        ArgumentNullException.ThrowIfNull(backend);
        ArgumentNullException.ThrowIfNull(device);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bytesPerVertex);
        _backend = backend;
        _bytesPerVertex = bytesPerVertex;
    }

    /// <summary>Chunks currently resident.</summary>
    public int ChunkCount => _entries.Count;

    /// <summary>Bytes uploaded since creation (diagnostic).</summary>
    public long UploadedBytes { get; private set; }

    /// <summary>Bytes currently held by the per-chunk buffers (diagnostic).</summary>
    public long MemoryBytes { get; private set; }

    /// <summary>Chunks skipped by the last draw's frustum test (diagnostic).</summary>
    public int CulledLastFrame { get; private set; }

    /// <summary>Cumulative DrawIndexed calls issued by this store (diagnostic).</summary>
    public long DrawCallsTotal { get; private set; }

    /// <summary>Per-chunk storage never needs a bulk presize; kept for the renderer's diagnostics.</summary>
    public bool Presized => false;

    /// <summary>Per-chunk storage never rewrites the whole world; kept for the renderer's diagnostics.</summary>
    public int FullRebuilds => 0;

    /// <summary>Uploads one chunk's geometry, replacing whatever that chunk had.</summary>
    public void Apply(long chunkKey, GeometryData mesh)
    {
        if (mesh.Vertices.Length == 0)
        {
            Remove(chunkKey);
            return;
        }

        var vertexBytes = (long)mesh.Vertices.Length * sizeof(float);
        var indexBytes = (long)mesh.Indices.Length * sizeof(uint);

        if (_entries.TryGetValue(chunkKey, out var existing)
            && existing.IndexCount == (uint)mesh.Indices.Length && existing.Bytes == vertexBytes + indexBytes)
        {
            // Same shape: rewrite in place, the common case for a block edit.
            _backend.WriteBuffer(existing.Vertex, System.Runtime.InteropServices.MemoryMarshal.AsBytes(mesh.Vertices.AsSpan()), 0);
            _backend.WriteBuffer(existing.Index, System.Runtime.InteropServices.MemoryMarshal.AsBytes(mesh.Indices.AsSpan()), 0);
            UploadedBytes += vertexBytes + indexBytes;
            return;
        }

        Remove(chunkKey);

        var vertexBuffer = _backend.CreateBuffer(new BufferDesc((ulong)vertexBytes, BufferUsages.Vertex | BufferUsages.CopyDst));
        var indexBuffer = _backend.CreateBuffer(new BufferDesc((ulong)indexBytes, BufferUsages.Index | BufferUsages.CopyDst));
        _backend.WriteBuffer(vertexBuffer, System.Runtime.InteropServices.MemoryMarshal.AsBytes(mesh.Vertices.AsSpan()), 0);
        _backend.WriteBuffer(indexBuffer, System.Runtime.InteropServices.MemoryMarshal.AsBytes(mesh.Indices.AsSpan()), 0);

        var (minimum, maximum) = BoundsOf(mesh);
        _entries[chunkKey] = new Entry
        {
            Vertex = vertexBuffer,
            Index = indexBuffer,
            IndexCount = (uint)mesh.Indices.Length,
            Minimum = minimum,
            Maximum = maximum,
            Bytes = vertexBytes + indexBytes
        };
        UploadedBytes += vertexBytes + indexBytes;
        MemoryBytes += vertexBytes + indexBytes;
    }

    /// <summary>Releases one chunk's buffers (it left the view).</summary>
    public void Remove(long chunkKey)
    {
        if (!_entries.Remove(chunkKey, out var gone))
        {
            return;
        }

        MemoryBytes -= gone.Bytes;
        _backend.Destroy(gone.Vertex);
        _backend.Destroy(gone.Index);
    }

    /// <summary>Draws the resident chunks, skipping any whose bounds fall outside the frustum.</summary>
    public void Draw(RenderPassEncoder pass, ReadOnlySpan<Vector4> planes)
    {
        CulledLastFrame = 0;

        foreach (var entry in _entries.Values)
        {
            if (planes.Length >= 6 && !FrustumPlanes.IsVisible(planes, entry.Minimum, entry.Maximum))
            {
                CulledLastFrame++;
                continue;
            }

            DrawEntry(pass, entry);
        }
    }

    /// <summary>Draws all resident chunks (no culling).</summary>
    public void Draw(RenderPassEncoder pass) => Draw(pass, default);

    /// <summary>Draws the resident chunks farthest first, which is the order blending needs.</summary>
    public void DrawFarthestFirst(RenderPassEncoder pass, Vector3 eye)
    {
        _sortKeys.Clear();
        foreach (var (key, entry) in _entries)
        {
            var cx = ((entry.Minimum.X + entry.Maximum.X) * 0.5f) - eye.X;
            var cy = ((entry.Minimum.Y + entry.Maximum.Y) * 0.5f) - eye.Y;
            var cz = ((entry.Minimum.Z + entry.Maximum.Z) * 0.5f) - eye.Z;
            _sortKeys.Add((key, (cx * cx) + (cy * cy) + (cz * cz)));
        }

        _sortKeys.Sort(static (a, b) => b.Distance.CompareTo(a.Distance));

        foreach (var (key, _) in _sortKeys)
        {
            if (_entries.TryGetValue(key, out var entry))
            {
                DrawEntry(pass, entry);
            }
        }
    }

    private void DrawEntry(RenderPassEncoder pass, Entry entry)
    {
        DrawCallsTotal++;
        // Pass recording has not moved behind the seam yet, so unwrap the native buffers for it (W2).
        var wgpu = _backend is WgpuRenderBackend native
            ? native
            : throw new InvalidOperationException("the seam implementation must unwrap its own handles");
        pass.SetVertexBuffer(0, wgpu.AsBuffer(entry.Vertex));
        pass.SetIndexBuffer(wgpu.AsBuffer(entry.Index), WGPUIndexFormat.Uint32);
        pass.DrawIndexed(entry.IndexCount);
    }

    private (Vector3 Minimum, Vector3 Maximum) BoundsOf(GeometryData mesh)
    {
        var minimum = new Vector3(float.MaxValue);
        var maximum = new Vector3(float.MinValue);
        var floatsPerVertex = _bytesPerVertex / sizeof(float);
        for (var i = 0; i + 2 < mesh.Vertices.Length; i += floatsPerVertex)
        {
            var position = new Vector3(mesh.Vertices[i], mesh.Vertices[i + 1], mesh.Vertices[i + 2]);
            minimum = Vector3.Min(minimum, position);
            maximum = Vector3.Max(maximum, position);
        }

        return (minimum, maximum);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var entry in _entries.Values)
        {
            _backend.Destroy(entry.Vertex);
            _backend.Destroy(entry.Index);
        }

        _entries.Clear();
        MemoryBytes = 0;
    }
}
