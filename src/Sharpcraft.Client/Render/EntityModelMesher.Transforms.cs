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
            if (model is null)
            {
                continue;
            }

            // Production geometry is compiled-tree only. A definition with no Compiled tree is invalid
            // here; fail loudly instead of silently falling back to a flat bake.
            var compiled = model.Compiled
                ?? throw new InvalidOperationException(
                    $"Entity model '{model.Name}' has no compiled tree; the flat Parts geometry path is removed.");
            for (var cubeIndex = 0; cubeIndex < compiled.Cubes.Count; cubeIndex++)
            {
                var cube = compiled.Cubes[cubeIndex];
                cubeCount += 1 + (cube.Overlays?.Count ?? 0);
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
            var thrownTrident = entity.TypeId == Sharpcraft.Client.Render.ThrownTridentModelDefinition.EntityTypeId;
            var rawPoseConjugation = model.RawSourceFrame && entityTransformFactory is null && !thrownTrident;
            var feet = Vector3.Zero;
            var entityTransform = entityTransformFactory?.Invoke(entity)
                ?? (thrownTrident ? EntityModelTransform.ThrownTrident(entity) : EntityModelTransform.From(entity));
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
                    if (!thrownTrident)
                    {
                        entityTransform = entityTransform.ForRawSourceFrame(model.SourceFrameBaselinePx / 16f * modelScale);
                    }
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

            // Compiled-tree only: a definition without a Compiled tree is invalid on this path. The
            // former flat Parts mesh loop was removed with Issue #229; fail loudly rather than fall back.
            var compiledModel = model.Compiled
                ?? throw new InvalidOperationException(
                    $"Entity model '{model.Name}' has no compiled tree; the flat Parts geometry path is removed.");
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

        if (entity.IsSmall && entity.TypeId == ArmorStandModelDefinition.ArmorStandEntityTypeId)
        {
            return EntityModelResolver.TryResolveVariant(ArmorStandModelDefinition.ArmorStandEntityTypeId, "small")
                ?? EntityModelResolver.Resolve(entity.TypeId, entity.IsBaby);
        }

        if (entity.IsBaby && entity.TypeId == EntityPlaceholderMesher.PlayerEntityTypeId)
        {
            // The player model has no separate baby mesh; the sample's baby humanoid uses the same source parts.
            return EntityModelResolver.Resolve(EntityPlaceholderMesher.PlayerEntityTypeId);
        }

        return variants is null
            ? EntityModelResolver.Resolve(entity.TypeId, entity.IsBaby)
            : variants.Resolve(entity.TypeId, entity.VariantId, entity.IsBaby);
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
        // The held-item anchor was fitted against the 24 px legacy bake (#120/#134), so this
        // compatibility frame keeps that constant even though the living raw baseline is now the
        // source 24.016 px flip (B-1 / #228). Following the living baseline would shift every held
        // item by 0.001 block away from the fitted anchor.
        const float LegacyHandBaselinePx = 24f;
        var legacyPivot = new Vector3(
            -sourcePivot.X,
            LegacyHandBaselinePx - sourcePivot.Y,
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
