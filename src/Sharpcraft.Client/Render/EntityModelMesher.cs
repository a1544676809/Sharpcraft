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
                _geometry.Indices[indexOffset + i] = (uint)(geometry.Indices[i] + (vertexBase / 14));
            }

            vertexBase += geometry.VertexFloats;
            indexOffset += geometry.IndexCount;
        }

        _geometry.VertexFloats = totalVertexFloats;
        _geometry.IndexCount = totalIndexCount;
        return _geometry;
    }

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
            return BuildModelBackedLayers(entities, skin, skinWidth, skinHeight, transforms, onlyPart, excludedPart, skinAtlas, variants, pose, usePreparedPoses, itemAtlas);
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

        var vertexBase = (uint)(target.VertexFloats / 14);
        Array.Copy(source.Vertices, 0, target.Vertices, target.VertexFloats, source.VertexFloats);
        for (var i = 0; i < source.IndexCount; i++)
        {
            target.Indices[target.IndexCount + i] = source.Indices[i] + vertexBase;
        }

        target.VertexFloats = neededVertices;
        target.IndexCount = neededIndices;
    }

    /// <summary>
    /// Fills <see cref="_animationPoses"/> for the current batch: one reused pose per entity, cleared and rebuilt from
    /// the registry provider. Runs once per frame; allocates only when the batch grows.
    /// </summary>
    private void PrepareAnimationPoses(ReadOnlySpan<EntityRenderState> entities)
    {
        while (_animationPoses.Count < entities.Length)
        {
            _animationPoses.Add(new EntityPose());
        }

        for (var i = 0; i < entities.Length; i++)
        {
            var pose = _animationPoses[i];
            pose.Clear();
            var entity = entities[i];
            // STAB swings pull the held item forward regardless of whether the entity has an animation provider, so the
            // spear offset is applied before the early-out below.
            SpearAnimations.ApplyThirdPersonAttackItem(entity.SwingAnimation, entity.SwingType, 0f, pose);
            if (entity.Animation is null || !EntityAnimationRegistry.TryGet(entity.TypeId, out var provider))
            {
                continue;
            }

            var frame = EntityAnimationFrame.From(entity);
            provider.Apply(entity.Animation, in frame, pose);
        }
    }

    private DynamicGeometry BuildInto(DynamicGeometry target, EntityGeometryLayerKind? kindFilter, ReadOnlySpan<EntityRenderState> entities, SpriteRect skin, int skinWidth, int skinHeight, IReadOnlyDictionary<string, FaceUvTransform>? transforms = null, string? onlyPart = null, string? excludedPart = null, ISpriteAtlas? skinAtlas = null, EntityVariantTable? variants = null, bool emissivePass = false, EntityPose? pose = null, bool usePreparedPoses = false, EntityModelDefinition? modelOverride = null, Func<EntityRenderState, string?>? layerTextureSelector = null, Func<EntityRenderState, bool>? entityFilter = null, IReadOnlyList<EntityPose?>? poses = null)
    {
        target.Clear();
        var cubeCount = 0;
        foreach (var entity in entities)
        {
            if (entityFilter is not null && !entityFilter(entity))
            {
                continue;
            }

            var model = modelOverride ?? ResolveEntityModel(entity, variants);
            if (model.Compiled is { } compiled)
            {
                foreach (var cube in compiled.Cubes)
                {
                    cubeCount += 1 + (cube.Overlays?.Count ?? 0);
                }

                continue;
            }

            foreach (var part in model.Parts)
            {
                cubeCount += 1 + (part.OverlayLayers?.Count ?? 0);
            }
        }

        target.Reserve((cubeCount + entities.Length * 2) * 24 * 14, (cubeCount + entities.Length * 2) * 36);
        var vertexFloats = 0;
        var indexCount = 0;

        for (var entityIndex = 0; entityIndex < entities.Length; entityIndex++)
        {
            var entity = entities[entityIndex];
            if (entityFilter is not null && !entityFilter(entity))
            {
                continue;
            }

            // Grouped batches carry their own pose list indexed by the compact span; otherwise use the per-batch
            // prepared poses (indexed by the original entity order) or one explicit pose for the whole span.
            var entityPose = poses is not null ? poses[entityIndex] : usePreparedPoses ? _animationPoses[entityIndex] : pose;
            var model = modelOverride ?? ResolveEntityModel(entity, variants);
            if (model is null)
            {
                continue;
            }

            if (emissivePass && !HasEmissive(model))
            {
                continue;
            }

            var px = (1f / 16f) * EntityPlaceholderMesher.ModelScale * model.Scale;
            var feet = Vector3.Zero;
            var entityTransform = EntityModelTransform.From(entity);
            var sprite = skin;
            // UVs are authored against the model's declared texture size, not the image resolution (e.g. ghast declares
            // 64x32 while its PNG is 128x64). Normalising by the image size would sample the wrong texels.
            var skinWidthForModel = model.TextureWidth;
            var skinHeightForModel = model.TextureHeight;
            if (emissivePass)
            {
                if (model.EmissiveTexturePath is { Length: > 0 } emissivePath)
                {
                    if (skinAtlas?.Get(emissivePath) is not { } eyeSprite)
                    {
                        continue;
                    }

                    sprite = eyeSprite;
                }
            }
            else if (skinAtlas?.Get(layerTextureSelector?.Invoke(entity) ?? model.TextureSelector?.Invoke(entity) ?? model.TexturePath) is { } atlasSprite)
            {
                sprite = atlasSprite;
            }

            if (model.Compiled is { } compiledModel)
            {
                BuildCompiledEntity(
                    compiledModel,
                    target,
                    kindFilter,
                    entity,
                    entityPose,
                    px,
                    sprite,
                    skinWidthForModel,
                    skinHeightForModel,
                    entityTransform,
                    transforms,
                    model.FaceTransforms,
                    model.EmissiveTexturePath,
                    onlyPart,
                    excludedPart,
                    skinAtlas,
                    emissivePass,
                    model.ApplyRootPose,
                    ref vertexFloats,
                    ref indexCount,
                    model.LocalTransform);
                continue;
            }

            foreach (var originalPart in model.Parts)
            {
                var part = originalPart;
                if (part.VisibleWhen is not null && !part.VisibleWhen(entity))
                {
                    continue;
                }

                if (emissivePass ? (!part.IncludeInEmissive || (part.EmissiveTexturePath is null && model.EmissiveTexturePath is null)) : part.EmissiveOnly)
                {
                    continue;
                }

                if (part.RotationSelector is { } rotationSelector)
                {
                    var selectedRotation = rotationSelector(entity);
                    part = part.With(
                        RotationX: selectedRotation.X,
                        RotationY: selectedRotation.Y,
                        RotationZ: selectedRotation.Z);
                }

                if (part.YOffsetSelector is { } yOffsetSelector)
                {
                    var yOffset = yOffsetSelector(entity);
                    part = part.With(
                        Bottom: part.Bottom + yOffset,
                        PivotY: part.PivotY + yOffset);
                }

                var partTextureWidth = part.TextureWidth > 0 ? part.TextureWidth : model.TextureWidth;
                var partTextureHeight = part.TextureHeight > 0 ? part.TextureHeight : model.TextureHeight;

                var centreX = part.CentreX * px;
                var centreZ = part.CentreZ * px;
                var bottom = part.Bottom * px;
                var halfW = part.Width * px * 0.5f;
                var halfD = part.Depth * px * 0.5f;
                var grow = part.Grow * px;
                var growX = part.GrowXyz is { } gx ? gx.X * px : grow;
                var growY = part.GrowXyz is { } gy ? gy.Y * px : grow;
                var growZ = part.GrowXyz is { } gz ? gz.Z * px : grow;
                var min = new Vector3(feet.X + centreX - halfW - growX, feet.Y + bottom - growY, feet.Z + centreZ - halfD - growZ);
                var max = new Vector3(feet.X + centreX + halfW + growX, feet.Y + bottom + (part.Height * px) + growY, feet.Z + centreZ + halfD + growZ);
                if (onlyPart is not null && part.Name != onlyPart) continue;
                if (excludedPart is not null && part.Name == excludedPart) continue;
                if (!entity.HasChest && (part.Name == "right_chest" || part.Name == "left_chest")) continue;
                if (!entity.HasEgg && part.Name == "egg_belly") continue;
                if (part.Width == 0f && part.Height == 0f && part.Depth == 0f)
                {
                    // Group node (the reference has empty ModelParts such as wolf "head"/"tail"); it only exists to
                    // carry a pivot for its children.
                    continue;
                }

                var partSprite = sprite;
                var partSkinWidth = partTextureWidth;
                var partSkinHeight = partTextureHeight;
                if (emissivePass && part.EmissiveTexturePath is { Length: > 0 } partEmissivePath && skinAtlas?.Get(partEmissivePath) is { } partEmissiveSprite)
                {
                    partSprite = partEmissiveSprite;
                }
                var selectedPartPath = part.TextureSelector?.Invoke(entity) ?? part.TexturePath;
                if (!emissivePass && selectedPartPath is { Length: > 0 } partPath && skinAtlas?.Get(partPath) is { } partAtlas)
                {
                    partSprite = partAtlas;
                }

                AddPart(part, target, kindFilter, emissivePass, min, max, partSprite, partSkinWidth, partSkinHeight, feet, entityTransform, PartMatrix(part, model.Parts, px, entityPose), transforms, model.FaceTransforms, entity, skinAtlas, px, ref vertexFloats, ref indexCount);
            }
        }

        target.VertexFloats = vertexFloats;
        target.IndexCount = indexCount;
        return target;
    }

    public static EntityModelDefinition ResolveEntityModel(EntityRenderState entity, EntityVariantTable? variants)
    {
        // The player profile's slim flag selects the 3px-arm model before any registry-variant lookup; all other
        // entities (and wide players) keep the existing resolution path unchanged.
        if (entity.Slim && entity.TypeId == EntityPlaceholderMesher.PlayerEntityTypeId)
        {
            return EntityModelResolver.ResolvePlayerModel(slim: true);
        }

        return variants is null
            ? EntityModelResolver.Resolve(entity.TypeId, entity.IsBaby)
            : variants.Resolve(entity.TypeId, entity.VariantId, entity.IsBaby);
    }

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
        ref int indexCount)
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

        if (emitBase && part.Depth == 0f && part.Width > 0f && part.Height > 0f)
        {
            AddFlatQuad(part, target, min, max, skin, skinWidth, skinHeight, entityTransform, partMatrix, partTint, ref vertexFloats, ref indexCount);
            return;
        }
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

                AddPart(overlay, target, kindFilter, emissivePass, overlayMin, overlayMax, overlaySkin, overlayWidth, overlayHeight, feet, entityTransform, partMatrix, transforms, defaultTransforms, renderState, skinAtlas, px, ref vertexFloats, ref indexCount);
            }
        }
    }

    private static string PosePartName(EntityModelPart part)
    {
        if (part.PoseSource is { Length: > 0 } source)
        {
            return source;
        }

        var name = part.Name;
        if (!name.StartsWith("equipment_", StringComparison.Ordinal))
        {
            return name;
        }

        var remainder = name.StartsWith("equipment_extra_", StringComparison.Ordinal)
            ? name["equipment_extra_".Length..]
            : name["equipment_".Length..];
        var first = remainder.IndexOf('_');
        if (first < 0)
        {
            return name;
        }

        remainder = remainder[(first + 1)..];
        var second = remainder.IndexOf('_');
        return second < 0 ? name : remainder[(second + 1)..];
    }
    /// <summary>
    /// Frame matrix of one node, given its parent frame. Shared by the geometry walk and by
    /// <see cref="PartWorldMatrix"/> so the two can never drift apart.
    /// </summary>
    private static Matrix4x4 NodeFrame(
        EntityModelPartNode node,
        Matrix4x4 parentFrame,
        EntityRenderState entity,
        EntityPose? entityPose,
        float px)
    {
        var poseName = node.PoseSource ?? node.Name;
        var poseOffset = entityPose?.Get(poseName) ?? PoseOffset.Zero;
        var rotation = new Vector3(node.RotationX, node.RotationY, node.RotationZ);
        if (node.RotationSelector is { } rotationSelector)
        {
            rotation = rotationSelector(entity);
        }

        var yOffset = node.YOffsetSelector?.Invoke(entity) ?? 0f;
        var pivot = new Vector3(
            (node.PivotX + poseOffset.Position.X) * px,
            (node.PivotY + poseOffset.Position.Y + yOffset) * px,
            (node.PivotZ + poseOffset.Position.Z) * px);
        var local = Matrix4x4.CreateScale(new Vector3(
                1f + poseOffset.Scale.X,
                1f + poseOffset.Scale.Y,
                1f + poseOffset.Scale.Z))
            * RotationZyx(
                rotation.X - poseOffset.Rotation.X,
                rotation.Y - poseOffset.Rotation.Y,
                rotation.Z + poseOffset.Rotation.Z)
            * Matrix4x4.CreateTranslation(pivot);
        return local * parentFrame;
    }

    /// <summary>
    /// World matrix of a named part of a baked model, in the units the geometry path uses. Callers that must attach
    /// extra geometry to a posed part (the held-item pass hanging quads on the hand) use this instead of duplicating
    /// the frame maths.
    /// </summary>
    public static Matrix4x4 PartWorldMatrix(
        EntityModelDefinition definition,
        EntityRenderState entity,
        string partName,
        EntityPose? pose = null,
        bool applyRootPose = true)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var compiled = definition.Compiled;
        if (compiled is null)
        {
            return Matrix4x4.Identity;
        }

        var px = (1f / 16f) * EntityPlaceholderMesher.ModelScale * definition.Scale;
        var rootPose = applyRootPose ? pose?.Get(compiled.Root.Name) ?? PoseOffset.Zero : PoseOffset.Zero;
        var rootFrame = Matrix4x4.CreateScale(new Vector3(
                1f + rootPose.Scale.X,
                1f + rootPose.Scale.Y,
                1f + rootPose.Scale.Z))
            * RotationZyx(-rootPose.Rotation.X, -rootPose.Rotation.Y, rootPose.Rotation.Z)
            * Matrix4x4.CreateTranslation(rootPose.Position * px);

        Matrix4x4? found = null;
        void Walk(EntityModelPartNode node, Matrix4x4 parentFrame)
        {
            if (found is not null)
            {
                return;
            }

            var frame = NodeFrame(node, parentFrame, entity, pose, px);
            if (string.Equals(node.Name, partName, StringComparison.Ordinal))
            {
                // Return the full frame. The geometry path subtracts a root part's pivot because its cube vertices are
                // authored relative to that pivot; a caller attaching *other* geometry (the held-item pass) needs the
                // pivot in world space, and subtracting it here silently collapsed the hand frame to the model origin.
                found = frame;
                return;
            }

            foreach (var child in node.Children)
            {
                Walk(child, frame);
            }
        }

        foreach (var child in compiled.Root.Children)
        {
            Walk(child, rootFrame);
        }

        return found ?? Matrix4x4.Identity;
    }
    private static Matrix4x4 RotationZyx(float rx, float ry, float rz) =>
        Matrix4x4.CreateRotationX(rx) * Matrix4x4.CreateRotationY(ry) * Matrix4x4.CreateRotationZ(rz);

    /// <summary>
    /// Matrix that maps a point expressed relative to <paramref name="part"/>'s origin into entity space. This is the
    /// frame other parts' pivots are expressed in, not the frame a part's own cube is stored in.
    /// </summary>
    private static Matrix4x4 PartFrame(EntityModelPart part, IReadOnlyList<EntityModelPart> parts, float px, EntityPose? pose = null)
    {
        var poseOffset = pose?.Get(PosePartName(part)) ?? PoseOffset.Zero;
        var pivot = new Vector3((part.PivotX + poseOffset.Position.X) * px, (part.PivotY + poseOffset.Position.Y) * px, (part.PivotZ + poseOffset.Position.Z) * px);
        // Row-vector convention: "rotate, then translate" is Rotation * Translation; animation scale is local.
        // The model vertices are the reference model after scale(-1,-1,1) (see FromVanilla), so an animation rotation
        // transcribed from the reference model space must be conjugated by that flip: X and Y angles are negated, Z is
        // unchanged. Base (bind-pose) rotations are already stored in this space and are left untouched.
        var local = Matrix4x4.CreateScale(new Vector3(1f + poseOffset.Scale.X, 1f + poseOffset.Scale.Y, 1f + poseOffset.Scale.Z)) * RotationZyx(part.RotationX - poseOffset.Rotation.X, part.RotationY - poseOffset.Rotation.Y, part.RotationZ + poseOffset.Rotation.Z) * Matrix4x4.CreateTranslation(pivot);

        if (part.Parent is not { Length: > 0 } parentName)
        {
            return local;
        }

        foreach (var candidate in parts)
        {
            if (string.Equals(candidate.Name, parentName, StringComparison.Ordinal))
            {
                return local * PartFrame(candidate, parts, px, pose);
            }
        }

        return local;
    }

    /// <summary>
    /// Matrix that maps a part's stored cube coordinates into entity space.
    /// </summary>
    /// <remarks>
    /// Root parts keep absolute model-space cubes (from <c>FromVanilla</c>), so they are rotated about their pivot with
    /// the extra translate back. Child parts keep cubes relative to their own origin (from <c>FromVanillaChild</c>), so
    /// their frame is already the storage transform.
    /// </remarks>
    private static Matrix4x4 PartMatrix(EntityModelPart part, IReadOnlyList<EntityModelPart> parts, float px, EntityPose? pose = null)
    {
        var frame = PartFrame(part, parts, px, pose);
        if (part.Parent is { Length: > 0 })
        {
            return frame;
        }

        var pivot = new Vector3(part.PivotX * px, part.PivotY * px, part.PivotZ * px);
        return Matrix4x4.CreateTranslation(-pivot) * frame;
    }

    private static bool LayerVisible(string name, EntityRenderState state) => name switch
    {
        "hat" => state.ShowHat,
        "jacket" => state.ShowJacket,
        "left_sleeve" => state.ShowLeftSleeve,
        "right_sleeve" => state.ShowRightSleeve,
        "left_pants" => state.ShowLeftPants,
        "right_pants" => state.ShowRightPants,
        _ => true
    };

    /// <summary>Emits a zero-depth part as two-sided flat quads, used by generated item models.</summary>
    private void AddFlatQuad(
        EntityModelPart part,
        DynamicGeometry target,
        Vector3 min,
        Vector3 max,
        SpriteRect skin,
        int skinWidth,
        int skinHeight,
        EntityModelTransform entityTransform,
        Matrix4x4 partMatrix,
        Vector3 tint,
        ref int vertexFloats,
        ref int indexCount)
    {
        var z = min.Z;
        Vector3 ToWorld(Vector3 point) => entityTransform.Apply(Vector3.Transform(point, partMatrix));
        var a = ToWorld(new Vector3(min.X, min.Y, z));
        var b = ToWorld(new Vector3(max.X, min.Y, z));
        var c = ToWorld(new Vector3(max.X, max.Y, z));
        var d = ToWorld(new Vector3(min.X, max.Y, z));
        var w = part.UvWidth > 0 ? part.UvWidth : part.Width;
        var h = part.UvHeight > 0 ? part.UvHeight : part.Height;
        AddFace(a, b, c, d, 0f, 0f, w, 0f, w, h, 0f, h, skin, skinWidth, skinHeight, ref vertexFloats, ref indexCount, FaceUvTransform.Identity, tint, target);
        AddFace(d, c, b, a, 0f, 0f, w, 0f, w, h, 0f, h, skin, skinWidth, skinHeight, ref vertexFloats, ref indexCount, FaceUvTransform.Identity, tint, target);
    }

    private void AddFace(
        Vector3 a, Vector3 b, Vector3 c, Vector3 d,
        float au, float av, float bu, float bv, float cu, float cv, float du, float dv,
        SpriteRect skin, int skinWidth, int skinHeight,
        ref int vertexFloats, ref int indexCount,
        FaceUvTransform transform,
        Vector3 tint,
        DynamicGeometry target)
    {
        var uMin = MathF.Min(MathF.Min(au, bu), MathF.Min(cu, du));
        var uMax = MathF.Max(MathF.Max(au, bu), MathF.Max(cu, du));
        var vMin = MathF.Min(MathF.Min(av, bv), MathF.Min(cv, dv));
        var vMax = MathF.Max(MathF.Max(av, bv), MathF.Max(cv, dv));
        (au, av) = TransformUv(au, av, uMin, uMax, vMin, vMax, transform);
        (bu, bv) = TransformUv(bu, bv, uMin, uMax, vMin, vMax, transform);
        (cu, cv) = TransformUv(cu, cv, uMin, uMax, vMin, vMax, transform);
        (du, dv) = TransformUv(du, dv, uMin, uMax, vMin, vMax, transform);
        var first = (uint)(vertexFloats / 14);
        WriteVertex(target.Vertices, ref vertexFloats, a, au / skinWidth, av / skinHeight, skin, tint);
        WriteVertex(target.Vertices, ref vertexFloats, b, bu / skinWidth, bv / skinHeight, skin, tint);
        WriteVertex(target.Vertices, ref vertexFloats, c, cu / skinWidth, cv / skinHeight, skin, tint);
        WriteVertex(target.Vertices, ref vertexFloats, d, du / skinWidth, dv / skinHeight, skin, tint);

        target.Indices[indexCount++] = first;
        target.Indices[indexCount++] = first + 1;
        target.Indices[indexCount++] = first + 2;
        target.Indices[indexCount++] = first;
        target.Indices[indexCount++] = first + 2;
        target.Indices[indexCount++] = first + 3;
    }

    private static (float U, float V) TransformUv(float u, float v, float uMin, float uMax, float vMin, float vMax, FaceUvTransform t)
    {
        var nu = (u - uMin) / MathF.Max(uMax - uMin, 1e-6f);
        var nv = (v - vMin) / MathF.Max(vMax - vMin, 1e-6f);
        if (t.FlipH) nu = 1f - nu;
        if (t.FlipV) nv = 1f - nv;
        (nu, nv) = (t.Rotation % 360) switch
        {
            90 => (nv, 1f - nu),
            180 => (1f - nu, 1f - nv),
            270 => (1f - nv, nu),
            _ => (nu, nv)
        };
        return (uMin + nu * (uMax - uMin), vMin + nv * (vMax - vMin));
    }

    private static void WriteVertex(float[] destination, ref int offset, Vector3 world, float u, float v, SpriteRect skin, Vector3 tint)
    {
        destination[offset++] = world.X;
        destination[offset++] = world.Y;
        destination[offset++] = world.Z;
        destination[offset++] = tint.X;
        destination[offset++] = tint.Y;
        destination[offset++] = tint.Z;
        destination[offset++] = u;
        destination[offset++] = v;
        destination[offset++] = skin.U0;
        destination[offset++] = skin.V0;
        destination[offset++] = skin.U1;
        destination[offset++] = skin.V1;
        destination[offset++] = 1f;
        destination[offset++] = 0f;
    }

    /// <summary>
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
        System.Numerics.Matrix4x4 localTransform = default)
    {
        var vertexFloatsLocal = vertexFloats;
        var indexCountLocal = indexCount;

        void Emit(EntityModelPartNode node, Matrix4x4 parentFrame)
        {
            if (node.VisibleWhen is not null && !node.VisibleWhen(entity))
            {
                return;
            }

            var frame = NodeFrame(node, parentFrame, entity, entityPose, px);
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

            foreach (var cube in node.Cubes)
            {
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

                var part = new EntityModelPart(
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
                    ref indexCountLocal);
            }

            foreach (var child in node.Children)
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
            * RotationZyx(-rootPose.Rotation.X, -rootPose.Rotation.Y, rootPose.Rotation.Z)
            * Matrix4x4.CreateTranslation(rootPose.Position * px);

        foreach (var child in compiled.Root.Children)
        {
            Emit(child, rootFrame);
        }

        vertexFloats = vertexFloatsLocal;
        indexCount = indexCountLocal;
    }
}
