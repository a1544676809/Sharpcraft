// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// AdultChickenModel: head with beak/red_thing, rotated body, two legs and two wings.
/// </summary>
public static class ChickenModelDefinition
{
    public const int ChickenEntityTypeId = 26;

    public static readonly EntityModelDefinition Definition = new(
        "chicken",
        64,
        32,
        [
            PlayerModelDefinition.FromVanilla("head", 0f, 15f, -4f, -2f, -6f, -2f, 4f, 6f, 3f, 0, 0),
            PlayerModelDefinition.FromVanilla("beak", 0f, 15f, -4f, -2f, -4f, -4f, 4f, 2f, 2f, 14, 0),
            PlayerModelDefinition.FromVanilla("red_thing", 0f, 15f, -4f, -1f, -2f, -3f, 2f, 2f, 2f, 14, 4),
            PlayerModelDefinition.FromVanilla("body", 0f, 16f, 0f, -3f, -4f, -3f, 6f, 8f, 6f, 0, 9, rotationX: -MathF.PI / 2f),
            PlayerModelDefinition.FromVanilla("right_leg", -2f, 19f, 1f, -1f, 0f, -3f, 3f, 5f, 3f, 26, 0),
            PlayerModelDefinition.FromVanilla("left_leg", 1f, 19f, 1f, -1f, 0f, -3f, 3f, 5f, 3f, 26, 0),
            PlayerModelDefinition.FromVanilla("right_wing", -4f, 13f, 0f, 0f, 0f, -3f, 1f, 4f, 6f, 24, 13),
            PlayerModelDefinition.FromVanilla("left_wing", 4f, 13f, 0f, -1f, 0f, -3f, 1f, 4f, 6f, 24, 13, mirror: true)
        ],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/chicken/chicken_temperate");
}
