// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// CreakingModel: root/upper_body groups, a multi-box head, two body slabs, long grasper arms and two legs.
/// The emissive eyes layer only contains the head; the reference gates it on the eyesGlowing state.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.monster.creaking.CreakingModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/creaking/CreakingModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class CreakingModelDefinition
{
    public const int CreakingEntityTypeId = 31;

    public static readonly EntityModelDefinition Definition = new(
        "creaking",
        64,
        64,
        [
            PlayerModelDefinition.FromVanilla("root", 0f, 24f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0),
            PlayerModelDefinition.FromVanillaChild("upper_body", "root", -1f, -19f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0),
            PlayerModelDefinition.FromVanillaChild("head", "upper_body", -3f, -11f, 0f, -3f, -10f, -3f, 6f, 10f, 6f, 0, 0),
            PlayerModelDefinition.FromVanillaChild("head_top", "head", 0f, 0f, 0f, -3f, -13f, -3f, 6f, 3f, 6f, 28, 31),
            PlayerModelDefinition.FromVanillaChild("head_plate_right", "head", 0f, 0f, 0f, 3f, -13f, 0f, 9f, 14f, 0f, 12, 40),
            PlayerModelDefinition.FromVanillaChild("head_plate_left", "head", 0f, 0f, 0f, -12f, -14f, 0f, 9f, 14f, 0f, 34, 12),
            PlayerModelDefinition.FromVanillaChild("body", "upper_body", 0f, -7f, 1f, 0f, -3f, -3f, 6f, 13f, 5f, 0, 16),
            PlayerModelDefinition.FromVanillaChild("body_front", "body", 0f, 0f, 0f, -6f, -4f, -3f, 6f, 7f, 5f, 24, 0),
            PlayerModelDefinition.FromVanillaChild("right_arm", "upper_body", -7f, -9.5f, 1.5f, -2f, -1.5f, -1.5f, 3f, 21f, 3f, 22, 13),
            PlayerModelDefinition.FromVanillaChild("right_hand", "right_arm", 0f, 0f, 0f, -2f, 19.5f, -1.5f, 3f, 4f, 3f, 46, 0),
            PlayerModelDefinition.FromVanillaChild("left_arm", "upper_body", 6f, -9f, 0.5f, 0f, -1f, -1.5f, 3f, 16f, 3f, 30, 40),
            PlayerModelDefinition.FromVanillaChild("left_upper", "left_arm", 0f, 0f, 0f, 0f, -5f, -1.5f, 3f, 4f, 3f, 52, 12),
            PlayerModelDefinition.FromVanillaChild("left_hand", "left_arm", 0f, 0f, 0f, 0f, 15f, -1.5f, 3f, 4f, 3f, 52, 19),
            PlayerModelDefinition.FromVanillaChild("left_leg", "root", 1.5f, -16f, 0.5f, -1.5f, 0f, -1.5f, 3f, 16f, 3f, 42, 40),
            PlayerModelDefinition.FromVanillaChild("left_foot", "left_leg", 0f, 0f, 0f, -1.5f, 15.7f, -4.5f, 5f, 0f, 9f, 45, 55),
            PlayerModelDefinition.FromVanillaChild("right_leg", "root", -1f, -17.5f, 0.5f, -3f, -1.5f, -1.5f, 3f, 19f, 3f, 0, 34),
            PlayerModelDefinition.FromVanillaChild("right_foot", "right_leg", 0f, 0f, 0f, -5f, 17.2f, -4.5f, 5f, 0f, 9f, 45, 46),
            PlayerModelDefinition.FromVanillaChild("right_knee", "right_leg", 0f, 0f, 0f, -3f, -4.5f, -1.5f, 3f, 3f, 3f, 12, 34)
        ],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/creaking/creaking",
        1f,
        "entity/creaking/creaking_eyes");
}
