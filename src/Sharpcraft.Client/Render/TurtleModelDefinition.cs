// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// AdultTurtleModel: root head/body shell/belly, four flat legs and the optional egg belly.
/// </summary>
public static class TurtleModelDefinition
{
    public const int TurtleEntityTypeId = 141;

    public static readonly EntityModelDefinition Definition = new(
        "turtle",
        128,
        64,
        [
            PlayerModelDefinition.FromVanilla("head", 0f, 19f, -10f, -3f, -1f, -3f, 6f, 5f, 6f, 3, 0),
            PlayerModelDefinition.FromVanilla("body", 0f, 11f, -10f, -9.5f, 3f, -10f, 19f, 20f, 6f, 7, 37, rotationX: -MathF.PI / 2f),
            PlayerModelDefinition.FromVanilla("belly", 0f, 11f, -10f, -5.5f, 3f, -13f, 11f, 18f, 3f, 31, 1, rotationX: -MathF.PI / 2f),
            PlayerModelDefinition.FromVanilla("egg_belly", 0f, 11f, -10f, -4.5f, 3f, -14f, 9f, 18f, 1f, 70, 33, rotationX: -MathF.PI / 2f),
            PlayerModelDefinition.FromVanilla("right_hind_leg", -3.5f, 22f, 11f, -2f, 0f, 0f, 4f, 1f, 10f, 1, 23),
            PlayerModelDefinition.FromVanilla("left_hind_leg", 3.5f, 22f, 11f, -2f, 0f, 0f, 4f, 1f, 10f, 1, 12),
            PlayerModelDefinition.FromVanilla("right_front_leg", -5f, 21f, -4f, -13f, 0f, -2f, 13f, 1f, 5f, 27, 30),
            PlayerModelDefinition.FromVanilla("left_front_leg", 5f, 21f, -4f, 0f, 0f, -2f, 13f, 1f, 5f, 27, 24)
        ],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/turtle/turtle");
}
