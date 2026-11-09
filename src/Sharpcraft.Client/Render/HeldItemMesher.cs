// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.World;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Draws held items with the same geometry the rest of the item path uses: the model resolved by
/// <see cref="ItemModelResolver"/>, emitted through <see cref="DroppedItemMesher.EmitQuads"/>, and placed by the posed
/// hand frame times the item's own display transform. It replaces the previous fixed 8x8 quad, which drew every item as
/// the same box wearing a different sprite.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.renderer.entity.layers.ItemInHandLayer", "ref://mc-26.3-analysis/src/net/minecraft/client/renderer/entity/layers/ItemInHandLayer.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Adapter, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "Item geometry reuses the item-model path; special renderers keep their own hand layers")]
[Sharpcraft.Common.Diagnostics.Simplification("SIM-ITEM-HELD-GEOMETRY", "T-765", Note = "Held items now use the shared item geometry and display transform, but SpearAnimations' STAB offset and the use-item arm pose offsets are not applied")]
public sealed class HeldItemMesher
{
    /// <summary>Layer name reported back to the renderer.</summary>
    public const string LayerName = "held_item";

    private readonly DynamicGeometry _geometry = new();
    private readonly List<ItemModelQuad> _quads = [];

    /// <summary>
    /// Builds one item-atlas layer with every held item in <paramref name="entities"/>, or null when no hand carries a
    /// drawable item. <paramref name="poses"/> must be the poses the entity layers were built with, indexed like
    /// <paramref name="entities"/>.
    /// </summary>
    public EntityGeometryLayer? Build(
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
            return null;
        }

        _geometry.Clear();
        var vertexFloats = 0;
        var indexCount = 0;
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
                if (EntityModelResolver.ResolveHeldItemModel(entity, offhand) is not { } resolution)
                {
                    continue;
                }

                _quads.Clear();
                DroppedItemMesher.CollectQuads(_quads, resolution, itemAtlas as ItemAtlas);
                if (_quads.Count == 0)
                {
                    continue;
                }

                var armName = offhand ? "left_arm" : "right_arm";
                var handFrame = EntityModelMesher.PartWorldMatrix(model, entity, armName, pose);
                // Reference order with row vectors: the item's display transform applies first, then the STAB swing
                // offset, then the hand anchor (XP -90, YP 180, per-hand offset), then the posed hand and the entity
                // transform.
                var display = EntityModelResolver.DisplayFor(resolution, offhand);
                var stab = IsStabSwingOnHand(entity, offhand)
                    ? SpearAnimations.ThirdPersonAttackItemMatrix(entity.SwingAnimation, jetForward: 0f)
                    : System.Numerics.Matrix4x4.Identity;
                var anchor = EntityModelResolver.AnchorCoreFor(offhand, entity.IsBaby);
                var world = display * stab * anchor * handFrame * entityMatrix;
                DroppedItemMesher.EmitQuads(
                    _geometry,
                    _quads,
                    world,
                    itemAtlas,
                    resolution,
                    grass,
                    ref vertexFloats,
                    ref indexCount);
            }
        }

        if (indexCount == 0)
        {
            return null;
        }

        _geometry.VertexFloats = vertexFloats;
        _geometry.IndexCount = indexCount;
        return new EntityGeometryLayer(LayerName, EntityGeometryLayerKind.Cutout, 890, _geometry, usesItemAtlas: true);
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
}
