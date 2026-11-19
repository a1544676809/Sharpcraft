// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.World;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Renders a witch's held potion with the real item model and the reference WitchItemLayer pose, replacing the old
/// single flat quad. Single responsibility: witches holding a potion; every other held/special item is untouched.
///
/// Source chain (all ref://mc-26.3-analysis):
///   world = flipBaseline * witchPose * groundDisplay * specialLocal * quads
///   flipBaseline = S(-1,-1,1)*T(0,-1.501,0)                    (LivingEntityRenderer.java:85,87)
///   witchPose    = root.TR * head.TR * nose.TR * T(0.0625,0.25,0) * Z180 * X140 * Z10 * X180
///                                                              (WitchItemLayer.java:15-24), or the CROSS branch
///                                                              (CrossedArmsItemLayer.java:30-36) for a non-potion item
///   groundDisplay = ItemTransform.apply(ItemDisplayContext.GROUND) from item/generated.json
///   quads        = builtin/generated layer0 (potion_overlay) + layer1 (potion), tint index = layer index
///                                                              (ItemModelGenerator.java:25,80-88)
///
/// Verified against the jar golden vectors witch-potion-pose.reference.txt (pose) and
/// witch-potion-quad.reference.txt (world keyed vertices).
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.renderer.entity.layers.WitchItemLayer", "ref://mc-26.3-analysis/src/net/minecraft/client/renderer/entity/layers/WitchItemLayer.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Adapter, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "potion branch implemented; CROSS branch pose implemented but only potion items are rendered here")]
public sealed class WitchHeldPotionMesher
{
    public const string LayerName = "held_potion_item";

    private readonly DynamicGeometry _cutoutGeometry = new();
    private readonly List<ItemModelQuad> _quads = [];

    /// <summary>Builds the witch held-potion layer, or null when no witch in the batch holds a potion.</summary>
    public EntityGeometryLayer? Build(
        IReadOnlyList<EntityRenderState> entities,
        IReadOnlyList<EntityPose> poses,
        ISpriteAtlas itemAtlas,
        ItemModelResolution? potionModel)
    {
        ArgumentNullException.ThrowIfNull(entities);
        ArgumentNullException.ThrowIfNull(itemAtlas);
        if (potionModel is null || EntityModelResolver.ItemModels is not { } models)
        {
            return null;
        }

        _cutoutGeometry.Clear();
        var vertexFloats = 0;
        var indexCount = 0;
        var grass = models.GrassColormap;

        for (var index = 0; index < entities.Count; index++)
        {
            var entity = entities[index];
            if (!entity.IsHoldingPotion || entity.TypeId != WitchModelDefinition.WitchEntityTypeId)
            {
                continue;
            }

            var definition = EntityModelResolver.Resolve(WitchModelDefinition.WitchEntityTypeId, entity.IsBaby);
            var root = definition.Compiled?.Root;
            if (root is null)
            {
                continue;
            }

            var pose = index < poses.Count ? poses[index] : null;
            // Source pose (root/head/nose chain + POTION literal transforms) in y-down source render units.
            var witchPose = WitchHeldItemFrame.PotionSource(root);
            // ItemDisplayContext.GROUND transform from the potion model's base display (item/generated.json).
            var display = DisplayForGround(potionModel);
            // Living-entity flip/baseline (S(-1,-1,1)*T(0,-1.501,0)) plus per-entity yaw/position/scale.
            var entityMatrix = EntityModelTransform.From(entity).ToMatrix();
            var world = witchPose * display * entityMatrix;

            _quads.Clear();
            ItemEntityMesher.CollectQuads(_quads, potionModel, itemAtlas as ItemAtlas);
            foreach (var quad in _quads)
            {
                if (itemAtlas.Get(quad.Texture) is not { } sprite)
                {
                    continue;
                }

                var tint = TintFor(quad.TintIndex, potionModel, entity, grass);
                ItemEntityMesher.EmitQuads(
                    _cutoutGeometry, [quad], world, itemAtlas, potionModel, grass,
                    ref vertexFloats, ref indexCount, tintOverride: tint);
            }
        }

        if (indexCount == 0)
        {
            return null;
        }

        _cutoutGeometry.VertexFloats = vertexFloats;
        _cutoutGeometry.IndexCount = indexCount;
        return new EntityGeometryLayer(LayerName, EntityGeometryLayerKind.ItemCutout, 895, _cutoutGeometry, usesItemAtlas: true);
    }

    /// <summary>ItemDisplayContext.GROUND transform from the model's base display (item/generated.json).</summary>
    private static Matrix4x4 DisplayForGround(ItemModelResolution model) =>
        model.Display.TryGetValue(ItemDisplayContext.Ground, out var transform)
            ? transform.ToMatrix(applyLeftHandFix: false)
            : Matrix4x4.Identity;

    /// <summary>
    /// Resolves the tint of a generated layer: a stack custom colour (potion contents), then the potion id's
    /// effect-derived colour table, then the item model default. Layer index maps to tint index.
    /// </summary>
    private Vector3 TintFor(int tintIndex, ItemModelResolution model, EntityRenderState entity, GrassColorMap? grass)
    {
        if (tintIndex < 0 || tintIndex >= model.TintSources.Count)
        {
            return Vector3.One;
        }

        var source = model.TintSources[tintIndex];
        if (source.Kind != ItemTintKind.Potion)
        {
            return source.Evaluate(grass);
        }

        int? argb = entity.HeldPotionColor;
        // Effect-derived colour needs the potion registry id (DataComponents.POTION_CONTENTS holder) on the render
        // state; it is not threaded there yet (R-046 blocker). Until then only a stack custom colour or the source
        // default is available. PotionColorTable holds the jar vectors and is unit-tested, but has no consumer here.
        argb ??= source.Argb == 0 ? ItemTintSource.PotionDefaultColor : source.Argb;
        return new Vector3(
            ((argb.Value >> 16) & 0xFF) / 255f,
            ((argb.Value >> 8) & 0xFF) / 255f,
            (argb.Value & 0xFF) / 255f);
    }
}
