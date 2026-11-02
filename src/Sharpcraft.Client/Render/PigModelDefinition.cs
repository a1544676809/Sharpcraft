// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// PigModel / QuadrupedModel with legSize 6: rotated body, head with snout, and four legs.
/// </summary>
public static class PigModelDefinition
{
    public const int PigEntityTypeId = 101;

    public static readonly EntityModelDefinition Definition = new(
        "pig",
        64,
        64,
        [
            PlayerModelDefinition.FromVanilla("head", 0f, 12f, -6f, -4f, -4f, -8f, 8f, 8f, 8f, 0, 0),
            PlayerModelDefinition.FromVanilla("snout", 0f, 12f, -6f, -2f, 0f, -9f, 4f, 3f, 1f, 16, 16),
            PlayerModelDefinition.FromVanilla("body", 0f, 11f, 2f, -5f, -10f, -7f, 10f, 16f, 8f, 28, 8,
                rotationX: -MathF.PI / 2f),
            PlayerModelDefinition.FromVanilla("right_hind_leg", -3f, 18f, 7f, -2f, 0f, -2f, 4f, 6f, 4f, 0, 16),
            PlayerModelDefinition.FromVanilla("left_hind_leg", 3f, 18f, 7f, -2f, 0f, -2f, 4f, 6f, 4f, 0, 16, mirror: true),
            PlayerModelDefinition.FromVanilla("right_front_leg", -3f, 18f, -5f, -2f, 0f, -2f, 4f, 6f, 4f, 0, 16),
            PlayerModelDefinition.FromVanilla("left_front_leg", 3f, 18f, -5f, -2f, 0f, -2f, 4f, 6f, 4f, 0, 16, mirror: true)
        ],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/pig/pig_temperate");
}
