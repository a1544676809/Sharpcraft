// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// Parched: SkeletonModel.createSingleModelDualBodyLayer body (dual body/head inflation cubes) plus the
/// separate HumanoidModel.createMesh(CubeDeformation(0.25), 0) outer layer. PARCHED_ARMOR reuses the
/// shared humanoid armor set, so no new armor mesh is declared here.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.monster.skeleton.SkeletonModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/skeleton/SkeletonModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class ParchedModelDefinition
{
    /// <summary>EntityTypes.PARCHED numeric registry id from BuiltInRegistries.ENTITY_TYPE (161 entries).</summary>
    public const int ParchedEntityTypeId = 98;

    public static readonly EntityModelDefinition Body = EntityModelDefinition.FromTree(
        "parched",
        64,
        64,
        BuildBodyMesh(),
        faceTransforms: null,
        texturePath: "entity/skeleton/parched",
        baselinePx: 0f,
        rawSourceFrame: true,
        sourceFrameBaselinePx: 0f);

    public static readonly EntityModelDefinition Outer = EntityModelDefinition.FromTree(
        "parched_outer",
        64,
        32,
        BuildOuterMesh(),
        faceTransforms: null,
        texturePath: null,
        baselinePx: 0f,
        rawSourceFrame: true,
        sourceFrameBaselinePx: 0f);

    private static VanillaMeshDefinition BuildBodyMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        var head = root.AddOrReplaceChild("head", VanillaPartPose.Zero,
            new VanillaCubeDefinition("head", 0, 0, -4f, -8f, -4f, 8f, 8f, 8f),
            new VanillaCubeDefinition("head_outer", 0, 32, -4f, -8f, -4f, 8f, 8f, 8f, Grow: 0.2f));
        head.AddOrReplaceChild("hat", VanillaPartPose.Zero);
        root.AddOrReplaceChild("right_arm", VanillaPartPose.Offset(-5.5f, 2f, 0f),
            new VanillaCubeDefinition("right_arm", 40, 16, -1f, -2f, -1f, 2f, 12f, 2f),
            new VanillaCubeDefinition("right_arm_outer", 42, 33, -1.55f, -2.025f, -1.5f, 3f, 12f, 3f));
        root.AddOrReplaceChild("left_leg", VanillaPartPose.Offset(2f, 12f, 0f),
            new VanillaCubeDefinition("left_leg", 0, 16, -1f, 0f, -1f, 2f, 12f, 2f),
            new VanillaCubeDefinition("left_leg_outer", 4, 49, -1.5f, 0f, -1.5f, 3f, 12f, 3f));
        root.AddOrReplaceChild("left_arm", VanillaPartPose.Offset(5.5f, 2f, 0f),
            new VanillaCubeDefinition("left_arm", 56, 16, -1f, -2f, -1f, 2f, 12f, 2f),
            new VanillaCubeDefinition("left_arm_outer", 40, 48, -1.45f, -2.025f, -1.5f, 3f, 12f, 3f));
        root.AddOrReplaceChild("right_leg", VanillaPartPose.Offset(-2f, 12f, 0f),
            new VanillaCubeDefinition("right_leg", 0, 16, -1f, 0f, -1f, 2f, 12f, 2f),
            new VanillaCubeDefinition("right_leg_outer", 0, 49, -1.5f, 0f, -1.5f, 3f, 12f, 3f));
        root.AddOrReplaceChild("body", VanillaPartPose.Zero,
            new VanillaCubeDefinition("body", 16, 16, -4f, 0f, -2f, 8f, 12f, 4f),
            new VanillaCubeDefinition("body_mid", 28, 0, -4f, 10f, -2f, 8f, 1f, 4f),
            new VanillaCubeDefinition("body_outer", 16, 48, -4f, 0f, -2f, 8f, 12f, 4f, Grow: 0.025f));
        return new VanillaMeshDefinition(root);
    }

    private static VanillaMeshDefinition BuildOuterMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        var head = root.AddOrReplaceChild("head", VanillaPartPose.Zero,
            new VanillaCubeDefinition("head", 0, 0, -4f, -8f, -4f, 8f, 8f, 8f, Grow: 0.25f));
        head.AddOrReplaceChild("hat", VanillaPartPose.Zero,
            new VanillaCubeDefinition("hat", 32, 0, -4f, -8f, -4f, 8f, 8f, 8f, Grow: 0.75f));
        root.AddOrReplaceChild("right_arm", VanillaPartPose.Offset(-5f, 2f, 0f),
            new VanillaCubeDefinition("right_arm", 40, 16, -3f, -2f, -2f, 4f, 12f, 4f, Grow: 0.25f));
        root.AddOrReplaceChild("left_leg", VanillaPartPose.Offset(1.9f, 12f, 0f),
            new VanillaCubeDefinition("left_leg", 0, 16, -2f, 0f, -2f, 4f, 12f, 4f, Grow: 0.25f, Mirror: true));
        root.AddOrReplaceChild("left_arm", VanillaPartPose.Offset(5f, 2f, 0f),
            new VanillaCubeDefinition("left_arm", 40, 16, -1f, -2f, -2f, 4f, 12f, 4f, Grow: 0.25f, Mirror: true));
        root.AddOrReplaceChild("right_leg", VanillaPartPose.Offset(-1.9f, 12f, 0f),
            new VanillaCubeDefinition("right_leg", 0, 16, -2f, 0f, -2f, 4f, 12f, 4f, Grow: 0.25f));
        root.AddOrReplaceChild("body", VanillaPartPose.Zero,
            new VanillaCubeDefinition("body", 16, 16, -4f, 0f, -2f, 8f, 12f, 4f, Grow: 0.25f));
        return new VanillaMeshDefinition(root);
    }
}
