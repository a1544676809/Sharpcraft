// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Framework.Rendering;

/// <summary>
/// Records the entity part of the scene pass: dynamic entity geometry first, then every layer the layer plan
/// produced, then the static fallback when there is no arena.
/// </summary>
/// <remarks>
/// This was a hundred line case inside a switch inside a two thousand line method. It is a component: it owns
/// the pipelines and buffers an entity draw needs, and the only things that change per frame are the counts
/// and the request.
/// <para>
/// Layers are skipped rather than drawn when they hold nothing, and item translucent layers are skipped here
/// because the reference OIT stages compose them later — drawing them in this pass would double up.
/// </para>
/// </remarks>
internal sealed class EntityPassRecorder
{
    private readonly IRenderBackend _backend;
    private readonly PipelineHandle _entityPipeline;
    private readonly PipelineHandle _cutoutPipeline;
    private readonly PipelineHandle _entityCutoutPipeline;
    private readonly PipelineHandle _itemCutoutPipeline;
    private readonly PipelineHandle _entityElytraPipeline;
    private readonly PipelineHandle _entityEmissivePipeline;
    private readonly PipelineHandle _entityGlintPipeline;
    private readonly EntitySkinTextureGpu _entitySkin;
    private readonly EntitySkinTextureGpu _itemAtlas;
    private readonly EntityLayerPool _layers;
    private readonly GrowableGeometryBuffers _dynamic;
    private readonly BindGroupHandle _terrainBindGroup;
    private readonly StaticWorldBuffers _staticWorld;

    /// <summary>Captures the handles an entity draw needs. They do not change between frames.</summary>
    /// <param name="backend">Backend to record through.</param>
    /// <param name="entityPipeline">Opaque and translucent entity pipeline.</param>
    /// <param name="cutoutPipeline">Terrain cutout pipeline, used for the static fallback's cutout geometry.</param>
    /// <param name="entityCutoutPipeline">Cutout entity pipeline.</param>
    /// <param name="itemCutoutPipeline">Cutout item pipeline.</param>
    /// <param name="entityElytraPipeline">Elytra pipeline.</param>
    /// <param name="entityEmissivePipeline">Emissive pipeline.</param>
    /// <param name="entityGlintPipeline">Glint pipeline.</param>
    /// <param name="entitySkin">Skin atlas, or one that was never created.</param>
    /// <param name="itemAtlas">Item atlas, or one that was never created.</param>
    /// <param name="layers">Per-layer buffers the layer plan fills.</param>
    /// <param name="dynamic">Dynamic entity geometry for this frame.</param>
    /// <param name="terrainBindGroup">Bind group restored after the entity draw.</param>
    /// <param name="staticWorld">Combined geometry used when there is no arena.</param>
    public EntityPassRecorder(
        IRenderBackend backend,
        PipelineHandle entityPipeline,
        PipelineHandle cutoutPipeline,
        PipelineHandle entityCutoutPipeline,
        PipelineHandle itemCutoutPipeline,
        PipelineHandle entityElytraPipeline,
        PipelineHandle entityEmissivePipeline,
        PipelineHandle entityGlintPipeline,
        EntitySkinTextureGpu entitySkin,
        EntitySkinTextureGpu itemAtlas,
        EntityLayerPool layers,
        GrowableGeometryBuffers dynamic,
        BindGroupHandle terrainBindGroup,
        StaticWorldBuffers staticWorld)
    {
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        _entityPipeline = entityPipeline;
        _cutoutPipeline = cutoutPipeline;
        _entityCutoutPipeline = entityCutoutPipeline;
        _itemCutoutPipeline = itemCutoutPipeline;
        _entityElytraPipeline = entityElytraPipeline;
        _entityEmissivePipeline = entityEmissivePipeline;
        _entityGlintPipeline = entityGlintPipeline;
        _entitySkin = entitySkin ?? throw new ArgumentNullException(nameof(entitySkin));
        _itemAtlas = itemAtlas ?? throw new ArgumentNullException(nameof(itemAtlas));
        _layers = layers ?? throw new ArgumentNullException(nameof(layers));
        _dynamic = dynamic ?? throw new ArgumentNullException(nameof(dynamic));
        _terrainBindGroup = terrainBindGroup;
        _staticWorld = staticWorld ?? throw new ArgumentNullException(nameof(staticWorld));
    }

    /// <summary>Records the entity draws into the scene pass.</summary>
    /// <param name="pass">Scene pass being recorded.</param>
    /// <param name="dynamicIndexCount">Indices the frame's dynamic entity geometry holds.</param>
    /// <param name="layersDrawn">Layers the layer plan produced this frame.</param>
    /// <param name="skipEntities">Host asked for entities to be left out.</param>
    /// <param name="useStaticWorld">There is no arena, so the request's static geometry is drawn instead.</param>
    /// <param name="request">Frame being recorded.</param>
    public void Record(
        RenderPassHandle pass,
        uint dynamicIndexCount,
        int layersDrawn,
        bool skipEntities,
        bool useStaticWorld,
        FrameRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!skipEntities && dynamicIndexCount > 0)
        {
            _backend.SetPipeline(pass, _entityPipeline);
            _backend.SetVertexBuffer(pass, 0, _dynamic.Vertex, 0, _dynamic.VertexCapacity);
            _backend.SetIndexBuffer(pass, _dynamic.Index, IndexFormat.Uint32, 0, (ulong)dynamicIndexCount * sizeof(uint));
            if (_entitySkin.Created)
            {
                _backend.SetBindGroup(pass, 0, _entitySkin.BindGroup);
            }

            _backend.DrawIndexed(pass, dynamicIndexCount, 1, 0, 0, 1);
            if (_entitySkin.Created)
            {
                _backend.SetBindGroup(pass, 0, _terrainBindGroup);
            }
        }

        // Cutout layers share the opaque depth; translucent layers blend with depth writes off, which the
        // pipeline state already encodes (docs/decisions/0021).
        for (var layer = 0; layer < layersDrawn; layer++)
        {
            var count = _layers.IndexCount(layer);
            if (count == 0)
            {
                continue;
            }

            var kind = _layers.Kind(layer);
            if (kind == EntityGeometryLayerKind.ItemTranslucent)
            {
                // Composed by the reference OIT stages, exactly as the reference binds OIT_ITEM there.
                continue;
            }

            _backend.SetPipeline(pass, kind switch
            {
                EntityGeometryLayerKind.ItemCutout => _itemCutoutPipeline,
                EntityGeometryLayerKind.Translucent => _entityPipeline,
                EntityGeometryLayerKind.Emissive => _entityEmissivePipeline,
                EntityGeometryLayerKind.Glint => _entityGlintPipeline,
                EntityGeometryLayerKind.Elytra => _entityElytraPipeline,
                _ => _entityCutoutPipeline
            });
            _backend.SetVertexBuffer(pass, 0, _layers.Vertex(layer), 0, _layers.VertexCapacity(layer));
            _backend.SetIndexBuffer(
                pass,
                _layers.Index(layer),
                IndexFormat.Uint32,
                0,
                (ulong)count * sizeof(uint));
            if (_layers.UsesItemAtlas(layer) && _itemAtlas.Created)
            {
                _backend.SetBindGroup(pass, 0, _itemAtlas.BindGroup);
            }
            else if (_entitySkin.Created)
            {
                _backend.SetBindGroup(pass, 0, _entitySkin.BindGroup);
            }

            _backend.DrawIndexed(pass, count, 1, 0, 0, 1);
        }

        if (layersDrawn > 0 && (_entitySkin.Created || _itemAtlas.Created))
        {
            _backend.SetBindGroup(pass, 0, _terrainBindGroup);
        }
        else if (useStaticWorld)
        {
            _backend.SetVertexBuffer(pass, 0, _staticWorld.Vertex);
            _backend.SetIndexBuffer(pass, _staticWorld.Index, IndexFormat.Uint32);
            _backend.DrawIndexed(pass, (uint)request.Indices.Length, 1, 0, 0, 1);
        }

        if (useStaticWorld && request.CutoutVertices is { Length: > 0 } cutoutVertices &&
            request.CutoutIndices is { Length: > 0 } cutoutIndices)
        {
            RecordStaticCutout(pass, cutoutVertices, cutoutIndices);
        }
    }

    /// <summary>
    /// Draws the request's cutout geometry when there is no arena. The buffers are created for the draw and
    /// destroyed straight after, because nothing else refers to them.
    /// </summary>
    private void RecordStaticCutout(RenderPassHandle pass, float[] vertices, uint[] indices)
    {
        var vertexBuffer = _backend.CreateBufferInit(
            new BufferDesc((ulong)vertices.Length * sizeof(float), BufferUsages.Vertex | BufferUsages.CopyDst),
            System.Runtime.InteropServices.MemoryMarshal.AsBytes(vertices.AsSpan()));
        var indexBuffer = _backend.CreateBufferInit(
            new BufferDesc((ulong)indices.Length * sizeof(uint), BufferUsages.Index | BufferUsages.CopyDst),
            System.Runtime.InteropServices.MemoryMarshal.AsBytes(indices.AsSpan()));

        _backend.SetPipeline(pass, _cutoutPipeline);
        _backend.SetBindGroup(pass, 0, _terrainBindGroup);
        _backend.SetVertexBuffer(pass, 0, vertexBuffer);
        _backend.SetIndexBuffer(pass, indexBuffer, IndexFormat.Uint32);
        _backend.DrawIndexed(pass, (uint)indices.Length, 1, 0, 0, 1);
        _backend.Destroy(indexBuffer);
        _backend.Destroy(vertexBuffer);
    }
}
