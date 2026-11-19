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
    public const string CrossLayerName = "held_witch_item";

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
        if (EntityModelResolver.ItemModels is not { } models)
        {
            return null;
        }

        _cutoutGeometry.Clear();
        var vertexFloats = 0;
        var indexCount = 0;
        var grass = models.GrassColormap;
        var emittedPotion = false;
        var emittedCross = false;

        for (var index = 0; index < entities.Count; index++)
        {
            var entity = entities[index];
            var itemId = entity.MainHandItemId;
            if (entity.TypeId != WitchModelDefinition.WitchEntityTypeId || itemId <= 0
                || EntityModelResolver.Items?.ById(itemId) is not { } item)
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
            var entityMatrix = EntityModelTransform.From(entity).ToMatrix();
            if (entity.IsHoldingPotion)
            {
                // Source WitchItemLayer potion branch: root/head/nose chain + POTION literal transforms.
                var potion = potionModel ?? models.Resolve("minecraft:potion");
                if (potion is null)
                {
                    continue;
                }

                var world = DisplayForGround(potion) * WitchHeldItemFrame.PotionSource(root) * entityMatrix;
                _quads.Clear();
                ItemEntityMesher.CollectQuads(_quads, potion, itemAtlas as ItemAtlas);
                foreach (var quad in _quads)
                {
                    if (itemAtlas.Get(quad.Texture) is not { } sprite)
                    {
                        continue;
                    }

                    var tint = TintFor(quad.TintIndex, potion, entity, grass);
                    ItemEntityMesher.EmitQuads(
                        _cutoutGeometry, [quad], world, itemAtlas, potion, grass,
                        ref vertexFloats, ref indexCount, tintOverride: tint);
                }

                emittedPotion = true;
                continue;
            }

            // Source CrossedArmsItemLayer branch: root/arms chain + crossed-arms transforms, then the stack's
            // ItemDisplayContext.GROUND display and its own model tint sources.
            foreach (var placement in models.ResolveAll(item.Id.ToString(), entity.IsUsingItem))
            {
                var world = DisplayForGround(placement.Model) * WitchHeldItemFrame.CrossSource(root) * entityMatrix;
                _quads.Clear();
                ItemEntityMesher.CollectQuads(_quads, placement.Model, itemAtlas as ItemAtlas);
                foreach (var quad in _quads)
                {
                    if (itemAtlas.Get(quad.Texture) is not { } sprite)
                    {
                        continue;
                    }

                    ItemEntityMesher.EmitQuads(
                        _cutoutGeometry, [quad], world, itemAtlas, placement.Model, grass,
                        ref vertexFloats, ref indexCount);
                }

                emittedCross = true;
            }
        }

        if (indexCount == 0)
        {
            return null;
        }

        _cutoutGeometry.VertexFloats = vertexFloats;
        _cutoutGeometry.IndexCount = indexCount;
        var layerName = emittedPotion || !emittedCross ? LayerName : CrossLayerName;
        return new EntityGeometryLayer(layerName, EntityGeometryLayerKind.ItemCutout, 895, _cutoutGeometry, usesItemAtlas: true);
    }
    /// <summary>ItemDisplayContext.GROUND transform from the model's base display (item/generated.json).</summary>
    private static Matrix4x4 DisplayForGround(ItemModelResolution model) =>
        model.Display.TryGetValue(ItemDisplayContext.Ground, out var transform)
            ? transform.ToMatrix(applyLeftHandFix: false)
            : Matrix4x4.Identity;

    /// <summary>
    /// Resolves the tint of a generated layer: the stack custom colour (PotionContents.custom_color) first, then the
    /// item model default. The effect-derived branch needs the potion registry id on the render state and is a
    /// registered blocker (R-046); the jar colour table is kept in the test assembly until it has a real consumer.
    /// Layer index maps to tint index.
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

        // PotionContents.getColorOr(default): custom colour -> effects/potion registry colour -> item tint default.
        int? argb = entity.HeldPotionColor;
        argb ??= PotionColorTable.Resolve(entity.HeldPotionId, entity.HeldPotionEffects);
        argb ??= source.Argb == 0 ? ItemTintSource.PotionDefaultColor : source.Argb;
        return new Vector3(
            ((argb.Value >> 16) & 0xFF) / 255f,
            ((argb.Value >> 8) & 0xFF) / 255f,
            (argb.Value & 0xFF) / 255f);
    }
}
