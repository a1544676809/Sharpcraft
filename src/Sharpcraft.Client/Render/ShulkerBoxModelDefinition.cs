// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// Static shulker box lid/base bind pose from <c>ShulkerModel.createBoxLayer</c>. Openness is a runtime
/// lid translation + Y rotation and the per-direction transform is runtime state, so both stay in the
/// renderer; this definition only supplies source bind geometry/UV.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.monster.shulker.ShulkerModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/shulker/ShulkerModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class ShulkerBoxModelDefinition
{
    private static readonly VanillaMeshDefinition Mesh = Build();

    public static readonly EntityModelDefinition Definition = new(
        "shulker_box_raw", 64, 64,
        [.. VanillaMeshBake.Bake(Mesh, 0, 0, modelHeight: 0f, rawSourceFrame: true)],
        faceTransforms: null, texturePath: null, baselinePx: 0f, rawSourceFrame: true);

    private static VanillaMeshDefinition Build()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        root.AddOrReplaceChild("lid", VanillaPartPose.Offset(0f, 24f, 0f),
            new VanillaCubeDefinition("lid", 0, 0, -8f, -16f, -8f, 16f, 12f, 16f));
        root.AddOrReplaceChild("base", VanillaPartPose.Offset(0f, 24f, 0f),
            new VanillaCubeDefinition("base", 0, 28, -8f, -8f, -8f, 16f, 8f, 16f));
        return new VanillaMeshDefinition(root);
    }
}
