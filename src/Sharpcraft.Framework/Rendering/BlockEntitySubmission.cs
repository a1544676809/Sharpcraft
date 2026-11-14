// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Runtime.InteropServices;
using Brew.Wgpu;

namespace Sharpcraft.Framework.Rendering;

/// <summary>
/// One block entity family's geometry for a frame: uploaded to its own buffers and drawn as a segment
/// with that family's atlas bound, outside the terrain geometry.
/// </summary>
/// <remarks>
/// The reference submits block entities through a node collector that batches by render type, while
/// terrain comes from per section compiled meshes - the two never share geometry
/// (docs/specs/render/block-entity-submission.md). This is the container for one such batch.
/// </remarks>
public sealed class BlockEntitySubmission : IDisposable
{
    private readonly IRenderBackend _backend;
    private BufferHandle _vertex;
    private BufferHandle _index;
    private BindGroupHandle _bindGroup;
    private int _vertexCapacityBytes;
    private int _indexCapacityBytes;
    private int _indexCount;
    private bool _disposed;

    /// <summary>Creates an empty submission that owns its buffers.</summary>
    /// <param name="backend">Backend that owns every handle.</param>
    public BlockEntitySubmission(IRenderBackend backend)
    {
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
    }

    /// <summary>Indices the last upload left ready to draw; zero means nothing to draw.</summary>
    public int IndexCount => _indexCount;

    /// <summary>Binding the segment draws with, as supplied to the last upload.</summary>
    public BindGroupHandle BindGroup => _bindGroup;

    /// <summary>Uploads this frame's geometry, growing the buffers only when the frame outgrows them.</summary>
    /// <param name="geometry">Geometry rebuilt for this frame.</param>
    /// <param name="bindGroup">Binding of the atlas this geometry samples.</param>
    public void Upload(DynamicGeometry geometry, BindGroupHandle bindGroup)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ObjectDisposedException.ThrowIf(_disposed, this);

        _bindGroup = bindGroup;
        _indexCount = geometry.IndexCount;

        if (_indexCount == 0)
        {
            return;
        }

        var vertexBytes = geometry.VertexFloats * sizeof(float);
        var indexBytes = geometry.IndexCount * sizeof(uint);

        if (vertexBytes > _vertexCapacityBytes)
        {
            _backend.Destroy(_vertex);
            _vertexCapacityBytes = Math.Max(vertexBytes, _vertexCapacityBytes * 2);
            _vertex = _backend.CreateBuffer(
                new BufferDesc((ulong)_vertexCapacityBytes, BufferUsages.Vertex | BufferUsages.CopyDst));
        }

        if (indexBytes > _indexCapacityBytes)
        {
            _backend.Destroy(_index);
            _indexCapacityBytes = Math.Max(indexBytes, _indexCapacityBytes * 2);
            _index = _backend.CreateBuffer(
                new BufferDesc((ulong)_indexCapacityBytes, BufferUsages.Index | BufferUsages.CopyDst));
        }

        _backend.WriteBuffer(
            _vertex,
            MemoryMarshal.AsBytes(geometry.Vertices.AsSpan(0, geometry.VertexFloats)),
            0);
        _backend.WriteBuffer(
            _index,
            MemoryMarshal.AsBytes(geometry.Indices.AsSpan(0, geometry.IndexCount)),
            0);
    }

    /// <summary>Draws the segment, binding its atlas and buffers first.</summary>
    /// <param name="backend">Backend recording the pass.</param>
    /// <param name="pass">Pass the segment belongs to.</param>
    /// <param name="pipeline">Pipeline to draw with; the terrain pipeline works when the layouts match.</param>
    public void Draw(IRenderBackend backend, RenderPassHandle pass, PipelineHandle pipeline)
    {
        ArgumentNullException.ThrowIfNull(backend);
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_indexCount == 0)
        {
            return;
        }

        backend.SetPipeline(pass, pipeline);
        backend.SetBindGroup(pass, 0, _bindGroup);
        backend.SetVertexBuffer(pass, 0, _vertex);
        backend.SetIndexBuffer(pass, _index, IndexFormat.Uint32);
        backend.DrawIndexed(pass, (uint)_indexCount, 1, 0, 0, 0);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _backend.Destroy(_index);
        _backend.Destroy(_vertex);
    }
}
