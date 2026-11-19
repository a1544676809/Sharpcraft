// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Assets;
using Sharpcraft.Client.World;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Draws held items with the same geometry the rest of the item path uses: the model resolved by
/// <see cref="ItemModelResolver"/>, emitted through <see cref="ItemEntityMesher.EmitQuads"/>, and placed by the posed
/// hand frame times the item's own display transform. It replaces the previous fixed 8x8 quad, which drew every item as
/// the same box wearing a different sprite.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.renderer.entity.layers.ItemInHandLayer", "ref://mc-26.3-analysis/src/net/minecraft/client/renderer/entity/layers/ItemInHandLayer.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Adapter, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "Item geometry reuses the item-model path; special renderers keep their own hand layers")]
[Sharpcraft.Common.Diagnostics.Simplification("SIM-ITEM-HELD-GEOMETRY", "T-765", Note = "Held items use the shared geometry and display transform with the STAB offset and the spear use-item animation applied; kinetic weapon values come from the reference spear table, and the hit-feedback term is always zero because the client does not track ticksSinceKineticHitFeedback")]
public sealed class ItemInHandMesher
{
    /// <summary>Layer name reported back to the renderer.</summary>
    public const string LayerName = "item_in_hand";

    private readonly DynamicGeometry _cutoutGeometry = new();
    private readonly DynamicGeometry _translucentGeometry = new();
    private readonly DynamicGeometry _glintGeometry = new();
    private readonly DynamicGeometry _specialGlintGeometry = new();
    private readonly List<ItemModelQuad> _quads = [];
    // Held special (trident/shield) raw hand-chain output: [trident/right, trident/left, shield/right, shield/left].
    private readonly DynamicGeometry[] _heldSpecialCutout = [new(), new(), new(), new()];
    private readonly DynamicGeometry[] _heldSpecialGlint = [new(), new(), new(), new()];
    private readonly int[] _heldSpecialCutoutFloats = new int[4];
    private readonly int[] _heldSpecialCutoutIndices = new int[4];
    private readonly int[] _heldSpecialGlintFloats = new int[4];
    private readonly int[] _heldSpecialGlintIndices = new int[4];
    private readonly EntityModelMesher _heldSpecialMesher = new();
    private readonly Dictionary<EntityModelDefinition, HeldSpecialGeometry> _heldSpecialGeometryCache = [];

    /// <summary>Item glint sampler texture (ItemFeatureRenderer.ENCHANTED_GLINT_ITEM).</summary>
    public const string GlintTexture = "misc/enchanted_glint_item";

    /// <summary>
    /// Builds one item-atlas layer with every held item in <paramref name="entities"/>, or null when no hand carries a
    /// drawable item. <paramref name="poses"/> must be the poses the entity layers were built with, indexed like
    /// <paramref name="entities"/>.
    /// </summary>
    [Sharpcraft.Common.Diagnostics.Simplification("SIM-ITEM-SPECIAL-GLINT-RENDERTYPE", "T-631e", Note = "source item_cutout_glint_special/item_translucent_glint_special compose albedo+glint in one pass; Sharpcraft keeps its base item pass plus an additive overlay, now with its own SpecialGlint kind and pipeline")]
    public IReadOnlyList<EntityGeometryLayer> Build(
        IReadOnlyList<EntityRenderState> entities,
        IReadOnlyList<EntityPose> poses,
        EntityVariantTable? variants,
        ISpriteAtlas itemAtlas)
    {
        ArgumentNullException.ThrowIfNull(entities);
        ArgumentNullException.ThrowIfNull(poses);
        ArgumentNullException.ThrowIfNull(itemAtlas);
        if (EntityModelResolver.ItemModels is not { } models)
        {
            return [];
        }

        _cutoutGeometry.Clear();
        _translucentGeometry.Clear();
        _glintGeometry.Clear();
        _specialGlintGeometry.Clear();
        for (var special = 0; special < 4; special++)
        {
            _heldSpecialCutout[special].Clear();
            _heldSpecialGlint[special].Clear();
            _heldSpecialCutoutFloats[special] = 0;
            _heldSpecialCutoutIndices[special] = 0;
            _heldSpecialGlintFloats[special] = 0;
            _heldSpecialGlintIndices[special] = 0;
        }

        var cutoutFloats = 0;
        var cutoutIndices = 0;
        var translucentFloats = 0;
        var translucentIndices = 0;
        var glintFloats = 0;
        var glintIndices = 0;
        var specialGlintFloats = 0;
        var specialGlintIndices = 0;
        var grass = models.GrassColormap;

        for (var index = 0; index < entities.Count; index++)
        {
            var entity = entities[index];
            var pose = index < poses.Count ? poses[index] : null;
            var model = EntityModelMesher.ResolveEntityModel(entity, variants);
            var entityMatrix = EntityModelTransform.From(entity).ToMatrix();

            for (var hand = 0; hand < 2; hand++)
            {
                var offhand = hand == 1;
                var armName = offhand ? "left_arm" : "right_arm";
                var placements = EntityModelResolver.ResolveHeldItemPlacements(entity, offhand);
                if (placements.Count == 0)
                {
                    // Special-renderer items have no item quads; the trident/shield models are emitted through the same
                    // raw hand chain in their own geometry buffer (see EmitHeldSpecial).
                    EmitHeldSpecial(entity, pose, model, offhand, armName, entityMatrix, itemAtlas);
                    continue;
                }
                var foil = offhand ? entity.OffHandFoil : entity.MainHandFoil;
                var displayContext = EntityModelResolver.DisplayContextFor(offhand);
                var humanoidSource = HumanoidHeldHandFrame.IsHumanoid(entity.TypeId);
                var handFrame = humanoidSource
                    ? HumanoidHeldHandFrame.SourceWorld(model, armName, pose, offhand, entity.IsBaby)
                    : model.RawSourceFrame
                        ? EntityModelMesher.LegacyHandWorldMatrix(model, entity, armName, pose)
                        : EntityModelMesher.PartWorldMatrix(model, entity, armName, pose);
                var stab = IsStabSwingOnHand(entity, offhand)
                    ? SpearAnimations.ThirdPersonAttackItemMatrix(entity.SwingAnimation, jetForward: 0f)
                    : System.Numerics.Matrix4x4.Identity;
                var useItem = SpearUseItemFor(entity, offhand);
                var anchor = humanoidSource
                    ? System.Numerics.Matrix4x4.Identity
                    : EntityModelResolver.AnchorCoreFor(offhand, entity.IsBaby);

                // A composite item submits every member, each under its own item-model transformation. Reference order
                // with row vectors: the member transform applies first, then the item display transform, the use-item
                // animation, the STAB swing offset, the hand anchor, then the posed hand and the entity transform.
                foreach (var placement in placements)
                {
                    _quads.Clear();
                    ItemEntityMesher.CollectQuads(_quads, placement.Model, itemAtlas as ItemAtlas);
                    if (_quads.Count == 0)
                    {
                        continue;
                    }

                    var display = EntityModelResolver.DisplayFor(placement.Model, offhand);
                    var world = placement.Transform * display * useItem * stab * anchor * handFrame * entityMatrix;
                    var translucent = ItemEntityMesher.IsTranslucent(placement.Model, itemAtlas as ItemAtlas);
                    if (translucent)
                    {
                        ItemEntityMesher.EmitQuads(
                            _translucentGeometry,
                            _quads,
                            world,
                            itemAtlas,
                            placement.Model,
                            grass,
                            ref translucentFloats,
                            ref translucentIndices);
                    }
                    else
                    {
                        ItemEntityMesher.EmitQuads(
                            _cutoutGeometry,
                            _quads,
                            world,
                            itemAtlas,
                            placement.Model,
                            grass,
                            ref cutoutFloats,
                            ref cutoutIndices);
                    }

                    if (foil == ItemFoilType.Special)
                    {
                        // SPECIAL foil (compass/clock): the glint samples the reference decal coordinate (UV3), so the
                        // same quads go through the emitter with the model-space projection instead of the sprite UV.
                        ItemEntityMesher.EmitQuads(
                            _specialGlintGeometry,
                            _quads,
                            world,
                            itemAtlas,
                            placement.Model,
                            grass,
                            ref specialGlintFloats,
                            ref specialGlintIndices,
                            GlintTexture,
                            ItemFoilDecal.ScaleFor(displayContext));
                    }
                    else if (foil == ItemFoilType.Standard)
                    {
                        ItemEntityMesher.EmitQuads(
                            _glintGeometry,
                            _quads,
                            world,
                            itemAtlas,
                            placement.Model,
                            grass,
                            ref glintFloats,
                            ref glintIndices,
                            GlintTexture);
                    }
                }
            }
        }

        var layers = new List<EntityGeometryLayer>(2);
        if (cutoutIndices > 0)
        {
            _cutoutGeometry.VertexFloats = cutoutFloats;
            _cutoutGeometry.IndexCount = cutoutIndices;
            layers.Add(new EntityGeometryLayer(LayerName, EntityGeometryLayerKind.ItemCutout, 890, _cutoutGeometry, usesItemAtlas: true));
        }

        if (translucentIndices > 0)
        {
            _translucentGeometry.VertexFloats = translucentFloats;
            _translucentGeometry.IndexCount = translucentIndices;
            layers.Add(new EntityGeometryLayer($"{LayerName}_translucent", EntityGeometryLayerKind.ItemTranslucent, 895, _translucentGeometry, usesItemAtlas: true));
        }

        if (glintIndices > 0)
        {
            _glintGeometry.VertexFloats = glintFloats;
            _glintGeometry.IndexCount = glintIndices;
            GlintTextureTransform.ApplyToGeometry(_glintGeometry, GlintClock.Millis, GlintTextureTransform.ItemScale);
            layers.Add(new EntityGeometryLayer($"{LayerName}_glint", EntityGeometryLayerKind.Glint, 892, _glintGeometry, usesItemAtlas: true));
        }

        if (specialGlintIndices > 0)
        {
            _specialGlintGeometry.VertexFloats = specialGlintFloats;
            _specialGlintGeometry.IndexCount = specialGlintIndices;
            GlintTextureTransform.ApplyDecalToGeometry(_specialGlintGeometry, GlintClock.Millis, GlintTextureTransform.ItemScale);
            layers.Add(new EntityGeometryLayer($"{LayerName}_glint_special", EntityGeometryLayerKind.SpecialGlint, 893, _specialGlintGeometry, usesItemAtlas: true));
        }

        for (var heldSpecial = 0; heldSpecial < 4; heldSpecial++)
        {
            var specialName = heldSpecial < 2 ? "trident" : "shield";
            var specialHand = heldSpecial % 2 == 1 ? "left" : "right";
            var cutout = _heldSpecialCutout[heldSpecial];
            if (_heldSpecialCutoutIndices[heldSpecial] > 0)
            {
                cutout.VertexFloats = _heldSpecialCutoutFloats[heldSpecial];
                cutout.IndexCount = _heldSpecialCutoutIndices[heldSpecial];
                layers.Add(new EntityGeometryLayer($"held_special_{specialName}_{specialHand}", EntityGeometryLayerKind.Cutout,
                    896 + (heldSpecial * 2), cutout, usesItemAtlas: true));
            }

            var glint = _heldSpecialGlint[heldSpecial];
            if (_heldSpecialGlintIndices[heldSpecial] > 0)
            {
                glint.VertexFloats = _heldSpecialGlintFloats[heldSpecial];
                glint.IndexCount = _heldSpecialGlintIndices[heldSpecial];
                GlintTextureTransform.ApplyToGeometry(glint, GlintClock.Millis, GlintTextureTransform.EntityScale);
                layers.Add(new EntityGeometryLayer($"held_special_{specialName}_{specialHand}_glint",
                    EntityGeometryLayerKind.Glint, 897 + (heldSpecial * 2), glint, usesItemAtlas: true));
            }
        }

        return layers;
    }

    /// <summary>
    /// Texture paths a held item needs, for both hands. The atlas pre-pass has to prime these exactly like the drop
    /// path, otherwise a held model whose sprites were never added renders nothing.
    /// </summary>
    public static IEnumerable<string> ResolveTextures(EntityRenderState entity)
    {
        if (entity.MainHandFoil != ItemFoilType.None || entity.OffHandFoil != ItemFoilType.None)
        {
            yield return GlintTexture;
        }

        foreach (var offhand in new[] { false, true })
        {
            foreach (var placement in EntityModelResolver.ResolveHeldItemPlacements(entity, offhand))
            {
                foreach (var texture in ItemEntityMesher.ResolutionTextures(placement.Model))
                {
                    yield return texture;
                }
            }
        }
    }
    /// <summary>
    /// Reference <c>ArmPose.animateUseItem</c> for the spear: only the SPEAR pose overrides it, so this contributes
    /// nothing unless the holder is mid-use with a kinetic weapon in the using hand.
    /// </summary>
    private static System.Numerics.Matrix4x4 SpearUseItemFor(in EntityRenderState entity, bool offhand)
    {
        if (entity.TicksUsingItem == 0 || (entity.UseItemHand == 1) != offhand)
        {
            return System.Numerics.Matrix4x4.Identity;
        }

        var itemId = offhand ? entity.OffHandItemId : entity.MainHandItemId;
        if (itemId <= 0 || EntityModelResolver.Items?.ById(itemId) is not { } item)
        {
            return System.Numerics.Matrix4x4.Identity;
        }

        if (KineticWeapons.ForItem(item.Id.Path) is not { } weapon)
        {
            return System.Numerics.Matrix4x4.Identity;
        }

        var parameters = SpearAnimations.UseParams.FromKineticWeapon(weapon, entity.TicksUsingItem);
        return SpearAnimations.ThirdPersonUseItemMatrix(parameters, weapon, entity.SwingAnimation, invert: offhand ? -1 : 1);
    }
    /// <summary>
    /// Reference condition for the STAB offset: the current swing's animation must be STAB and its hand must be the arm
    /// holding this item.
    /// </summary>
    private static bool IsStabSwingOnHand(in EntityRenderState entity, bool offhand)
    {
        if (entity.SwingType != SpearAnimations.StabSwingType)
        {
            return false;
        }

        // The swing hand is MAIN_HAND (0) or OFF_HAND (1); it maps onto an arm through the entity's main arm, which this
        // baseline treats as the right arm.
        return (entity.SwingHand == 1) == offhand;
    }

    /// <summary>
    /// Emits the trident/shield special model through the same raw hand chain as regular held items:
    /// world = specialLocal · display · stab · anchor · handFrame · entityMatrix (row vectors).
    /// The special geometry cannot share the item-quad buffer (cube model vs. quad model), so it keeps its own
    /// geometry buffer while using the identical frame.
    /// </summary>
    private void EmitHeldSpecial(
        EntityRenderState entity,
        EntityPose? pose,
        EntityModelDefinition hostModel,
        bool offhand,
        string armName,
        Matrix4x4 entityMatrix,
        ISpriteAtlas itemAtlas)
    {
        if (EntityModelResolver.ResolveHeldItemSpecial(entity, offhand) is not { } specialId)
        {
            return;
        }

        var specialType = specialId.StartsWith("minecraft:", StringComparison.Ordinal) ? specialId["minecraft:".Length..] : specialId;
        if (specialType is not ("trident" or "shield"))
        {
            return;
        }

        var itemId = offhand ? entity.OffHandItemId : entity.MainHandItemId;
        if (EntityModelResolver.ItemModels is not { } models || EntityModelResolver.Items?.ById(itemId) is not { } item)
        {
            return;
        }

        var placements = models.ResolveAll(item.Id.ToString(), entity.IsUsingItem);
        if (placements.Count == 0)
        {
            return;
        }

        var humanoidSource = HumanoidHeldHandFrame.IsHumanoid(entity.TypeId);
        var handFrame = humanoidSource
            ? HumanoidHeldHandFrame.SourceWorld(hostModel, armName, pose, offhand, entity.IsBaby)
            : hostModel.RawSourceFrame
                ? EntityModelMesher.LegacyHandWorldMatrix(hostModel, entity, armName, pose)
                : EntityModelMesher.PartWorldMatrix(hostModel, entity, armName, pose);
        var stab = IsStabSwingOnHand(entity, offhand)
            ? SpearAnimations.ThirdPersonAttackItemMatrix(entity.SwingAnimation, jetForward: 0f)
            : Matrix4x4.Identity;
        var anchor = humanoidSource ? Matrix4x4.Identity : EntityModelResolver.AnchorCoreFor(offhand, entity.IsBaby);
        var display = EntityModelResolver.DisplayFor(placements[0].Model, offhand);
        // items/{trident,shield}.json `transformation.scale = (1, -1, -1)`; rotations/translation are identity.
        var specialLocal = Matrix4x4.CreateScale(1f, -1f, -1f);
        var world = specialLocal * display * stab * anchor * handFrame * entityMatrix;

        var specialModel = specialType == "trident" ? SpecialItemModelDefinition.Trident : SpecialItemModelDefinition.Shield;
        var texture = specialType == "trident" ? SpecialItemModelDefinition.TridentTexture : SpecialItemModelDefinition.ShieldTexture;
        if (itemAtlas.Get(texture) is not { } sprite)
        {
            return;
        }

        var slot = (specialType == "shield" ? 2 : 0) + (offhand ? 1 : 0);
        var geometry = HeldSpecialGeometryFor(specialModel);
        var target = _heldSpecialCutout[slot];
        var vertices = geometry.Positions.Length / 3;
        target.Reserve(_heldSpecialCutoutFloats[slot] + (vertices * ChunkMesher.FloatsPerVertex),
            _heldSpecialCutoutIndices[slot] + ((vertices / 4) * 6));
        for (var vertex = 0; vertex < vertices; vertex++)
        {
            var point = new Vector3(geometry.Positions[vertex * 3], geometry.Positions[(vertex * 3) + 1],
                geometry.Positions[(vertex * 3) + 2]);
            point = Vector3.Transform(point, world);
            TerrainVertexWriter.Write(target.Vertices, ref _heldSpecialCutoutFloats[slot],
                point.X, point.Y, point.Z, 1f, 1f, 1f,
                geometry.Uvs[vertex * 2], geometry.Uvs[(vertex * 2) + 1], sprite, sky: 1f, block: 0f);
        }

        WriteHeldSpecialIndices(target, ref _heldSpecialCutoutIndices[slot], _heldSpecialCutoutFloats[slot], vertices);

        var foil = offhand ? entity.OffHandFoil : entity.MainHandFoil;
        if (foil == ItemFoilType.None)
        {
            return;
        }

        var glintTarget = _heldSpecialGlint[slot];
        glintTarget.Reserve(_heldSpecialGlintFloats[slot] + (vertices * ChunkMesher.FloatsPerVertex),
            _heldSpecialGlintIndices[slot] + ((vertices / 4) * 6));
        for (var vertex = 0; vertex < vertices; vertex++)
        {
            var point = new Vector3(geometry.Positions[vertex * 3], geometry.Positions[(vertex * 3) + 1],
                geometry.Positions[(vertex * 3) + 2]);
            point = Vector3.Transform(point, world);
            TerrainVertexWriter.Write(glintTarget.Vertices, ref _heldSpecialGlintFloats[slot],
                point.X, point.Y, point.Z, 1f, 1f, 1f,
                geometry.Uvs[vertex * 2], geometry.Uvs[(vertex * 2) + 1], sprite, sky: 1f, block: 0f);
        }

        WriteHeldSpecialIndices(glintTarget, ref _heldSpecialGlintIndices[slot], _heldSpecialGlintFloats[slot], vertices);
    }

    private static void WriteHeldSpecialIndices(DynamicGeometry target, ref int indexCount, int vertexFloats, int vertices)
    {
        var first = (uint)((vertexFloats / ChunkMesher.FloatsPerVertex) - vertices);
        for (var quad = 0; quad < vertices / 4; quad++)
        {
            var start = first + (uint)(quad * 4);
            target.Indices[indexCount++] = start;
            target.Indices[indexCount++] = start + 1;
            target.Indices[indexCount++] = start + 2;
            target.Indices[indexCount++] = start;
            target.Indices[indexCount++] = start + 2;
            target.Indices[indexCount++] = start + 3;
        }
    }

    private HeldSpecialGeometry HeldSpecialGeometryFor(EntityModelDefinition model)
    {
        if (_heldSpecialGeometryCache.TryGetValue(model, out var cached))
        {
            return cached;
        }

        var state = new EntityRenderState(0, 0, 0f, 0f, 0f, 180f, 0f);
        var geometry = _heldSpecialMesher.BuildModel(model, state,
            new SpriteRect(0, 0, model.TextureWidth, model.TextureHeight, 1),
            model.TextureWidth, model.TextureHeight, modelScale: 1f);
        var count = geometry.VertexFloats / ChunkMesher.FloatsPerVertex;
        var positions = new float[count * 3];
        var uvs = new float[count * 2];
        for (var vertex = 0; vertex < count; vertex++)
        {
            var offset = vertex * ChunkMesher.FloatsPerVertex;
            positions[vertex * 3] = geometry.Vertices[offset];
            positions[(vertex * 3) + 1] = geometry.Vertices[offset + 1];
            positions[(vertex * 3) + 2] = geometry.Vertices[offset + 2];
            var packed = BitConverter.SingleToUInt32Bits(geometry.Vertices[offset + 4]);
            uvs[vertex * 2] = (float)BitConverter.UInt16BitsToHalf((ushort)(packed & 0xFFFF));
            uvs[(vertex * 2) + 1] = (float)BitConverter.UInt16BitsToHalf((ushort)(packed >> 16));
        }

        var result = new HeldSpecialGeometry(positions, uvs);
        _heldSpecialGeometryCache[model] = result;
        return result;
    }

    private readonly record struct HeldSpecialGeometry(float[] Positions, float[] Uvs);}
