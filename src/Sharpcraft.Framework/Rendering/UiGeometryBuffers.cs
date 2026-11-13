// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Framework.Rendering;

/// <summary>
/// The interface pass's vertex and index buffers, grown to fit the geometry a frame uploads.
/// </summary>
/// <remarks>
/// Same shape as <see cref="EntityLayerPool"/>: the buffers persist across frames and only grow, and a
/// buffer that is replaced goes to the release queue rather than being destroyed while a frame may still be
/// reading it. Pulling the five related locals out of the windowed loop gives them one owner and one
/// invariant — capacity always describes the buffer beside it (T-837).
/// </remarks>
internal sealed class UiGeometryBuffers
{
    private readonly IRenderBackend _backend;
    private readonly DeferredBufferRelease _deferred;
    private readonly ulong _minimumBytes;

    private ulong _vertexCapacity;
    private ulong _indexCapacity;

    private BufferHandle _vertex;
    private BufferHandle _index;

    /// <summary>Creates both buffers at the minimum size.</summary>
    /// <param name="backend">Backend to create buffers through.</param>
    /// <param name="deferred">Queue that disposes a buffer once no frame can be reading it.</param>
    /// <param name="minimumBytes">Smallest a buffer is allowed to be.</param>
    public UiGeometryBuffers(IRenderBackend backend, DeferredBufferRelease deferred, ulong minimumBytes)
    {
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        _deferred = deferred ?? throw new ArgumentNullException(nameof(deferred));
        _minimumBytes = minimumBytes;
        _vertexCapacity = minimumBytes;
        _indexCapacity = minimumBytes;
        _vertex = backend.CreateBuffer(new BufferDesc(minimumBytes, BufferUsages.Vertex | BufferUsages.CopyDst));
        _index = backend.CreateBuffer(new BufferDesc(minimumBytes, BufferUsages.Index | BufferUsages.CopyDst));
    }

    /// <summary>Buffer the frame's interface vertices are written into.</summary>
    public BufferHandle Vertex => _vertex;

    /// <summary>Buffer the frame's interface indices are written into.</summary>
    public BufferHandle Index => _index;

    /// <summary>Bytes allocated for <see cref="Vertex"/>.</summary>
    public ulong VertexCapacity => _vertexCapacity;

    /// <summary>Bytes allocated for <see cref="Index"/>.</summary>
    public ulong IndexCapacity => _indexCapacity;

    /// <summary>Indices the last upload wrote; zero means nothing to draw.</summary>
    public uint IndexCount { get; private set; }

    /// <summary>Records how many indices this frame's upload produced.</summary>
    /// <param name="count">Index count, or zero when there is nothing to draw.</param>
    public void SetIndexCount(uint count) => IndexCount = count;

    /// <summary>Replaces the vertex buffer if the frame needs more room than it has.</summary>
    /// <param name="neededBytes">Bytes the frame's vertices occupy.</param>
    public void EnsureVertexCapacity(ulong neededBytes)
    {
        if (neededBytes <= _vertexCapacity)
        {
            return;
        }

        _deferred.Release(_vertex);
        _vertexCapacity = Math.Max(neededBytes * 2, _minimumBytes);
        _vertex = _backend.CreateBuffer(
            new BufferDesc(_vertexCapacity, BufferUsages.Vertex | BufferUsages.CopyDst));
    }

    /// <summary>Replaces the index buffer if the frame needs more room than it has.</summary>
    /// <param name="neededBytes">Bytes the frame's indices occupy.</param>
    public void EnsureIndexCapacity(ulong neededBytes)
    {
        if (neededBytes <= _indexCapacity)
        {
            return;
        }

        _deferred.Release(_index);
        _indexCapacity = Math.Max(neededBytes * 2, _minimumBytes);
        _index = _backend.CreateBuffer(
            new BufferDesc(_indexCapacity, BufferUsages.Index | BufferUsages.CopyDst));
    }

    /// <summary>Destroys both buffers immediately, for shutdown where nothing is in flight.</summary>
    public void Destroy()
    {
        _backend.Destroy(_vertex);
        _backend.Destroy(_index);
    }
}
