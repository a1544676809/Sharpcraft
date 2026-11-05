// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// IronGolemModel: root head/body cubes, two long arms and two legs.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.animal.golem.IronGolemModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/golem/IronGolemModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class IronGolemModelDefinition
{
    public const int IronGolemEntityTypeId = 71;

    public static readonly EntityModelDefinition Definition = new(
        "iron_golem",
        128,
        128,
        [
            PlayerModelDefinition.FromVanilla("head", 0f, -7f, -2f, -4f, -12f, -5.5f, 8f, 10f, 8f, 0, 0),
            PlayerModelDefinition.FromVanilla("nose", 0f, -7f, -2f, -1f, -5f, -7.5f, 2f, 4f, 2f, 24, 0),
            PlayerModelDefinition.FromVanilla("body", 0f, -7f, 0f, -9f, -2f, -6f, 18f, 12f, 11f, 0, 40),
            PlayerModelDefinition.FromVanilla("lower_body", 0f, -7f, 0f, -4.5f, 10f, -3f, 9f, 5f, 6f, 0, 70, grow: 0.5f),
            PlayerModelDefinition.FromVanilla("right_arm", 0f, -7f, 0f, -13f, -2.5f, -3f, 4f, 30f, 6f, 60, 21),
            PlayerModelDefinition.FromVanilla("left_arm", 0f, -7f, 0f, 9f, -2.5f, -3f, 4f, 30f, 6f, 60, 58, mirror: true),
            PlayerModelDefinition.FromVanilla("right_leg", -4f, 11f, 0f, -3.5f, -3f, -3f, 6f, 16f, 5f, 37, 0),
            PlayerModelDefinition.FromVanilla("left_leg", 5f, 11f, 0f, -3.5f, -3f, -3f, 6f, 16f, 5f, 60, 0, mirror: true)
        ],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/iron_golem/iron_golem");
}
