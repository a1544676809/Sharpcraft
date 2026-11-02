// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// AdultRabbitModel static approximation: body/tail/head/ears/front legs/back haunches. The reference uses nested
/// body/head pivots; this definition keeps the same raw cubes while the shared mesher applies the root transform.
/// </summary>
public static class RabbitModelDefinition
{
    public const int RabbitEntityTypeId = 111;

    public static readonly EntityModelDefinition Definition = new(
        "rabbit",
        64,
        64,
        [
            PlayerModelDefinition.FromVanilla("body", 0f, 23f, 4f, -4f, -6f, -9f, 8f, 6f, 10f, 0, 0, rotationX: 0.3927f),
            PlayerModelDefinition.FromVanilla("tail", 0f, 18f, 4f, -2f, -3.0084f, -1.0125f, 4f, 4f, 4f, 20, 16),
            PlayerModelDefinition.FromVanilla("head", 0f, 17.7f, -4.1f, -2.5f, -3f, -4f, 5f, 5f, 5f, 0, 16),
            PlayerModelDefinition.FromVanilla("left_ear", 0f, 14f, -5f, -1f, -4.3f, -0.1f, 2f, 5f, 1f, 32, 0),
            PlayerModelDefinition.FromVanilla("right_ear", 0f, 14f, -5f, -1f, -4.3f, -0.1f, 2f, 5f, 1f, 26, 0),
            PlayerModelDefinition.FromVanilla("right_front_leg", -2f, 21.4f, -2.3f, -0.9f, -1f, -0.9f, 2f, 4f, 2f, 36, 18, rotationX: 0.3927f),
            PlayerModelDefinition.FromVanilla("left_front_leg", 2f, 21.4f, -2.3f, -1f, -1f, -1f, 2f, 4f, 2f, 44, 18, rotationX: 0.3927f),
            PlayerModelDefinition.FromVanilla("right_haunch", -3f, 23.5f, 4f, -1f, 0f, -5f, 2f, 1f, 6f, 20, 24, rotationY: 0.3927f),
            PlayerModelDefinition.FromVanilla("left_haunch", 3f, 23.5f, 4f, -1f, 0f, -5f, 2f, 1f, 6f, 36, 24, rotationY: -0.3927f)
        ],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/rabbit/rabbit_brown");
}
