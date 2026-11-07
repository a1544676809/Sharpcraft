// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// CreeperModel's static cubes: head/body plus four 4x6x4 legs offset in Z. Parts are declared with the
/// <c>VanillaMeshDefinition</c> DSL and baked via <c>VanillaMeshBake</c> (T-690).
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.monster.creeper.CreeperModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/creeper/CreeperModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class CreeperModelDefinition
{
    public const int CreeperEntityTypeId = 32;

    private static readonly VanillaMeshDefinition CreeperMesh = BuildCreeperMesh();

    public static readonly EntityModelDefinition Definition = EntityModelDefinition.FromTree(
        "creeper",
        64,
        32,
        CreeperMesh,
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/creeper/creeper");

    private static VanillaMeshDefinition BuildCreeperMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        root.AddOrReplaceChild("head", VanillaPartPose.Offset(0f, 6f, 0f),
            new VanillaCubeDefinition("head", 0, 0, -4f, -8f, -4f, 8f, 8f, 8f));
        root.AddOrReplaceChild("body", VanillaPartPose.Offset(0f, 6f, 0f),
            new VanillaCubeDefinition("body", 16, 16, -4f, 0f, -2f, 8f, 12f, 4f));
        root.AddOrReplaceChild("left_hind_leg", VanillaPartPose.Offset(2f, 18f, 4f),
            new VanillaCubeDefinition("left_hind_leg", 0, 16, -2f, 0f, -2f, 4f, 6f, 4f));
        root.AddOrReplaceChild("right_hind_leg", VanillaPartPose.Offset(-2f, 18f, 4f),
            new VanillaCubeDefinition("right_hind_leg", 0, 16, -2f, 0f, -2f, 4f, 6f, 4f));
        root.AddOrReplaceChild("left_front_leg", VanillaPartPose.Offset(2f, 18f, -4f),
            new VanillaCubeDefinition("left_front_leg", 0, 16, -2f, 0f, -2f, 4f, 6f, 4f));
        root.AddOrReplaceChild("right_front_leg", VanillaPartPose.Offset(-2f, 18f, -4f),
            new VanillaCubeDefinition("right_front_leg", 0, 16, -2f, 0f, -2f, 4f, 6f, 4f));
        return new VanillaMeshDefinition(root);
    }
}
