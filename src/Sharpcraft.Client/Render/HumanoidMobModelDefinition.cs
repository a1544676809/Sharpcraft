// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// Humanoid mob definitions. ZombieModel extends HumanoidModel, so its static pose uses the same six part boxes;
/// the only differences are the texture layer and the arm/leg UV offsets.
/// </summary>
public static class HumanoidMobModelDefinition
{
    public const int ZombieEntityTypeId = 154;

    public static readonly EntityModelDefinition Zombie = new(
        "zombie",
        64,
        64,
        [
            new("head", 0f, 24f, 8f, 8f, 8f, 0, 0),
            new("body", 0f, 12f, 8f, 12f, 4f, 16, 16),
            new("left_arm", -6f, 12f, 4f, 12f, 4f, 40, 16, true),
            new("right_arm", 6f, 12f, 4f, 12f, 4f, 40, 16),
            new("left_leg", -1.9f, 0f, 4f, 12f, 4f, 0, 16, true),
            new("right_leg", 1.9f, 0f, 4f, 12f, 4f, 0, 16)
        ],
        PlayerModelDefinition.DefaultFaceTransforms);
}
