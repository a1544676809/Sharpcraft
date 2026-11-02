// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// VillagerModel: head/hat, body/jacket, nose, crossed-arm group (three cubes under one rotated pivot) and legs.
/// The reference applies villagerLikeScale 0.9375 to the whole layer.
/// </summary>
public static class VillagerModelDefinition
{
    public const int VillagerEntityTypeId = 143;

    public static readonly EntityModelDefinition Definition = new(
        "villager",
        64,
        64,
        [
            new("head", 0f, 24f, 8f, 10f, 8f, 0, 0, false,
                [new("hat", 0.51f, 32, 0)]),
            new("body", 0f, 12f, 8f, 12f, 6f, 16, 20),
            new("jacket", 0f, 4f, 8f, 20f, 6f, 0, 38),
            new("nose", 0f, 23f, 2f, 4f, 2f, 24, 0, false, null, -5f),
            new("left_arm", -6f, 19f, 4f, 8f, 4f, 44, 22, false, null, -1f,
                PivotX: 0f, PivotY: 21f, PivotZ: -1f, RotationX: -0.75f),
            new("right_arm", 6f, 19f, 4f, 8f, 4f, 44, 22, true, null, -1f,
                PivotX: 0f, PivotY: 21f, PivotZ: -1f, RotationX: -0.75f),
            new("crossed_arms", 0f, 15f, 8f, 4f, 4f, 40, 38, false, null, -1f,
                PivotX: 0f, PivotY: 21f, PivotZ: -1f, RotationX: -0.75f),
            new("right_leg", 2f, 0f, 4f, 12f, 4f, 0, 22),
            new("left_leg", -2f, 0f, 4f, 12f, 4f, 0, 22, true)
        ],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/villager/villager",
        0.9375f);
}
