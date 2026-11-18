// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using System.Runtime.InteropServices;
using Sharpcraft.Client.Assets;
using Sharpcraft.Client.World;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Builds the static player model from <see cref="PlayerModelDefinition"/> and the default skin. This is render-only:
/// it consumes <see cref="EntityRenderState"/> and never touches world or entity simulation.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.geom.ModelPart", "ref://mc-26.3-analysis/src/net/minecraft/client/model/geom/ModelPart.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Adapter, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "Builds vertex geometry equivalent to ModelPart cuboids")]
public sealed partial class EntityModelMesher
{
    private readonly DynamicGeometry _geometry = new();
    private EntityRenderState _singleState;
    private readonly List<DynamicGeometry> _layerGeometries = [new(), new(), new(), new()];
    private readonly List<DynamicGeometry> _layerScratch = [new(), new(), new(), new()];

    /// <summary>One reusable pose per entity in the current batch, so a steady frame does not allocate poses.</summary>
    private readonly List<EntityPose> _animationPoses = [];

    /// <summary>Poses prepared for the most recent layer build, indexed like the batch that produced them.</summary>
    public IReadOnlyList<EntityPose> PreparedPoses => _animationPoses;

    /// <summary>
    /// Reused plan/group buffers for <see cref="BuildModelBackedLayers"/>. Cleared per frame/layer so a steady frame
    /// allocates no dictionaries, group records or entity lists.
    /// </summary>
    private readonly Dictionary<(string Name, EntityGeometryLayerKind Kind, int Order), EntityRenderLayerDefinition> _modelBackedPlanByKey = new();
    private readonly List<EntityRenderLayerDefinition> _modelBackedPlan = [];
    private readonly List<LayerGroup> _layerGroups = [];
    private readonly List<EntityRenderState> _groupEntities = [];
    private readonly List<EntityPose?> _groupPoses = [];

    /// <summary>Everything <c>BuildInto</c> observes for a layer besides the per-entity state and visibility.</summary>
    private readonly record struct LayerGroupKey(
        EntityModelDefinition Model,
        bool AllCubes,
        Func<EntityRenderState, string?>? TextureSelector);

    private sealed class LayerGroup
    {
        public EntityModelDefinition Model = null!;
        public EntityRenderLayerDefinition Layer = null!;
        public readonly List<int> EntityIndices = [];
    }

    /// <summary>Merged geometry of every layer, kept for callers that draw entities in one pass.</summary>
    public DynamicGeometry Build(ReadOnlySpan<EntityRenderState> entities, SpriteRect skin, int skinWidth, int skinHeight, IReadOnlyDictionary<string, FaceUvTransform>? transforms = null, string? onlyPart = null, string? excludedPart = null, ISpriteAtlas? skinAtlas = null, EntityVariantTable? variants = null, EntityPose? pose = null, ISpriteAtlas? itemAtlas = null)    {
        var layers = BuildLayers(entities, skin, skinWidth, skinHeight, transforms, onlyPart, excludedPart, skinAtlas, variants, pose, itemAtlas);
        _geometry.Clear();
        var totalVertexFloats = 0;
        var totalIndexCount = 0;
        foreach (var layer in layers)
        {
            totalVertexFloats += layer.Geometry.VertexFloats;
            totalIndexCount += layer.Geometry.IndexCount;
        }

        _geometry.Reserve(totalVertexFloats, totalIndexCount);
        var vertexBase = 0;
        var indexOffset = 0;
        foreach (var layer in layers)
        {
            var geometry = layer.Geometry;
            Array.Copy(geometry.Vertices, 0, _geometry.Vertices, vertexBase, geometry.VertexFloats);
            for (var i = 0; i < geometry.IndexCount; i++)
            {
                _geometry.Indices[indexOffset + i] = (uint)(geometry.Indices[i] + (vertexBase / ChunkMesher.FloatsPerVertex));
            }

            vertexBase += geometry.VertexFloats;
            indexOffset += geometry.IndexCount;
        }

        _geometry.VertexFloats = totalVertexFloats;
        _geometry.IndexCount = totalIndexCount;
        return _geometry;
    }

    /// <summary>
    /// Builds a single model definition directly, for block entity renderers that already own a
    /// baked <see cref="EntityModelDefinition"/>. The geometry path is the same <c>BuildInto</c>
    /// path used by <see cref="Build"/>; no part matrix, UV or face ordering is duplicated.
    /// </summary>
    /// <remarks>
    /// Reference basis: <c>EntityModelSet.bakeLayer</c> and <c>ModelPart</c> in
    /// <c>ref://mc-26.3-analysis/src/net/minecraft/client/model/geom/EntityModelSet.java</c> /
    /// <c>ref://mc-26.3-analysis/src/net/minecraft/client/model/geom/ModelPart.java</c>.
    /// Issue: #59.
    /// The state's X/Y/Z is the model origin in world coordinates; block entity callers pass the
    /// block centre (X+0.5, Y, Z+0.5) so the model rotates about the block centre.
    /// <see cref="EntityRenderState.Yaw"/> follows the existing entity convention (<c>180 - Yaw</c>).
    /// Existing entity collision and squish scaling are ignored; <paramref name="modelScale"/> is
    /// the model's own unit scale (the default is the existing entity scale used by <see cref="Build"/>).
    /// </remarks>
    /// <param name="model">Baked model definition to draw.</param>
    /// <param name="state">Block entity position and yaw.</param>
    /// <param name="sprite">Atlas rect used for the model's faces.</param>
    /// <param name="textureWidth">Declared model texture width in pixels.</param>
    /// <param name="textureHeight">Declared model texture height in pixels.</param>
    /// <param name="pose">Optional static pose overrides; no entity animation is applied.</param>
    /// <param name="modelScale">Model unit scale; 1 maps model pixels to 1/16 block (defaults to the entity scale used by <see cref="Build"/>).</param>
    public DynamicGeometry BuildModel(
        EntityModelDefinition model,
        EntityRenderState state,
        SpriteRect sprite,
        int textureWidth,
        int textureHeight,
        EntityPose? pose = null,
        float modelScale = EntityPlaceholderMesher.ModelScale)
    {
        _singleState = state;
        return BuildModel(model, MemoryMarshal.CreateReadOnlySpan(ref _singleState, 1), sprite, textureWidth, textureHeight, pose, modelScale);
    }

    /// <summary>Builds a batch of block entity instances from one model definition.</summary>
    /// <param name="model">Baked model definition to draw.</param>
    /// <param name="states">Block entity positions and yaws.</param>
    /// <param name="sprite">Atlas rect used for the model's faces.</param>
    /// <param name="textureWidth">Declared model texture width in pixels.</param>
    /// <param name="textureHeight">Declared model texture height in pixels.</param>
    /// <param name="pose">Optional static pose overrides; no entity animation is applied.</param>
    /// <param name="modelScale">Model unit scale; 1 maps model pixels to 1/16 block (defaults to the entity scale used by <see cref="Build"/>).</param>
    public DynamicGeometry BuildModel(
        EntityModelDefinition model,
        ReadOnlySpan<EntityRenderState> states,
        SpriteRect sprite,
        int textureWidth,
        int textureHeight,
        EntityPose? pose = null,
        float modelScale = EntityPlaceholderMesher.ModelScale)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(textureWidth, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(textureHeight, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(modelScale, 0f);

        return BuildInto(
            _geometry,
            kindFilter: null,
            states,
            sprite,
            textureWidth,
            textureHeight,
            modelOverride: model,
            pose: pose,
            modelScale: modelScale,
            entityTransformFactory: BlockEntityTransformFactory,
            textureWidthOverride: textureWidth,
            textureHeightOverride: textureHeight);
    }

    private static readonly Func<EntityRenderState, EntityModelTransform> BlockEntityTransformFactory = BlockEntityTransform;

    /// <summary>
    /// Block entity transform: use the existing entity yaw convention (<c>180 - Yaw</c>) and ignore
    /// entity collision/squish scaling. The caller places the model origin at the block centre by
    /// setting X/Z to the block centre; rotation then happens about that centre.
    /// </summary>
    private static EntityModelTransform BlockEntityTransform(EntityRenderState state) =>
        new(
            Translation: new Vector3(state.X, state.Y, state.Z),
            YawDegrees: 180f - state.Yaw,
            PitchDegrees: 0f);

    /// <summary>
    /// Builds one geometry per reference render type, in draw order (cutout, translucent, emissive).
    /// </summary>
    public IReadOnlyList<EntityGeometryLayer> BuildLayers(ReadOnlySpan<EntityRenderState> entities, SpriteRect skin, int skinWidth, int skinHeight, IReadOnlyDictionary<string, FaceUvTransform>? transforms = null, string? onlyPart = null, string? excludedPart = null, ISpriteAtlas? skinAtlas = null, EntityVariantTable? variants = null, EntityPose? pose = null, ISpriteAtlas? itemAtlas = null)
    {
        // Called once per frame. Without an explicit (sandbox) pose, build one pose per entity here, before the three
        // layer passes, so provider state updates run once per entity rather than once per layer.
        var usePreparedPoses = pose is null;
        if (usePreparedPoses)
        {
            PrepareAnimationPoses(entities);
        }

        if (HasModelBackedLayers(entities, variants))
        {
            var built = BuildModelBackedLayers(entities, skin, skinWidth, skinHeight, transforms, onlyPart, excludedPart, skinAtlas, variants, pose, usePreparedPoses, itemAtlas);
            {
            }

            return built;
        }

        var plan = BuildLayerPlan(entities, variants);
        while (_layerGeometries.Count < plan.Count)
        {
            _layerGeometries.Add(new DynamicGeometry());
        }

        var layers = new List<EntityGeometryLayer>(plan.Count);
        for (var i = 0; i < plan.Count; i++)
        {
            var definition = plan[i];
            var kind = definition.Kind;
            var geometry = _layerGeometries[i];
            var emissivePass = kind == EntityGeometryLayerKind.Emissive;
            var layerAtlas = definition.UsesItemAtlas ? itemAtlas : skinAtlas;
            BuildInto(geometry, kind, entities, skin, skinWidth, skinHeight, transforms, onlyPart, excludedPart, layerAtlas, variants, emissivePass, pose, usePreparedPoses);
            if (geometry.IndexCount > 0)
            {
                layers.Add(new EntityGeometryLayer(definition.Name, kind, definition.Order, geometry, definition.UsesItemAtlas));
            }
        }

        return layers;
    }

    /// <summary>
    /// Builds the batch's layer plan from every model's explicit render-layer definitions. Duplicate kinds collapse to
    /// the earliest declared order, then the plan is sorted by <see cref="EntityRenderLayerDefinition.Order"/>.
    /// </summary>
    private static List<EntityRenderLayerDefinition> BuildLayerPlan(
        ReadOnlySpan<EntityRenderState> entities,
        EntityVariantTable? variants)
    {
        var byKind = new Dictionary<EntityGeometryLayerKind, EntityRenderLayerDefinition>();
        foreach (var entity in entities)
        {
            var model = ResolveEntityModel(entity, variants);
            foreach (var layer in model.RenderLayers)
            {
                if (!byKind.TryGetValue(layer.Kind, out var existing) || layer.Order < existing.Order)
                {
                    byKind[layer.Kind] = layer;
                }
            }
        }

        var plan = new List<EntityRenderLayerDefinition>(byKind.Count);
        plan.AddRange(byKind.Values);
        plan.Sort(static (left, right) => left.Order.CompareTo(right.Order));
        return plan;
    }

    private bool HasModelBackedLayers(ReadOnlySpan<EntityRenderState> entities, EntityVariantTable? variants)
    {
        foreach (var entity in entities)
        {
            foreach (var layer in ResolveEntityModel(entity, variants).RenderLayers)
            {
                if (layer.Model is not null)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Builds every model-backed layer with its own model/texture. Entities resolving to the same effective layer
    /// model (and the same BuildInto settings) are meshed in one call and appended once, rather than one scratch
    /// build/append per entity. The plan order, per-entity visibility, poses and texture selectors are unchanged.
    /// </summary>
    private IReadOnlyList<EntityGeometryLayer> BuildModelBackedLayers(
        ReadOnlySpan<EntityRenderState> entities,
        SpriteRect skin,
        int skinWidth,
        int skinHeight,
        IReadOnlyDictionary<string, FaceUvTransform>? transforms,
        string? onlyPart,
        string? excludedPart,
        ISpriteAtlas? skinAtlas,
        EntityVariantTable? variants,
        EntityPose? pose,
        bool usePreparedPoses,
        ISpriteAtlas? itemAtlas)
    {
        _modelBackedPlanByKey.Clear();
        foreach (var entity in entities)
        {
            foreach (var layer in ResolveEntityModel(entity, variants).RenderLayers)
            {
                _modelBackedPlanByKey[(layer.Name, layer.Kind, layer.Order)] = layer;
            }
        }

        _modelBackedPlan.Clear();
        _modelBackedPlan.AddRange(_modelBackedPlanByKey.Values);
        _modelBackedPlan.Sort(static (left, right) =>
        {
            var order = left.Order.CompareTo(right.Order);
            return order != 0 ? order : string.CompareOrdinal(left.Name, right.Name);
        });
        var plan = _modelBackedPlan;
        while (_layerGeometries.Count < plan.Count)
        {
            _layerGeometries.Add(new DynamicGeometry());
            _layerScratch.Add(new DynamicGeometry());
        }

        var layers = new List<EntityGeometryLayer>(plan.Count);
        for (var layerIndex = 0; layerIndex < plan.Count; layerIndex++)
        {
            var definition = plan[layerIndex];
            var target = _layerGeometries[layerIndex];
            var scratch = _layerScratch[layerIndex];
            target.Clear();

            // One grouping pass for this layer. Groups are contiguous runs only: merging non-adjacent entities with
            // the same model would reorder vertex data, which is not equivalent to the reference's entity submission
            // order (and matters for translucent ties). Buffers are pooled fields cleared in place.
            var groupCount = 0;
            var activeGroup = -1;
            var activeKey = default(LayerGroupKey);
            for (var entityIndex = 0; entityIndex < entities.Length; entityIndex++)
            {
                var entity = entities[entityIndex];
                var model = ResolveEntityModel(entity, variants);
                EntityRenderLayerDefinition? entityLayer = null;
                foreach (var candidate in model.RenderLayers)
                {
                    if (candidate.Name == definition.Name && candidate.Kind == definition.Kind && candidate.Order == definition.Order)
                    {
                        entityLayer = candidate;
                        break;
                    }
                }

                if (entityLayer is null || (entityLayer.VisibleWhen is not null && !entityLayer.VisibleWhen(entity)))
                {
                    continue;
                }

                var effectiveModel = entityLayer.Model ?? model;
                var key = new LayerGroupKey(effectiveModel, entityLayer.AllCubes, entityLayer.TextureSelector);
                if (activeGroup < 0 || !key.Equals(activeKey))
                {
                    activeGroup = groupCount++;
                    activeKey = key;
                    while (_layerGroups.Count <= activeGroup)
                    {
                        _layerGroups.Add(new LayerGroup());
                    }

                    var created = _layerGroups[activeGroup];
                    created.Model = effectiveModel;
                    created.Layer = entityLayer;
                    created.EntityIndices.Clear();
                }

                _layerGroups[activeGroup].EntityIndices.Add(entityIndex);
            }

            for (var groupIndex = 0; groupIndex < groupCount; groupIndex++)
            {
                var group = _layerGroups[groupIndex];
                _groupEntities.Clear();
                _groupPoses.Clear();
                foreach (var entityIndex in group.EntityIndices)
                {
                    _groupEntities.Add(entities[entityIndex]);
                    _groupPoses.Add(usePreparedPoses ? _animationPoses[entityIndex] : pose);
                }

                BuildInto(
                    scratch,
                    group.Layer.AllCubes ? null : group.Layer.Kind,
                    CollectionsMarshal.AsSpan(_groupEntities),
                    skin,
                    skinWidth,
                    skinHeight,
                    transforms,
                    onlyPart,
                    excludedPart,
                    group.Layer.UsesItemAtlas ? itemAtlas : skinAtlas,
                    variants,
                    group.Layer.Kind == EntityGeometryLayerKind.Emissive,
                    usePreparedPoses: false,
                    modelOverride: group.Model,
                    layerTextureSelector: group.Layer.TextureSelector,
                    poses: _groupPoses);
                AppendGeometry(target, scratch);
            }

            if (target.IndexCount > 0)
            {
                layers.Add(new EntityGeometryLayer(definition.Name, definition.Kind, definition.Order, target, definition.UsesItemAtlas));
            }
        }

        return layers;
    }

    private static void AppendGeometry(DynamicGeometry target, DynamicGeometry source)
    {
        var neededVertices = target.VertexFloats + source.VertexFloats;
        if (target.Vertices.Length < neededVertices)
        {
            var vertices = new float[Math.Max(neededVertices, target.Vertices.Length * 2)];
            Array.Copy(target.Vertices, vertices, target.VertexFloats);
            target.Vertices = vertices;
        }

        var neededIndices = target.IndexCount + source.IndexCount;
        if (target.Indices.Length < neededIndices)
        {
            var indices = new uint[Math.Max(neededIndices, target.Indices.Length * 2)];
            Array.Copy(target.Indices, indices, target.IndexCount);
            target.Indices = indices;
        }

        var vertexBase = (uint)(target.VertexFloats / ChunkMesher.FloatsPerVertex);
        Array.Copy(source.Vertices, 0, target.Vertices, target.VertexFloats, source.VertexFloats);
        for (var i = 0; i < source.IndexCount; i++)
        {
            target.Indices[target.IndexCount + i] = source.Indices[i] + vertexBase;
        }

        target.VertexFloats = neededVertices;
        target.IndexCount = neededIndices;
    }

    /// <summary>

    private static bool HasEmissive(EntityModelDefinition model)
    {
        if (model.EmissiveTexturePath is { Length: > 0 })
        {
            return true;
        }

        foreach (var part in model.Parts)
        {
            if (part.EmissiveTexturePath is { Length: > 0 })
            {
                return true;
            }
        }

        if (model.Compiled is { } compiled)
        {
            foreach (var cube in compiled.Cubes)
            {
                if (cube.EmissiveTexturePath is { Length: > 0 })
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Emits the six faces in the reference <c>ModelPart.Cube</c> order. The UV corners below are the exact
    /// Polygon constructor values; no per-face rectangle flip or 180-degree rotation is applied.
    /// </summary>
    private void AddPart(
        EntityModelPart part,
        DynamicGeometry target,
        EntityGeometryLayerKind? kindFilter,
        bool emissivePass,
        Vector3 min,
        Vector3 max,
        SpriteRect skin,
        int skinWidth,
        int skinHeight,
        Vector3 feet,
        EntityModelTransform entityTransform,
        Matrix4x4 partMatrix,
        IReadOnlyDictionary<string, FaceUvTransform>? transforms,
        IReadOnlyDictionary<string, FaceUvTransform> defaultTransforms,
        EntityRenderState renderState,
        ISpriteAtlas? skinAtlas,
        float px,
        ref int vertexFloats,
        ref int indexCount,
        bool rawSourceFrame = false)
    {
        var d = part.UvDepth > 0 ? part.UvDepth : part.Depth;
        var w = part.UvWidth > 0 ? part.UvWidth : part.Width;
        var h = part.UvHeight > 0 ? part.UvHeight : part.Height;
        var tx = part.TextureX;
        var ty = part.TextureY;

        var u0 = tx;
        var u1 = tx + d;
        var u2 = tx + d + w;
        var u22 = tx + d + (2f * w);
        var u3 = tx + d + w + d;
        var u4 = tx + d + w + d + w;
        var v0 = ty;
        var v1 = ty + d;
        var v2 = ty + d + h;

        var t0 = new Vector3(min.X, min.Y, min.Z);
        var t1 = new Vector3(max.X, min.Y, min.Z);
        var t2 = new Vector3(max.X, max.Y, min.Z);
        var t3 = new Vector3(min.X, max.Y, min.Z);
        var l0 = new Vector3(min.X, min.Y, max.Z);
        var l1 = new Vector3(max.X, min.Y, max.Z);
        var l2 = new Vector3(max.X, max.Y, max.Z);
        var l3 = new Vector3(min.X, max.Y, max.Z);
        // The part matrix already carries this part's own rotation and the whole parent chain, so the corners only
        // need the matrix and then the entity-level transform.
        Vector3 ToWorld(Vector3 point) => entityTransform.Apply(Vector3.Transform(point, partMatrix));
        t0 = ToWorld(t0); t1 = ToWorld(t1); t2 = ToWorld(t2); t3 = ToWorld(t3);
        l0 = ToWorld(l0); l1 = ToWorld(l1); l2 = ToWorld(l2); l3 = ToWorld(l3);
        FaceUvTransform T(string face)
        {
            var fallback = defaultTransforms.TryGetValue(face, out var defaultTransform)
                ? defaultTransform
                : FaceUvTransform.Identity;
            var result = transforms is not null && transforms.TryGetValue(part.Name + "." + face, out var value)
                ? value
                : fallback;
            // CubeListBuilder.mirror flips U for every face of the part.
            return part.Mirror ? result with { FlipH = !result.FlipH } : result;
        }

        var emitBase = emissivePass || kindFilter is null || part.Kind == kindFilter;
        var partTint = part.TintSelector?.Invoke(renderState)
            ?? (renderState.Tint == default ? Vector3.One : renderState.Tint);

        if (emitBase && !rawSourceFrame && part.Depth == 0f && part.Width > 0f && part.Height > 0f)
        {
            AddFlatQuad(part, target, min, max, skin, skinWidth, skinHeight, entityTransform, partMatrix, partTint, ref vertexFloats, ref indexCount);
            return;
        }
        if (emitBase && rawSourceFrame)
        {
            // Raw source faces use the reference ModelPart.Cube polygon order and UV corners. A mirrored
            // CubeListBuilder reflects the cube in X inside ModelPart.Cube (minX/maxX swap) and the
            // Polygon constructor then reverses the four vertices; both steps are replayed here.
            var sourceMinX = part.Mirror ? max.X : min.X;
            var sourceMaxX = part.Mirror ? min.X : max.X;
            var rt0 = ToWorld(new Vector3(sourceMinX, min.Y, min.Z));
            var rt1 = ToWorld(new Vector3(sourceMaxX, min.Y, min.Z));
            var rt2 = ToWorld(new Vector3(sourceMaxX, max.Y, min.Z));
            var rt3 = ToWorld(new Vector3(sourceMinX, max.Y, min.Z));
            var rl0 = ToWorld(new Vector3(sourceMinX, min.Y, max.Z));
            var rl1 = ToWorld(new Vector3(sourceMaxX, min.Y, max.Z));
            var rl2 = ToWorld(new Vector3(sourceMaxX, max.Y, max.Z));
            var rl3 = ToWorld(new Vector3(sourceMinX, max.Y, max.Z));

            var rawVertexFloats = vertexFloats;
            var rawIndexCount = indexCount;
            void EmitRawFace(Vector3 a, Vector3 b, Vector3 c, Vector3 d,
                float au, float av, float bu, float bv, float cu, float cv, float du, float dv)
            {
                if (part.Mirror)
                {
                    AddFace(d, c, b, a, du, dv, cu, cv, bu, bv, au, av,
                        skin, skinWidth, skinHeight, ref rawVertexFloats, ref rawIndexCount,
                        FaceUvTransform.Identity, partTint, target, rawUv: true);
                    return;
                }

                AddFace(a, b, c, d, au, av, bu, bv, cu, cv, du, dv,
                    skin, skinWidth, skinHeight, ref rawVertexFloats, ref rawIndexCount,
                    FaceUvTransform.Identity, partTint, target, rawUv: true);
            }

            // Face order matches ref ModelPart.Cube polygons: DOWN, UP, WEST, NORTH, EAST, SOUTH.
            // VisibleSides selects which source faces exist; all-6 (default) is byte-for-byte unchanged.
            var sides = part.VisibleSides;
            if ((sides & EntityModelVisibleSides.Down) != 0)
                EmitRawFace(rl1, rl0, rt0, rt1, u2, v0, u1, v0, u1, v1, u2, v1);
            if ((sides & EntityModelVisibleSides.Up) != 0)
                EmitRawFace(rt2, rt3, rl3, rl2, u22, v1, u2, v1, u2, v0, u22, v0);
            if ((sides & EntityModelVisibleSides.West) != 0)
                EmitRawFace(rt0, rl0, rl3, rt3, u1, v1, u0, v1, u0, v2, u1, v2);
            if ((sides & EntityModelVisibleSides.North) != 0)
                EmitRawFace(rt1, rt0, rt3, rt2, u2, v1, u1, v1, u1, v2, u2, v2);
            if ((sides & EntityModelVisibleSides.East) != 0)
                EmitRawFace(rl1, rt1, rt2, rl2, u3, v1, u2, v1, u2, v2, u3, v2);
            if ((sides & EntityModelVisibleSides.South) != 0)
                EmitRawFace(rl0, rl1, rl2, rl3, u4, v1, u3, v1, u3, v2, u4, v2);
            vertexFloats = rawVertexFloats;
            indexCount = rawIndexCount;
        }
        else if (emitBase)
        {
            var swapBodyDownUp = true;
        var d0u = swapBodyDownUp ? u22 : u2; var d0v = swapBodyDownUp ? v1 : v0;
        var d1u = swapBodyDownUp ? u2 : u1; var d1v = swapBodyDownUp ? v1 : v0;
        var d2u = swapBodyDownUp ? u2 : u1; var d2v = swapBodyDownUp ? v0 : v1;
        var d3u = swapBodyDownUp ? u22 : u2; var d3v = swapBodyDownUp ? v0 : v1;
        var u0u = swapBodyDownUp ? u2 : u22; var u0v = swapBodyDownUp ? v0 : v1;
        var u1u = swapBodyDownUp ? u1 : u2; var u1v = swapBodyDownUp ? v0 : v1;
        var u2u = swapBodyDownUp ? u1 : u2; var u2v = swapBodyDownUp ? v1 : v0;
        var u3u = swapBodyDownUp ? u2 : u22; var u3v = swapBodyDownUp ? v1 : v0;

        // The reference CubeListBuilder.mirror() reflects the whole cube in X, which also swaps which
        // texture chunk the WEST and EAST faces sample. Our model space is already X-mirrored relative to
        // the reference, so for a non-mirrored part the two differences cancel (WEST<->EAST u-range). For a
        // mirrored part the reference reflection cancels our own, leaving the native assignment, so the two
        // X-normal faces must sample the opposite side's u-range (u0..u1 <-> u2..u3). DOWN/UP/NORTH/SOUTH
        // only flip horizontally and need no such swap.
        var westU0 = part.Mirror ? u1 : u3;
        var westU1 = part.Mirror ? u0 : u2;
        var westU2 = part.Mirror ? u0 : u2;
        var westU3 = part.Mirror ? u1 : u3;
        var eastU0 = part.Mirror ? u3 : u1;
        var eastU1 = part.Mirror ? u2 : u0;
        var eastU2 = part.Mirror ? u2 : u0;
        var eastU3 = part.Mirror ? u3 : u1;

        if (emitBase)
        {
        AddFace(l1, l0, t0, t1, d0u, d0v, d1u, d1v, d2u, d2v, d3u, d3v, skin, skinWidth, skinHeight, ref vertexFloats, ref indexCount, T("DOWN"), partTint, target); // DOWN
        AddFace(t2, t3, l3, l2, u0u, u0v, u1u, u1v, u2u, u2v, u3u, u3v, skin, skinWidth, skinHeight, ref vertexFloats, ref indexCount, T("UP"), partTint, target); // UP
        AddFace(t0, l0, l3, t3, westU0, v2, westU1, v2, westU2, v1, westU3, v1, skin, skinWidth, skinHeight, ref vertexFloats, ref indexCount, T("WEST"), partTint, target); // WEST
        AddFace(t1, t0, t3, t2, u2, v2, u1, v2, u1, v1, u2, v1, skin, skinWidth, skinHeight, ref vertexFloats, ref indexCount, T("NORTH"), partTint, target); // NORTH
        AddFace(l1, t1, t2, l2, eastU0, v2, eastU1, v2, eastU2, v1, eastU3, v1, skin, skinWidth, skinHeight, ref vertexFloats, ref indexCount, T("EAST"), partTint, target); // EAST
        AddFace(l0, l1, l2, l3, u4, v2, u3, v2, u3, v1, u4, v1, skin, skinWidth, skinHeight, ref vertexFloats, ref indexCount, T("SOUTH"), partTint, target); // SOUTH
        }
        }


        if (!emissivePass && part.OverlayLayers is not null)
        {
            foreach (var layer in part.OverlayLayers)
            {
                if (!LayerVisible(layer.Name, renderState))
                {
                    continue;
                }

                if (kindFilter is not null && layer.Kind != kindFilter)
                {
                    continue;
                }

                var grow = layer.Grow * px;
                var overlay = new EntityModelPart(
                    part.Name,
                    part.CentreX,
                    part.Bottom,
                    part.Width,
                    part.Height,
                    part.Depth,
                    layer.TextureX,
                    layer.TextureY,
                    layer.Mirror || part.Mirror,
                    null,
                    part.CentreZ,
                    part.PivotX,
                    part.PivotY,
                    part.PivotZ,
                    part.RotationX,
                    part.RotationY,
                    part.RotationZ,
                    Kind: layer.Kind);
                var overlayMin = new Vector3(min.X - grow, min.Y - grow, min.Z - grow);
                var overlayMax = new Vector3(max.X + grow, max.Y + grow, max.Z + grow);
                var overlaySkin = skin;
                var overlayWidth = skinWidth;
                var overlayHeight = skinHeight;
                if (layer.TexturePath is { Length: > 0 } layerPath && skinAtlas?.Get(layerPath) is { } layerSprite)
                {
                    overlaySkin = layerSprite;
                    // UVs are authored in the model/part texture grid. Use the declared dimensions, never the PNG
                    // resolution: an HD texture with the same layout must not shift overlay UVs.
                    overlayWidth = skinWidth;
                    overlayHeight = skinHeight;
                }

                AddPart(overlay, target, kindFilter, emissivePass, overlayMin, overlayMax, overlaySkin, overlayWidth, overlayHeight, feet, entityTransform, partMatrix, transforms, defaultTransforms, renderState, skinAtlas, px, ref vertexFloats, ref indexCount, rawSourceFrame);
            }
        }
    }

    /// Tree-path renderer: walks the structural model, computes one matrix per node, emits each node's cube range and
    /// inherits each node's visible predicate to its whole subtree.
    /// </summary>


    private void BuildCompiledEntity(
        CompiledEntityModel compiled,
        DynamicGeometry target,
        EntityGeometryLayerKind? kindFilter,
        EntityRenderState entity,
        EntityPose? entityPose,
        float px,
        SpriteRect sprite,
        int skinWidth,
        int skinHeight,
        EntityModelTransform entityTransform,
        IReadOnlyDictionary<string, FaceUvTransform>? transforms,
        IReadOnlyDictionary<string, FaceUvTransform> defaultTransforms,
        string? modelEmissivePath,
        string? onlyPart,
        string? excludedPart,
        ISpriteAtlas? skinAtlas,
        bool emissivePass,
        bool applyRootPose,
        ref int vertexFloats,
        ref int indexCount,
        System.Numerics.Matrix4x4 localTransform = default,
        bool rawSourceFrame = false,
        bool rawPoseConjugation = false)
    {
        var vertexFloatsLocal = vertexFloats;
        var indexCountLocal = indexCount;

        void Emit(EntityModelPartNode node, Matrix4x4 parentFrame)
        {
            if (node.VisibleWhen is not null && !node.VisibleWhen(entity))
            {
                return;
            }

            var frame = NodeFrame(node, parentFrame, entity, entityPose, px, rawSourceFrame, rawPoseConjugation);
            var yOffset = node.YOffsetSelector?.Invoke(entity) ?? 0f;

            var basePivot = new Vector3(node.PivotX * px, (node.PivotY + yOffset) * px, node.PivotZ * px);
            var partMatrix = node.Parent is null
                ? Matrix4x4.CreateTranslation(-basePivot) * frame
                : frame;
            if (localTransform != default)
            {
                // The layer's constant transform acts in the geometry's own space, before the part frame.
                partMatrix = localTransform * partMatrix;
            }

            var cachedParts = node.PartsFor(node.CubeArray.Length);
            for (var cubeIndex = 0; cubeIndex < node.CubeArray.Length; cubeIndex++)
            {
                var cube = node.CubeArray[cubeIndex];
                if (cube.Width == 0f && cube.Height == 0f && cube.Depth == 0f)
                {
                    continue;
                }

                if (emissivePass
                    ? (!cube.IncludeInEmissive || (cube.EmissiveTexturePath is null && modelEmissivePath is null))
                    : cube.EmissiveOnly)
                {
                    continue;
                }

                if (onlyPart is not null && !string.Equals(cube.Name, onlyPart, StringComparison.Ordinal))
                {
                    continue;
                }

                if (excludedPart is not null && string.Equals(cube.Name, excludedPart, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!entity.HasChest && (cube.Name == "right_chest" || cube.Name == "left_chest"))
                {
                    continue;
                }

                if (!entity.HasEgg && cube.Name == "egg_belly")
                {
                    continue;
                }

                // The descriptor is rebuilt only when it does not exist yet, or when this entity shifts the part
                // vertically; every other field comes from the node and its cube, which never change (T-829).
                var part = cachedParts[cubeIndex];
                if (part.Name is null || yOffset != 0f)
                {
                    part = new EntityModelPart(
                        Name: cube.Name,
                        CentreX: cube.CentreX,
                        Bottom: cube.Bottom + yOffset,
                    Width: cube.Width,
                    Height: cube.Height,
                    Depth: cube.Depth,
                    TextureX: cube.TextureX,
                    TextureY: cube.TextureY,
                    Mirror: cube.Mirror,
                    OverlayLayers: cube.Overlays,
                    CentreZ: cube.CentreZ,
                    PivotX: node.PivotX,
                    PivotY: node.PivotY + yOffset,
                    PivotZ: node.PivotZ,
                    RotationX: node.RotationX,
                    RotationY: node.RotationY,
                    RotationZ: node.RotationZ,
                    TexturePath: cube.TexturePath,
                    Grow: cube.Grow,
                    Parent: node.Parent,
                    Kind: cube.Kind,
                    VisibleWhen: node.VisibleWhen,
                    TextureWidth: cube.TextureWidth,
                    TextureHeight: cube.TextureHeight,
                    IncludeInEmissive: cube.IncludeInEmissive,
                    EmissiveOnly: cube.EmissiveOnly,
                    EmissiveTexturePath: cube.EmissiveTexturePath,
                    GrowXyz: cube.GrowXyz,
                    TintSelector: node.TintSelector,
                    UvWidth: cube.UvWidth,
                    UvHeight: cube.UvHeight,
                    UvDepth: cube.UvDepth);

                    if (yOffset == 0f)
                    {
                        cachedParts[cubeIndex] = part;
                    }
                }

                var centreX = cube.CentreX * px;
                var centreZ = cube.CentreZ * px;
                var bottom = (cube.Bottom + yOffset) * px;
                var halfW = cube.Width * px * 0.5f;
                var halfD = cube.Depth * px * 0.5f;
                var grow = cube.Grow * px;
                var growX = cube.GrowXyz is { } gx ? gx.X * px : grow;
                var growY = cube.GrowXyz is { } gy ? gy.Y * px : grow;
                var growZ = cube.GrowXyz is { } gz ? gz.Z * px : grow;
                var min = new Vector3(centreX - halfW - growX, bottom - growY, centreZ - halfD - growZ);
                var max = new Vector3(centreX + halfW + growX, bottom + (cube.Height * px) + growY, centreZ + halfD + growZ);

                var partTextureWidth = cube.TextureWidth > 0 ? cube.TextureWidth : skinWidth;
                var partTextureHeight = cube.TextureHeight > 0 ? cube.TextureHeight : skinHeight;
                var partSprite = sprite;
                if (emissivePass
                    && cube.EmissiveTexturePath is { Length: > 0 } partEmissivePath
                    && skinAtlas?.Get(partEmissivePath) is { } partEmissiveSprite)
                {
                    partSprite = partEmissiveSprite;
                }

                var selectedPartPath = cube.TextureSelector?.Invoke(entity) ?? cube.TexturePath;
                if (!emissivePass
                    && selectedPartPath is { Length: > 0 } partPath
                    && skinAtlas?.Get(partPath) is { } partAtlas)
                {
                    partSprite = partAtlas;
                }

                AddPart(
                    part,
                    target,
                    kindFilter,
                    emissivePass,
                    min,
                    max,
                    partSprite,
                    partTextureWidth,
                    partTextureHeight,
                    Vector3.Zero,
                    entityTransform,
                    partMatrix,
                    transforms,
                    defaultTransforms,
                    entity,
                    skinAtlas,
                    px,
                    ref vertexFloatsLocal,
                    ref indexCountLocal,
                    rawSourceFrame: rawSourceFrame);
            }

            foreach (var child in node.ChildArray)
            {
                Emit(child, frame);
            }
        }

        var rootPose = applyRootPose
            ? entityPose?.Get(compiled.Root.Name) ?? PoseOffset.Zero
            : PoseOffset.Zero;
        var rootFrame = Matrix4x4.CreateScale(new Vector3(
                1f + rootPose.Scale.X,
                1f + rootPose.Scale.Y,
                1f + rootPose.Scale.Z))
            * (rawSourceFrame
                ? RotationZyx(rootPose.Rotation.X, rootPose.Rotation.Y, rootPose.Rotation.Z)
                : RotationZyx(-rootPose.Rotation.X, -rootPose.Rotation.Y, rootPose.Rotation.Z))
            * Matrix4x4.CreateTranslation(rootPose.Position * px);

        foreach (var child in compiled.Root.ChildArray)
        {
            Emit(child, rootFrame);
        }

        vertexFloats = vertexFloatsLocal;
        indexCount = indexCountLocal;
    }
}
