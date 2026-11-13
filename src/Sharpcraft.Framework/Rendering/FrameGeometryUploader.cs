// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Framework.Rendering;

/// <summary>
/// Uploads the geometry a frame produced and applies chunk changes to the arenas.
/// </summary>
/// <remarks>
/// Takes the resolved lists rather than the callbacks that produce them. That is deliberate: the code this
/// replaced invoked the removal callback once to read a count and again to iterate, so a callback that drained
/// its queue had already handed everything over by the second call and the removals quietly did nothing.
/// Resolving outside means each source is consulted exactly once, and the type cannot be misused into
/// consulting it twice (T-838).
/// </remarks>
internal sealed class FrameGeometryUploader
{
    private readonly IRenderBackend _backend;
    private readonly GrowableGeometryBuffers _dynamic;
    private readonly EntityLayerPool _layers;
    private readonly ChunkGeometryStore? _arena;
    private readonly ChunkGeometryStore? _arenaCutout;
    private readonly ChunkGeometryStore? _arenaTranslucent;

    /// <summary>Captures the buffers and arenas the upload writes into.</summary>
    /// <param name="backend">Backend to upload through.</param>
    /// <param name="dynamic">Dynamic entity geometry buffers.</param>
    /// <param name="layers">Per-layer entity buffers.</param>
    /// <param name="arena">Opaque chunk arena, or null when the host supplies static geometry.</param>
    /// <param name="arenaCutout">Cutout chunk arena, or null when the host supplies static geometry.</param>
    /// <param name="arenaTranslucent">Translucent chunk arena, or null when there is none.</param>
    public FrameGeometryUploader(
        IRenderBackend backend,
        GrowableGeometryBuffers dynamic,
        EntityLayerPool layers,
        ChunkGeometryStore? arena,
        ChunkGeometryStore? arenaCutout,
        ChunkGeometryStore? arenaTranslucent)
    {
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        _dynamic = dynamic ?? throw new ArgumentNullException(nameof(dynamic));
        _layers = layers ?? throw new ArgumentNullException(nameof(layers));
        _arena = arena;
        _arenaCutout = arenaCutout;
        _arenaTranslucent = arenaTranslucent;
    }

    /// <summary>What the frame ended up holding, so the draw side knows how much to draw.</summary>
    /// <param name="DynamicIndexCount">Indices in the dynamic entity geometry.</param>
    /// <param name="LayersDrawn">Layers the layer plan produced.</param>
    /// <param name="Updates">Chunk updates applied this frame.</param>
    public readonly record struct Result(uint DynamicIndexCount, int LayersDrawn, int Updates);

    /// <summary>Uploads one frame's geometry.</summary>
    /// <param name="dynamic">Dynamic entity geometry, or null when the frame produced none.</param>
    /// <param name="layers">Entity layers, or null when the frame produced none.</param>
    /// <param name="updates">Chunk geometry updates, or null when there are none.</param>
    /// <param name="removals">Chunk keys to drop, or null when nothing left.</param>
    /// <param name="skipUpload">A/B toggle: keep the geometry identical but skip the buffer writes.</param>
    /// <returns>The counts the draw side needs.</returns>
    public Result Upload(
        DynamicGeometry? dynamic,
        IReadOnlyList<EntityGeometryLayer>? layers,
        IReadOnlyList<(long Key, GeometryData Mesh)>? updates,
        IReadOnlyList<long>? removals,
        bool skipUpload)
    {
        var dynamicIndexCount = UploadDynamic(dynamic, skipUpload);
        var layersDrawn = UploadLayers(layers, skipUpload);
        var applied = ApplyUpdates(updates, skipUpload);
        ApplyRemovals(removals);

        return new Result(dynamicIndexCount, layersDrawn, applied);
    }

    private uint UploadDynamic(DynamicGeometry? dynamic, bool skipUpload)
    {
        if (dynamic is not { IndexCount: > 0 })
        {
            return 0;
        }

        _dynamic.EnsureVertexCapacity((ulong)dynamic.VertexFloats * sizeof(float));
        _dynamic.EnsureIndexCapacity((ulong)dynamic.IndexCount * sizeof(uint));

        if (!skipUpload)
        {
            _backend.WriteBuffer(
                _dynamic.Vertex,
                System.Runtime.InteropServices.MemoryMarshal.AsBytes(
                    dynamic.Vertices.AsSpan(0, dynamic.VertexFloats)),
                0);
            _backend.WriteBuffer(
                _dynamic.Index,
                System.Runtime.InteropServices.MemoryMarshal.AsBytes(
                    dynamic.Indices.AsSpan(0, dynamic.IndexCount)),
                0);
        }

        return (uint)dynamic.IndexCount;
    }

    private int UploadLayers(IReadOnlyList<EntityGeometryLayer>? layers, bool skipUpload)
    {
        if (layers is not { Count: > 0 })
        {
            return 0;
        }

        var used = layers.Count;
        _layers.EnsureCapacity(used);
        for (var layerIndex = 0; layerIndex < used; layerIndex++)
        {
            var layer = layers[layerIndex];
            var geometry = layer.Geometry;
            _layers.SetIndexCount(layerIndex, 0);
            _layers.SetKind(layerIndex, layer.Kind);
            _layers.SetUsesItemAtlas(layerIndex, layer.UsesItemAtlas);
            if (geometry.IndexCount <= 0)
            {
                continue;
            }

            _layers.GrowVertex(layerIndex, (ulong)geometry.VertexFloats * sizeof(float));
            _layers.GrowIndex(layerIndex, (ulong)geometry.IndexCount * sizeof(uint));

            if (!skipUpload)
            {
                _backend.WriteBuffer(
                    _layers.Vertex(layerIndex),
                    System.Runtime.InteropServices.MemoryMarshal.AsBytes(
                        geometry.Vertices.AsSpan(0, geometry.VertexFloats)),
                    0);
                _backend.WriteBuffer(
                    _layers.Index(layerIndex),
                    System.Runtime.InteropServices.MemoryMarshal.AsBytes(
                        geometry.Indices.AsSpan(0, geometry.IndexCount)),
                    0);
            }

            _layers.SetIndexCount(layerIndex, (uint)geometry.IndexCount);
        }

        return used;
    }

    private int ApplyUpdates(IReadOnlyList<(long Key, GeometryData Mesh)>? updates, bool skipUpload)
    {
        if (_arena is null || updates is not { Count: > 0 })
        {
            return 0;
        }

        if (skipUpload)
        {
            return updates.Count;
        }

        foreach (var (key, mesh) in updates)
        {
            _arena.Apply(key, mesh);

            // The per chunk layers travel with the opaque geometry; a chunk with none just skips them.
            if (mesh.CutoutVertices is { Length: > 0 } cutoutVertices)
            {
                _arenaCutout?.Apply(key, new GeometryData(cutoutVertices, mesh.CutoutIndices ?? []));
            }

            if (mesh.TranslucentVertices is { Length: > 0 } translucentVertices)
            {
                _arenaTranslucent?.Apply(
                    key,
                    new GeometryData(translucentVertices, mesh.TranslucentIndices ?? []));
            }
        }

        return updates.Count;
    }

    private void ApplyRemovals(IReadOnlyList<long>? removals)
    {
        if (removals is not { Count: > 0 })
        {
            return;
        }

        foreach (var key in removals)
        {
            _arena?.Remove(key);
            _arenaCutout?.Remove(key);
            _arenaTranslucent?.Remove(key);
        }
    }
}
