// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// Minimal held-item layer for adult humanoids. It mirrors the right-arm pose name so the item follows the arm, while
/// the quad samples the independent item atlas through the layer texture selector.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.renderer.entity.layers.ItemInHandLayer", "ref://mc-26.3-analysis/src/net/minecraft/client/renderer/entity/layers/ItemInHandLayer.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Adapter, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
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

    private static VanillaMeshDefinition BuildMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        var rightArm = root.AddOrReplaceChild("right_arm", VanillaPartPose.Offset(-5f, 2f, 0f),
            new VanillaCubeDefinition("right_arm", 0, 0, 0f, 0f, 0f, 0f, 0f, 0f));
        rightArm.AddOrReplaceChild("held_item", VanillaPartPose.Offset(0f, -10f, -4f),
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
                UsesItemAtlas: true));
        return new VanillaMeshDefinition(root);
    }
}
