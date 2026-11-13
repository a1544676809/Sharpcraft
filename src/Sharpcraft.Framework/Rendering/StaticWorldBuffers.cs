// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Framework.Rendering;

/// <summary>
/// The combined static world geometry the host supplies when there is no chunk arena, with the buffers it
/// lives in and the ones that were replaced along the way.
/// </summary>
/// <remarks>
/// The handles are mutable and read through properties rather than handed out once. That is the whole point:
/// a rebuild can replace the buffers, and a consumer that captured the handle earlier would keep drawing from
/// the retired one — which is then destroyed, so it is not merely stale geometry but a released resource.
/// </remarks>
internal sealed class StaticWorldBuffers
{
    private readonly IRenderBackend _backend;
    private readonly ulong _minimumBytes;
    private readonly List<BufferHandle> _retiredVertex;
    private readonly List<BufferHandle> _retiredIndex;

    private ulong _vertexCapacity;
    private ulong _indexCapacity;

    /// <summary>Creates the buffers at whatever the request needs, and uploads it.</summary>
    /// <param name="backend">Backend to create and upload through.</param>
    /// <param name="request">Request the initial geometry comes from.</param>
    /// <param name="minimumBytes">Smallest a buffer is allowed to be.</param>
    /// <param name="retiredVertex">Receives vertex buffers replaced by a rebuild.</param>
    /// <param name="retiredIndex">Receives index buffers replaced by a rebuild.</param>
    public StaticWorldBuffers(
        IRenderBackend backend,
        FrameRequest request,
        ulong minimumBytes,
        List<BufferHandle> retiredVertex,
        List<BufferHandle> retiredIndex)
    {
        ArgumentNullException.ThrowIfNull(backend);
        ArgumentNullException.ThrowIfNull(request);
        _backend = backend;
        _minimumBytes = minimumBytes;
        _retiredVertex = retiredVertex ?? throw new ArgumentNullException(nameof(retiredVertex));
        _retiredIndex = retiredIndex ?? throw new ArgumentNullException(nameof(retiredIndex));

        _vertexCapacity = Math.Max((ulong)request.Vertices.Length * sizeof(float), minimumBytes);
        _indexCapacity = Math.Max((ulong)request.Indices.Length * sizeof(uint), minimumBytes);
        Vertex = backend.CreateBuffer(
            new BufferDesc(_vertexCapacity, BufferUsages.Vertex | BufferUsages.CopyDst));
        Index = backend.CreateBuffer(
            new BufferDesc(_indexCapacity, BufferUsages.Index | BufferUsages.CopyDst));

        _backend.WriteBuffer(
            Vertex,
            System.Runtime.InteropServices.MemoryMarshal.AsBytes(request.Vertices.AsSpan()),
            0);
        _backend.WriteBuffer(
            Index,
            System.Runtime.InteropServices.MemoryMarshal.AsBytes(request.Indices.AsSpan()),
            0);
    }

    /// <summary>Buffer the static world's vertices live in; changes when a rebuild needs more room.</summary>
    public BufferHandle Vertex { get; private set; }

    /// <summary>Buffer the static world's indices live in; changes when a rebuild needs more room.</summary>
    public BufferHandle Index { get; private set; }

    /// <summary>Vertex buffers a rebuild replaced, awaiting destruction.</summary>
    public IReadOnlyList<BufferHandle> RetiredVertex => _retiredVertex;

    /// <summary>Index buffers a rebuild replaced, awaiting destruction.</summary>
    public IReadOnlyList<BufferHandle> RetiredIndex => _retiredIndex;

    /// <summary>Replaces the geometry with a rebuilt request, growing either buffer if it no longer fits.</summary>
    /// <param name="rebuilt">The rebuilt request.</param>
    /// <returns>Bytes uploaded, for the pacing diagnostic.</returns>
    public long Upload(FrameRequest rebuilt)
    {
        ArgumentNullException.ThrowIfNull(rebuilt);

        var neededVertex = (ulong)rebuilt.Vertices.Length * sizeof(float);
        var neededIndex = (ulong)rebuilt.Indices.Length * sizeof(uint);

        if (neededVertex > _vertexCapacity)
        {
            _retiredVertex.Add(Vertex);
            _vertexCapacity = Math.Max(neededVertex * 2, _minimumBytes);
            Vertex = _backend.CreateBuffer(
                new BufferDesc(_vertexCapacity, BufferUsages.Vertex | BufferUsages.CopyDst));
        }

        if (neededIndex > _indexCapacity)
        {
            _retiredIndex.Add(Index);
            _indexCapacity = Math.Max(neededIndex * 2, _minimumBytes);
            Index = _backend.CreateBuffer(
                new BufferDesc(_indexCapacity, BufferUsages.Index | BufferUsages.CopyDst));
        }

        _backend.WriteBuffer(
            Vertex,
            System.Runtime.InteropServices.MemoryMarshal.AsBytes(rebuilt.Vertices.AsSpan()),
            0);
        _backend.WriteBuffer(
            Index,
            System.Runtime.InteropServices.MemoryMarshal.AsBytes(rebuilt.Indices.AsSpan()),
            0);

        return ((long)rebuilt.Vertices.Length * sizeof(float)) + ((long)rebuilt.Indices.Length * sizeof(uint));
    }

    /// <summary>Destroys the live buffers and everything retired, for shutdown.</summary>
    public void Destroy()
    {
        _backend.Destroy(Vertex);
        _backend.Destroy(Index);
        foreach (var buffer in _retiredVertex)
        {
            _backend.Destroy(buffer);
        }

        foreach (var buffer in _retiredIndex)
        {
            _backend.Destroy(buffer);
        }

        _retiredVertex.Clear();
        _retiredIndex.Clear();
    }
}
