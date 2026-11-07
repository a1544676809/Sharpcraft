// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// CreakingModel: root/upper_body groups, a multi-box head, two body slabs, long grasper arms and two legs.
/// The emissive eyes layer only contains the head; the reference gates it on the eyesGlowing state. Parts are declared
/// with the <c>VanillaMeshDefinition</c> DSL and baked via <c>VanillaMeshBake</c> (T-692).
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.monster.creaking.CreakingModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/creaking/CreakingModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class CreakingModelDefinition
{
    public const int CreakingEntityTypeId = 31;

    private static readonly VanillaMeshDefinition CreakingMesh = BuildCreakingMesh();

    public static readonly EntityModelDefinition Definition = EntityModelDefinition.FromTree(
        "creaking",
        64,
        64,
        CreakingMesh,
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/creaking/creaking",
        emissiveTexturePath: "entity/creaking/creaking_eyes");

    private static VanillaMeshDefinition BuildCreakingMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        var rootPart = root.AddOrReplaceChild("root", VanillaPartPose.Offset(0f, 24f, 0f),
            new VanillaCubeDefinition("root", 0, 0, 0f, 0f, 0f, 0f, 0f, 0f));
        var upperBody = rootPart.AddOrReplaceChild("upper_body", VanillaPartPose.Offset(-1f, -19f, 0f),
            new VanillaCubeDefinition("upper_body", 0, 0, 0f, 0f, 0f, 0f, 0f, 0f));
        var head = upperBody.AddOrReplaceChild("head", VanillaPartPose.Offset(-3f, -11f, 0f),
            new VanillaCubeDefinition("head", 0, 0, -3f, -10f, -3f, 6f, 10f, 6f));
        head.AddOrReplaceChild("head_top", VanillaPartPose.Zero,
            new VanillaCubeDefinition("head_top", 28, 31, -3f, -13f, -3f, 6f, 3f, 6f));
        head.AddOrReplaceChild("head_plate_right", VanillaPartPose.Zero,
            new VanillaCubeDefinition("head_plate_right", 12, 40, 3f, -13f, 0f, 9f, 14f, 0f));
        head.AddOrReplaceChild("head_plate_left", VanillaPartPose.Zero,
            new VanillaCubeDefinition("head_plate_left", 34, 12, -12f, -14f, 0f, 9f, 14f, 0f));
        var body = upperBody.AddOrReplaceChild("body", VanillaPartPose.Offset(0f, -7f, 1f),
            new VanillaCubeDefinition("body", 0, 16, 0f, -3f, -3f, 6f, 13f, 5f));
        body.AddOrReplaceChild("body_front", VanillaPartPose.Zero,
            new VanillaCubeDefinition("body_front", 24, 0, -6f, -4f, -3f, 6f, 7f, 5f));
        var rightArm = upperBody.AddOrReplaceChild("right_arm", VanillaPartPose.Offset(-7f, -9.5f, 1.5f),
            new VanillaCubeDefinition("right_arm", 22, 13, -2f, -1.5f, -1.5f, 3f, 21f, 3f));
        rightArm.AddOrReplaceChild("right_hand", VanillaPartPose.Zero,
            new VanillaCubeDefinition("right_hand", 46, 0, -2f, 19.5f, -1.5f, 3f, 4f, 3f));
        var leftArm = upperBody.AddOrReplaceChild("left_arm", VanillaPartPose.Offset(6f, -9f, 0.5f),
            new VanillaCubeDefinition("left_arm", 30, 40, 0f, -1f, -1.5f, 3f, 16f, 3f));
        leftArm.AddOrReplaceChild("left_upper", VanillaPartPose.Zero,
            new VanillaCubeDefinition("left_upper", 52, 12, 0f, -5f, -1.5f, 3f, 4f, 3f));
        leftArm.AddOrReplaceChild("left_hand", VanillaPartPose.Zero,
            new VanillaCubeDefinition("left_hand", 52, 19, 0f, 15f, -1.5f, 3f, 4f, 3f));
        var leftLeg = rootPart.AddOrReplaceChild("left_leg", VanillaPartPose.Offset(1.5f, -16f, 0.5f),
            new VanillaCubeDefinition("left_leg", 42, 40, -1.5f, 0f, -1.5f, 3f, 16f, 3f));
        leftLeg.AddOrReplaceChild("left_foot", VanillaPartPose.Zero,
            new VanillaCubeDefinition("left_foot", 45, 55, -1.5f, 15.7f, -4.5f, 5f, 0f, 9f));
        var rightLeg = rootPart.AddOrReplaceChild("right_leg", VanillaPartPose.Offset(-1f, -17.5f, 0.5f),
            new VanillaCubeDefinition("right_leg", 0, 34, -3f, -1.5f, -1.5f, 3f, 19f, 3f));
        rightLeg.AddOrReplaceChild("right_foot", VanillaPartPose.Zero,
            new VanillaCubeDefinition("right_foot", 45, 46, -5f, 17.2f, -4.5f, 5f, 0f, 9f));
        rightLeg.AddOrReplaceChild("right_knee", VanillaPartPose.Zero,
            new VanillaCubeDefinition("right_knee", 12, 34, -3f, -4.5f, -1.5f, 3f, 3f, 3f));
        return new VanillaMeshDefinition(root);
    }
}
