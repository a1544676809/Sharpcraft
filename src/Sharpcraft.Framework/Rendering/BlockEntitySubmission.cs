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
    private int _solidIndexCount;
    private bool _disposed;

    /// <summary>Creates an empty submission that owns its buffers.</summary>
    /// <param name="backend">Backend that owns every handle.</param>
    public BlockEntitySubmission(IRenderBackend backend)
    {
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
    }

    /// <summary>Indices the last upload left ready to draw; zero means nothing to draw.</summary>
    public int IndexCount => _indexCount;

    /// <summary>Leading indices drawn with the solid pipeline by the last upload.</summary>
    public int SolidIndexCount => _solidIndexCount;

    /// <summary>One contiguous indexed draw range within the submission's index buffer.</summary>
    internal readonly record struct DrawRange(int FirstIndex, int IndexCount, int BaseVertex);

    /// <summary>Solid and no-cull draw ranges for one submission.</summary>
    internal readonly record struct DrawPlan(DrawRange Solid, DrawRange NoCull);

    /// <summary>
    /// Splits a submission's indices into the solid prefix and the no-cull suffix. Both draws use the
    /// same vertex/index buffers, so their base vertex is zero and the no-cull draw starts exactly at
    /// the solid index count.
    /// </summary>
    internal static DrawPlan CreateDrawPlan(int indexCount, int solidIndexCount)
    {
        if ((uint)solidIndexCount > (uint)indexCount)
        {
            throw new ArgumentOutOfRangeException(nameof(solidIndexCount));
        }

        return new DrawPlan(
            new DrawRange(FirstIndex: 0, solidIndexCount, BaseVertex: 0),
            new DrawRange(FirstIndex: solidIndexCount, indexCount - solidIndexCount, BaseVertex: 0));
    }

    /// <summary>Binding the segment draws with, as supplied to the last upload.</summary>
    public BindGroupHandle BindGroup => _bindGroup;

    /// <summary>Uploads this frame's geometry, growing the buffers only when the frame outgrows them.</summary>
    /// <param name="geometry">Geometry rebuilt for this frame.</param>
    /// <param name="bindGroup">Binding of the atlas this geometry samples.</param>
    /// <param name="solidIndexCount">Leading indices that use the solid pipeline.</param>
    public void Upload(DynamicGeometry geometry, BindGroupHandle bindGroup, int solidIndexCount)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ObjectDisposedException.ThrowIf(_disposed, this);
        if ((uint)solidIndexCount > (uint)geometry.IndexCount)
        {
            throw new ArgumentOutOfRangeException(nameof(solidIndexCount));
        }

        _bindGroup = bindGroup;
        _indexCount = geometry.IndexCount;
        _solidIndexCount = solidIndexCount;

        if (_indexCount == 0)
        {
            return;
        }

        var vertexBytes = geometry.VertexFloats * sizeof(float);
        var indexBytes = geometry.IndexCount * sizeof(uint);

        if (vertexBytes > _vertexCapacityBytes)
        {
            if (_vertexCapacityBytes > 0)
            {
                _backend.Destroy(_vertex);
            }

            _vertexCapacityBytes = Math.Max(vertexBytes, _vertexCapacityBytes * 2);
            _vertex = _backend.CreateBuffer(
                new BufferDesc((ulong)_vertexCapacityBytes, BufferUsages.Vertex | BufferUsages.CopyDst));
        }

        if (indexBytes > _indexCapacityBytes)
        {
            if (_indexCapacityBytes > 0)
            {
                _backend.Destroy(_index);
            }

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
    /// <param name="noCullPipeline">Pipeline for the trailing no-cull index range.</param>
    /// <param name="solidPipeline">Pipeline for the leading solid index range.</param>
    public void Draw(
        IRenderBackend backend,
        RenderPassHandle pass,
        PipelineHandle noCullPipeline,
        PipelineHandle solidPipeline)
    {
        ArgumentNullException.ThrowIfNull(backend);
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_indexCount == 0)
        {
            return;
        }

        backend.SetBindGroup(pass, 0, _bindGroup);
        backend.SetVertexBuffer(pass, 0, _vertex);
        backend.SetIndexBuffer(pass, _index, IndexFormat.Uint32);

        var plan = CreateDrawPlan(_indexCount, _solidIndexCount);
        if (plan.Solid.IndexCount > 0)
        {
            backend.SetPipeline(pass, solidPipeline);
            backend.DrawIndexed(
                pass,
                (uint)plan.Solid.IndexCount,
                1,
                (uint)plan.Solid.FirstIndex,
                plan.Solid.BaseVertex,
                0);
        }

        if (plan.NoCull.IndexCount > 0)
        {
            backend.SetPipeline(pass, noCullPipeline);
            backend.DrawIndexed(
                pass,
                (uint)plan.NoCull.IndexCount,
                1,
                (uint)plan.NoCull.FirstIndex,
                plan.NoCull.BaseVertex,
                0);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_indexCapacityBytes > 0)
        {
            _backend.Destroy(_index);
        }

        if (_vertexCapacityBytes > 0)
        {
            _backend.Destroy(_vertex);
        }
    }
}
