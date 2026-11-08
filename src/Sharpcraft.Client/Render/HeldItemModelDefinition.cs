// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// Minimal held-item layers for adult humanoids. Each variant mirrors the matching arm pose name so the item follows
/// the arm, while the quad samples the independent item atlas through the layer texture selector.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.renderer.entity.layers.ItemInHandLayer", "ref://mc-26.3-analysis/src/net/minecraft/client/renderer/entity/layers/ItemInHandLayer.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Adapter, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
[Sharpcraft.Common.Diagnostics.Simplification("SIM-ITEM-HELD-ANCHOR", "T-631e", Note = "Vanilla uses translateToHand + XP/-90 + YP/180 + (1,2,-10)/16 adult or (0,1,-4.5)/16 baby offsets; Sharpcraft uses a fixed per-arm part offset and no rotation")]
public static class HeldItemModelDefinition
{
    public static readonly EntityModelDefinition Definition = EntityModelDefinition.FromTree(
        "held_item",
        64,
        64,
        BuildMesh(),
        PlayerModelDefinition.DefaultFaceTransforms,
        texturePath: null,
        1f);

    /// <summary>Off-hand variant; the left arm pivot mirrors the humanoid right arm.</summary>
    public static readonly EntityModelDefinition LeftDefinition = EntityModelDefinition.FromTree(
        "held_item_left",
        64,
        64,
        BuildMesh(left: true),
        PlayerModelDefinition.DefaultFaceTransforms,
        texturePath: null,
        1f);

    /// <summary>Baby humanoid right-hand variant (arms are 3 px from the centre and 15.5 px high).</summary>
    public static readonly EntityModelDefinition BabyDefinition = EntityModelDefinition.FromTree(
        "held_item_baby_right",
        64,
        64,
        BuildMesh(left: false, baby: true),
        PlayerModelDefinition.DefaultFaceTransforms,
        texturePath: null,
        1f);

    /// <summary>Baby humanoid left-hand variant.</summary>
    public static readonly EntityModelDefinition BabyLeftDefinition = EntityModelDefinition.FromTree(
        "held_item_baby_left",
        64,
        64,
        BuildMesh(left: true, baby: true),
        PlayerModelDefinition.DefaultFaceTransforms,
        texturePath: null,
        1f);

    private static VanillaMeshDefinition BuildMesh(bool left = false, bool baby = false)
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        var arm = root.AddOrReplaceChild(left ? "left_arm" : "right_arm",
            VanillaPartPose.Offset(left ? (baby ? 3f : 5f) : (baby ? -3f : -5f), baby ? 15.5f : 2f, 0f),
            new VanillaCubeDefinition(left ? "left_arm" : "right_arm", 0, 0, 0f, 0f, 0f, 0f, 0f, 0f));
        arm.AddOrReplaceChild("held_item", VanillaPartPose.Offset(0f, baby ? -5f : -10f, baby ? -3f : -4f),
            new VanillaCubeDefinition(
                "held_item",
                0,
                0,
                -4f,
                -4f,
                0f,
                8f,
                8f,
                0f,
                TextureWidth: 16,
                TextureHeight: 16,
                UvWidth: 16,
                UvHeight: 16,
                UsesItemAtlas: true,
                TintSelector: state => EntityModelResolver.ResolveHeldItemTint(state, offhand: left),
                TransformSelector: state => EntityModelResolver.ResolveHeldItemAnchor(state, offhand: left)));
        return new VanillaMeshDefinition(root);
    }
}
