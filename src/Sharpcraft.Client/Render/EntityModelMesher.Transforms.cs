// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using System.Runtime.InteropServices;
using Sharpcraft.Client.Assets;
using Sharpcraft.Client.World;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Render;

public sealed partial class EntityModelMesher
{
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

    // Internal so tests can sample a specific model definition through the exact living-entity
    // geometry path (modelOverride + null transform factory) for source-equivalence verification.
    internal DynamicGeometry BuildInto(DynamicGeometry target, EntityGeometryLayerKind? kindFilter, ReadOnlySpan<EntityRenderState> entities, SpriteRect skin, int skinWidth, int skinHeight, IReadOnlyDictionary<string, FaceUvTransform>? transforms = null, string? onlyPart = null, string? excludedPart = null, ISpriteAtlas? skinAtlas = null, EntityVariantTable? variants = null, bool emissivePass = false, EntityPose? pose = null, bool usePreparedPoses = false, EntityModelDefinition? modelOverride = null, Func<EntityRenderState, string?>? layerTextureSelector = null, Func<EntityRenderState, bool>? entityFilter = null, IReadOnlyList<EntityPose?>? poses = null, float modelScale = EntityPlaceholderMesher.ModelScale, Func<EntityRenderState, EntityModelTransform>? entityTransformFactory = null, int? textureWidthOverride = null, int? textureHeightOverride = null)
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
                for (var cubeIndex = 0; cubeIndex < compiled.Cubes.Count; cubeIndex++)
                {
                    var cube = compiled.Cubes[cubeIndex];
                    cubeCount += 1 + (cube.Overlays?.Count ?? 0);
                }

                continue;
            }

            for (var partIndex = 0; partIndex < model.Parts.Count; partIndex++)
            {
                var part = model.Parts[partIndex];
                cubeCount += 1 + (part.OverlayLayers?.Count ?? 0);
            }
        }

        target.Reserve((cubeCount + entities.Length * 2) * 24 * ChunkMesher.FloatsPerVertex, (cubeCount + entities.Length * 2) * 36);
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
            // Item entities are drawn by ItemEntityMesher and have no entity model in the reference, so they must not
            // fall back to the placeholder box: that box samples the entity skin at 64x64 UVs and showed up as a mosaic
            // cube wrapped around every dropped item (T-763).
            if (modelOverride is null && entity.TypeId == Sharpcraft.Client.World.EntityTracker.ItemEntityTypeId)
            {
                continue;
            }

            var model = modelOverride ?? ResolveEntityModel(entity, variants);
            if (model is null)
            {
                continue;
            }


            if (emissivePass && !HasEmissive(model))
            {
                continue;
            }

            var px = (1f / 16f) * modelScale * model.Scale;
            // Only the living-entity raw path (no block-entity transform factory) receives the source
            // frame pose conjugation; block entities keep their authored source pose literals.
            var rawPoseConjugation = model.RawSourceFrame && entityTransformFactory is null;
            var feet = Vector3.Zero;
            var entityTransform = entityTransformFactory?.Invoke(entity) ?? EntityModelTransform.From(entity);
            // Raw source-frame models are stored in the reference's y-down pixel frame. The legacy bake
            // folded the model-height reflection into geometry; for raw models the entity transform owns
            // that reflection (reference LivingEntityRenderer: scale(-1,-1,1) then translate(0,-1.501,0)).
            // The baseline is expressed in px already scaled by modelScale, matching the legacy world coords.
            // Block-entity renderers (a non-null factory) supply their own source transform and must not
            // receive the living-entity flip; only the LivingEntityRenderer path owns it.
            if (model.RawSourceFrame)
            {
                // Source layer scaling (MeshTransformer.scaling, ref://.../builders/MeshTransformer.java:7-9)
                // applies root yOffset = 24.016*(1-scale) in the y-down model frame. BuildModel has no
                // entity flip, so its samples add the offset directly to the world translation. The living
                // path flips y before the entity transform: add the model-space offset there (and subtract
                // it from Translation to keep the flip), and keep the 24 px baseline outside Scale.
                const float layerScalePivotPx = VanillaMeshBake.ReferencePivotHeight;
                if (entityTransformFactory is not null)
                {
                    if (model.Scale != 1f)
                    {
                        var offset = layerScalePivotPx * (1f - model.Scale) / 16f * modelScale;
                        entityTransform = entityTransform with
                        {
                            Translation = entityTransform.Translation + new Vector3(0f, offset, 0f)
                        };
                    }
                }
                else
                {
                    var modelSpaceOffset = layerScalePivotPx * (1f - model.Scale) / 16f * modelScale;
                    // The source root translation sits before the entity's own scale; our Translation is
                    // applied after EntityModelTransform scales the model, so pre-scale the offset here.
                    var entityScaleY = entityTransform.Scale * entityTransform.ScaleY;
                    entityTransform = entityTransform with
                    {
                        Translation = entityTransform.Translation - new Vector3(0f, modelSpaceOffset * entityScaleY, 0f)
                    };
                    entityTransform = entityTransform.ForRawSourceFrame(model.SourceFrameBaselinePx / 16f * modelScale);
                }
            }
            var sprite = skin;
            // UVs are authored against the model's declared texture size, not the image resolution (e.g. ghast declares
            // 64x32 while its PNG is 128x64). Normalising by the image size would sample the wrong texels.
            var skinWidthForModel = textureWidthOverride ?? model.TextureWidth;
            var skinHeightForModel = textureHeightOverride ?? model.TextureHeight;
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
                    model.LocalTransformSelector?.Invoke(entity) ?? model.LocalTransform,
                    model.RawSourceFrame,
                    rawPoseConjugation);
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

                var partTextureWidth = part.TextureWidth > 0 ? part.TextureWidth : skinWidthForModel;
                var partTextureHeight = part.TextureHeight > 0 ? part.TextureHeight : skinHeightForModel;

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

                AddPart(part, target, kindFilter, emissivePass, min, max, partSprite, partSkinWidth, partSkinHeight, feet, entityTransform, PartMatrix(part, model.Parts, px, entityPose, model.RawSourceFrame, rawPoseConjugation), transforms, model.FaceTransforms, entity, skinAtlas, px, ref vertexFloats, ref indexCount, model.RawSourceFrame);
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
        float px,
        bool rawSourceFrame = false,
        bool rawPoseConjugation = false)
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
                node.ScaleX * (1f + poseOffset.Scale.X),
                node.ScaleY * (1f + poseOffset.Scale.Y),
                node.ScaleZ * (1f + poseOffset.Scale.Z)))
            * (rawSourceFrame
                ? RotationZyx(
                    rotation.X + (rawPoseConjugation ? -poseOffset.Rotation.X : poseOffset.Rotation.X),
                    rotation.Y + poseOffset.Rotation.Y,
                    rotation.Z + (rawPoseConjugation ? -poseOffset.Rotation.Z : poseOffset.Rotation.Z))
                : RotationZyx(
                    rotation.X - poseOffset.Rotation.X,
                    rotation.Y - poseOffset.Rotation.Y,
                    rotation.Z + poseOffset.Rotation.Z))
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
        bool applyRootPose = true,
        bool rawSourceFrame = false)
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
            * (rawSourceFrame
                ? RotationZyx(-rootPose.Rotation.X, rootPose.Rotation.Y, -rootPose.Rotation.Z)
                : RotationZyx(-rootPose.Rotation.X, -rootPose.Rotation.Y, rootPose.Rotation.Z))
            * Matrix4x4.CreateTranslation(rootPose.Position * px);

        Matrix4x4? found = null;
        void Walk(EntityModelPartNode node, Matrix4x4 parentFrame)
        {
            if (found is not null)
            {
                return;
            }

            var frame = NodeFrame(node, parentFrame, entity, pose, px, rawSourceFrame, rawPoseConjugation: rawSourceFrame);
            if (string.Equals(node.Name, partName, StringComparison.Ordinal))
            {
                // Return the full frame. The geometry path subtracts a root part's pivot because its cube vertices are
                // authored relative to that pivot; a caller attaching *other* geometry (the held-item pass) needs the
                // pivot in world space, and subtracting it here silently collapsed the hand frame to the model origin.
                found = frame;
                return;
            }

            foreach (var child in node.ChildArray)
            {
                Walk(child, frame);
            }
        }

        foreach (var child in compiled.Root.ChildArray)
        {
            Walk(child, rootFrame);
        }

        return found ?? Matrix4x4.Identity;
    }
    /// <summary>
    /// Held-item-only legacy hand frame for a raw source-frame model. The reference's item-in-hand anchor was
    /// fitted against the legacy baked hand frame; raw part pivots are source-frame values, so replaying the
    /// legacy root-pivot conjugation here keeps that anchor valid without changing raw geometry or the generic
    /// <see cref="PartWorldMatrix"/> semantics.
    /// </summary>
    /// <remarks>
    /// For a root child the legacy bake maps a source pivot <c>(x, y, z)</c> to
    /// <c>(-x, modelHeight - y, z)</c>; child pivots already use the parent-local frame and fall back to the
    /// existing <see cref="PartWorldMatrix"/> path (e.g. CopperGolem's arm is a child of body).
    /// </remarks>
    public static Matrix4x4 LegacyHandWorldMatrix(
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

        var target = FindNode(compiled.Root, partName);
        if (target is null)
        {
            return Matrix4x4.Identity;
        }

        if (target.Parent is not null)
        {
            return PartWorldMatrix(definition, entity, partName, pose, applyRootPose, rawSourceFrame: false);
        }

        var px = (1f / 16f) * EntityPlaceholderMesher.ModelScale * definition.Scale;
        var rootPose = applyRootPose ? pose?.Get(compiled.Root.Name) ?? PoseOffset.Zero : PoseOffset.Zero;
        var rootFrame = Matrix4x4.CreateScale(new Vector3(
                1f + rootPose.Scale.X,
                1f + rootPose.Scale.Y,
                1f + rootPose.Scale.Z))
            * RotationZyx(-rootPose.Rotation.X, -rootPose.Rotation.Y, rootPose.Rotation.Z)
            * Matrix4x4.CreateTranslation(rootPose.Position * px);

        var poseName = target.PoseSource ?? target.Name;
        var poseOffset = pose?.Get(poseName) ?? PoseOffset.Zero;
        var rotation = new Vector3(target.RotationX, target.RotationY, target.RotationZ);
        if (target.RotationSelector is { } rotationSelector)
        {
            rotation = rotationSelector(entity);
        }

        var yOffset = target.YOffsetSelector?.Invoke(entity) ?? 0f;
        var sourcePivot = new Vector3(
            target.PivotX + poseOffset.Position.X,
            target.PivotY + poseOffset.Position.Y + yOffset,
            target.PivotZ + poseOffset.Position.Z);
        var legacyPivot = new Vector3(
            -sourcePivot.X,
            definition.SourceFrameBaselinePx - sourcePivot.Y,
            sourcePivot.Z);
        var local = Matrix4x4.CreateScale(new Vector3(
                1f + poseOffset.Scale.X,
                1f + poseOffset.Scale.Y,
                1f + poseOffset.Scale.Z))
            * RotationZyx(
                rotation.X - poseOffset.Rotation.X,
                rotation.Y - poseOffset.Rotation.Y,
                rotation.Z + poseOffset.Rotation.Z)
            * Matrix4x4.CreateTranslation(legacyPivot * px);
        return local * rootFrame;
    }

    private static EntityModelPartNode? FindNode(EntityModelPartNode node, string name)
    {
        if (string.Equals(node.Name, name, StringComparison.Ordinal))
        {
            return node;
        }

        foreach (var child in node.ChildArray)
        {
            if (FindNode(child, name) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    private static Matrix4x4 RotationZyx(float rx, float ry, float rz) =>
        Matrix4x4.CreateRotationX(rx) * Matrix4x4.CreateRotationY(ry) * Matrix4x4.CreateRotationZ(rz);

    /// <summary>
    /// Matrix that maps a point expressed relative to <paramref name="part"/>'s origin into entity space. This is the
    /// frame other parts' pivots are expressed in, not the frame a part's own cube is stored in.
    /// </summary>
    private static Matrix4x4 PartFrame(EntityModelPart part, IReadOnlyList<EntityModelPart> parts, float px, EntityPose? pose = null, bool rawSourceFrame = false, bool rawPoseConjugation = false)
    {
        var poseOffset = pose?.Get(PosePartName(part)) ?? PoseOffset.Zero;
        var pivot = new Vector3((part.PivotX + poseOffset.Position.X) * px, (part.PivotY + poseOffset.Position.Y) * px, (part.PivotZ + poseOffset.Position.Z) * px);
        // Row-vector convention: "rotate, then translate" is Rotation * Translation; animation scale is local.
        // Legacy vertices are already in the flipped space, so X/Y animation angles are conjugated; raw
        // source-frame vertices take the reference rotation literals directly.
        var local = Matrix4x4.CreateScale(new Vector3(1f + poseOffset.Scale.X, 1f + poseOffset.Scale.Y, 1f + poseOffset.Scale.Z))
            * (rawSourceFrame
                ? RotationZyx(
                    part.RotationX + (rawPoseConjugation ? -poseOffset.Rotation.X : poseOffset.Rotation.X),
                    part.RotationY + poseOffset.Rotation.Y,
                    part.RotationZ + (rawPoseConjugation ? -poseOffset.Rotation.Z : poseOffset.Rotation.Z))
                : RotationZyx(part.RotationX - poseOffset.Rotation.X, part.RotationY - poseOffset.Rotation.Y, part.RotationZ + poseOffset.Rotation.Z))
            * Matrix4x4.CreateTranslation(pivot);

        if (part.Parent is not { Length: > 0 } parentName)
        {
            return local;
        }

        foreach (var candidate in parts)
        {
            if (string.Equals(candidate.Name, parentName, StringComparison.Ordinal))
            {
                return local * PartFrame(candidate, parts, px, pose, rawSourceFrame, rawPoseConjugation);
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
    private static Matrix4x4 PartMatrix(EntityModelPart part, IReadOnlyList<EntityModelPart> parts, float px, EntityPose? pose = null, bool rawSourceFrame = false, bool rawPoseConjugation = false)
    {
        var frame = PartFrame(part, parts, px, pose, rawSourceFrame, rawPoseConjugation);
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
        DynamicGeometry target,
        bool rawUv = false)
    {
        var uMin = MathF.Min(MathF.Min(au, bu), MathF.Min(cu, du));
        var uMax = MathF.Max(MathF.Max(au, bu), MathF.Max(cu, du));
        var vMin = MathF.Min(MathF.Min(av, bv), MathF.Min(cv, dv));
        var vMax = MathF.Max(MathF.Max(av, bv), MathF.Max(cv, dv));
        if (!rawUv)
        {
            (au, av) = TransformUv(au, av, uMin, uMax, vMin, vMax, transform);
            (bu, bv) = TransformUv(bu, bv, uMin, uMax, vMin, vMax, transform);
            (cu, cv) = TransformUv(cu, cv, uMin, uMax, vMin, vMax, transform);
            (du, dv) = TransformUv(du, dv, uMin, uMax, vMin, vMax, transform);
        }
        var first = (uint)(vertexFloats / ChunkMesher.FloatsPerVertex);
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

    private static float ClampUv(float value, bool clamp) => clamp ? Math.Clamp(value, 0f, 1f) : value;
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
        TerrainVertexWriter.Write(
            destination,
            ref offset,
            world.X,
            world.Y,
            world.Z,
            tint.X,
            tint.Y,
            tint.Z,
            u,
            v,
            skin,
            sky: 1f,
            block: 0f);
    }

    /// <summary>
}
