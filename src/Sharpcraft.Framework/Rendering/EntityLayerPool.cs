// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Framework.Rendering;

/// <summary>
/// The per-layer vertex and index buffers the entity pass draws from, grown to fit the layer plan.
/// </summary>
/// <remarks>
/// Persistent rather than recreated per frame: a layer's buffer is reused until a frame needs more room than
/// it has, at which point a larger one replaces it. The replaced buffer goes to
/// <see cref="DeferredBufferRelease"/> rather than being destroyed, because a frame still reading it may be
/// in flight.
/// <para>
/// Pulled out of the windowed loop because it is a component with its own invariant — the parallel lists must
/// stay the same length — rather than a pile of locals belonging to the frame (T-837).
/// </para>
/// </remarks>
/// <param name="backend">Backend to create buffers through.</param>
/// <param name="deferred">Queue that disposes a buffer once no frame can be reading it.</param>
/// <param name="minimumBytes">Smallest a buffer is allowed to be.</param>
internal sealed class EntityLayerPool(
    IGeometryBackend backend,
    DeferredBufferRelease deferred,
    ulong minimumBytes)
{
    private readonly IGeometryBackend _backend = backend ?? throw new ArgumentNullException(nameof(backend));
    private readonly DeferredBufferRelease _deferred = deferred ?? throw new ArgumentNullException(nameof(deferred));
    private readonly ulong _minimumBytes = minimumBytes;

    private readonly List<BufferHandle> _vertex = [];
    private readonly List<BufferHandle> _index = [];
    private readonly List<ulong> _vertexCapacity = [];
    private readonly List<ulong> _indexCapacity = [];
    private readonly List<uint> _indexCount = [];
    private readonly List<EntityGeometryLayerKind> _kind = [];
    private readonly List<bool> _usesItemAtlas = [];

    /// <summary>How many layers have buffers.</summary>
    public int Count => _vertex.Count;

    /// <summary>Vertex buffer of one layer.</summary>
    public BufferHandle Vertex(int layer) => _vertex[layer];

    /// <summary>Index buffer of one layer.</summary>
    public BufferHandle Index(int layer) => _index[layer];

    /// <summary>Bytes allocated for one layer's vertex buffer.</summary>
    public ulong VertexCapacity(int layer) => _vertexCapacity[layer];

    /// <summary>Bytes allocated for one layer's index buffer.</summary>
    public ulong IndexCapacity(int layer) => _indexCapacity[layer];

    /// <summary>Indices the last upload wrote for one layer; zero means nothing to draw.</summary>
    public uint IndexCount(int layer) => _indexCount[layer];

    /// <summary>What kind of geometry one layer holds.</summary>
    public EntityGeometryLayerKind Kind(int layer) => _kind[layer];

    /// <summary>Whether one layer samples the item atlas rather than the block atlas.</summary>
    public bool UsesItemAtlas(int layer) => _usesItemAtlas[layer];

    /// <summary>Records how many indices one layer holds this frame.</summary>
    public void SetIndexCount(int layer, uint count) => _indexCount[layer] = count;

    /// <summary>Records the geometry kind of one layer.</summary>
    public void SetKind(int layer, EntityGeometryLayerKind kind) => _kind[layer] = kind;

    /// <summary>Records which atlas one layer samples.</summary>
    public void SetUsesItemAtlas(int layer, bool usesItemAtlas) => _usesItemAtlas[layer] = usesItemAtlas;

    /// <summary>Adds buffers until the pool holds at least <paramref name="count"/> layers.</summary>
    /// <param name="count">Layers the plan asks for.</param>
    public void EnsureCapacity(int count)
    {
        while (_vertex.Count < count)
        {
            _vertex.Add(_backend.CreateBuffer(
                new BufferDesc(_minimumBytes, BufferUsages.Vertex | BufferUsages.CopyDst)));
            _index.Add(_backend.CreateBuffer(
                new BufferDesc(_minimumBytes, BufferUsages.Index | BufferUsages.CopyDst)));
            _vertexCapacity.Add(_minimumBytes);
            _indexCapacity.Add(_minimumBytes);
            _indexCount.Add(0);
            _kind.Add(default);
            _usesItemAtlas.Add(false);
        }
    }

    /// <summary>Replaces one layer's vertex buffer, if it is too small for what the frame needs.</summary>
    /// <param name="layer">Layer to grow.</param>
    /// <param name="neededBytes">Bytes the frame needs.</param>
    public void GrowVertex(int layer, ulong neededBytes)
    {
        if (neededBytes <= _vertexCapacity[layer])
        {
            return;
        }

        _deferred.Release(_vertex[layer]);
        var capacity = Math.Max(neededBytes * 2, _minimumBytes);
        _vertex[layer] = _backend.CreateBuffer(
            new BufferDesc(capacity, BufferUsages.Vertex | BufferUsages.CopyDst));
        _vertexCapacity[layer] = capacity;
    }

    /// <summary>Replaces one layer's index buffer, if it is too small for what the frame needs.</summary>
    /// <param name="layer">Layer to grow.</param>
    /// <param name="neededBytes">Bytes the frame needs.</param>
    public void GrowIndex(int layer, ulong neededBytes)
    {
        if (neededBytes <= _indexCapacity[layer])
        {
            return;
        }

        _deferred.Release(_index[layer]);
        var capacity = Math.Max(neededBytes * 2, _minimumBytes);
        _index[layer] = _backend.CreateBuffer(
            new BufferDesc(capacity, BufferUsages.Index | BufferUsages.CopyDst));
        _indexCapacity[layer] = capacity;
    }
}
