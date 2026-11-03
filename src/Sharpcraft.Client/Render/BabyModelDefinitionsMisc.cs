// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>Baby goat/panda/polar bear/turtle models (default textures).</summary>
public static class BabyModelDefinitionsMisc
{
    public static readonly EntityModelDefinition Goat = new(
        "baby_goat",
        64,
        64,
        [
            PlayerModelDefinition.FromVanilla("left_hind_leg", 1.5f, 19.5f, 3f, -1f, -0.5f, -1f, 2f, 5f, 2f, 29, 12),
            PlayerModelDefinition.FromVanilla("right_hind_leg", -1.5f, 19.5f, 3f, -1f, -0.5f, -1f, 2f, 5f, 2f, 21, 12),
            PlayerModelDefinition.FromVanilla("right_front_leg", -1.5f, 19.5f, -2f, -1f, -0.5f, -1f, 2f, 5f, 2f, 21, 5),
            PlayerModelDefinition.FromVanilla("left_front_leg", 1.5f, 19.5f, -2f, -1f, -0.5f, -1f, 2f, 5f, 2f, 29, 5),
            PlayerModelDefinition.FromVanilla("body", 0f, 17.8f, 0f, -3f, -2.3f, -4.5f, 6f, 5f, 9f, 0, 10),
            PlayerModelDefinition.FromVanillaChild("body_lower", "body", 0f, 0f, 0f, -2.5f, -2.2f, -4f, 5f, 4f, 8f, 0, 24),
            PlayerModelDefinition.FromVanilla("head", 0f, 15.5f, -3f, -2f, -3.8126f, -5.1548f, 4f, 4f, 6f, 0, 0, rotationX: -0.4363f),
            PlayerModelDefinition.FromVanillaChild("right_horn", "head", -1.5f, -1.5f, -1f, 0f, -4.5f, 0f, 1f, 2f, 1f, 24, 0, rotationX: -0.3927f),
            PlayerModelDefinition.FromVanillaChild("left_horn", "head", -1.5f, -1.5f, -1f, 2f, -4.5f, 0f, 1f, 2f, 1f, 24, 0, rotationX: -0.3927f),
            PlayerModelDefinition.FromVanillaChild("right_ear", "head", -1.7f, -2.3126f, 0.1452f, -2f, -0.5f, -0.5f, 2f, 1f, 1f, 0, 12, rotationY: -0.5236f),
            PlayerModelDefinition.FromVanillaChild("left_ear", "head", 1.7f, -2.3126f, 0.1452f, 0f, -0.5f, -0.5f, 2f, 1f, 1f, 0, 12, rotationY: 0.5236f),
            PlayerModelDefinition.FromVanillaChild("head_main", "head", 0f, -1.3126f, -1.1548f, -2f, -2.5f, -4f, 4f, 4f, 6f, 0, 0)
        ],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/goat/goat_baby");

    public static readonly EntityModelDefinition Panda = new(
        "baby_panda",
        64,
        64,
        [
            PlayerModelDefinition.FromVanilla("body", 0f, 18.5f, 2.5f, -4.5f, -3.5f, -5.5f, 9f, 7f, 11f, 0, 11),
            PlayerModelDefinition.FromVanilla("head", 0f, 19f, -3f, -3.5f, -3f, -5f, 7f, 6f, 5f, 0, 0),
            PlayerModelDefinition.FromVanillaChild("mouth", "head", 0f, 0f, 0f, -2f, 1f, -6f, 4f, 2f, 1f, 24, 6),
            PlayerModelDefinition.FromVanillaChild("right_ear", "head", 0f, 0f, 0f, -4.5f, -4f, -3.5f, 3f, 3f, 1f, 24, 0),
            PlayerModelDefinition.FromVanillaChild("left_ear", "head", 0f, 0f, 0f, 1.5f, -4f, -3.5f, 3f, 3f, 1f, 33, 0),
            PlayerModelDefinition.FromVanilla("right_hind_leg", -3f, 22f, 6.5f, -1.5f, 0f, -1.5f, 3f, 2f, 3f, 0, 34),
            PlayerModelDefinition.FromVanilla("left_hind_leg", 3f, 22f, 6.5f, -1.5f, 0f, -1.5f, 3f, 2f, 3f, 12, 34),
            PlayerModelDefinition.FromVanilla("right_front_leg", -3f, 22f, -1.5f, -1.5f, 0f, -1.5f, 3f, 2f, 3f, 0, 29),
            PlayerModelDefinition.FromVanilla("left_front_leg", 3f, 22f, -1.5f, -1.5f, 0f, -1.5f, 3f, 2f, 3f, 12, 29)
        ],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/panda/panda_baby");

    public static readonly EntityModelDefinition PolarBear = new(
        "baby_polar_bear",
        64,
        64,
        [
            PlayerModelDefinition.FromVanilla("body", 0f, 17.5f, 0f, -4f, -3.5f, -6f, 8f, 7f, 12f, 0, 9),
            PlayerModelDefinition.FromVanilla("head", 0f, 18.625f, -5.75f, -3f, -2.625f, -4.25f, 6f, 5f, 4f, 0, 0),
            PlayerModelDefinition.FromVanillaChild("snout", "head", 0f, 0f, 0f, -2f, 0.375f, -6.25f, 4f, 2f, 2f, 20, 3),
            PlayerModelDefinition.FromVanillaChild("right_ear", "head", 0f, 0f, 0f, -4f, -3.625f, -2.75f, 2f, 2f, 1f, 20, 0),
            PlayerModelDefinition.FromVanillaChild("left_ear", "head", 0f, 0f, 0f, 2f, -3.625f, -2.75f, 2f, 2f, 1f, 26, 0),
            PlayerModelDefinition.FromVanilla("right_hind_leg", -2.5f, 21.5f, 4.5f, -1.5f, -0.5f, -1.5f, 3f, 3f, 3f, 0, 34),
            PlayerModelDefinition.FromVanilla("left_hind_leg", 2.5f, 21.5f, 4.5f, -1.5f, -0.5f, -1.5f, 3f, 3f, 3f, 12, 34),
            PlayerModelDefinition.FromVanilla("right_front_leg", -2.5f, 21.5f, -4.5f, -1.5f, -0.5f, -1.5f, 3f, 3f, 3f, 0, 28),
            PlayerModelDefinition.FromVanilla("left_front_leg", 2.5f, 21.5f, -4.5f, -1.5f, -0.5f, -1.5f, 3f, 3f, 3f, 12, 28)
        ],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/bear/polarbear_baby");

    public static readonly EntityModelDefinition Turtle = new(
        "baby_turtle",
        16,
        16,
        [
            PlayerModelDefinition.FromVanilla("body", 0f, 22.9f, 1f, -2f, -1f, -2f, 4f, 2f, 4f, 0, 0),
            PlayerModelDefinition.FromVanilla("head", 0f, 22.9f, -1f, -1.5f, -2f, -3f, 3f, 3f, 3f, 0, 6),
            PlayerModelDefinition.FromVanilla("right_hind_leg", -2f, 23.9f, 2.5f, -2f, 0f, -0.5f, 2f, 0f, 1f, -1, 0),
            PlayerModelDefinition.FromVanilla("left_hind_leg", 2f, 23.9f, 2.5f, 0f, 0f, -0.5f, 2f, 0f, 1f, -1, 1),
            PlayerModelDefinition.FromVanilla("right_front_leg", -2f, 23.9f, -0.5f, -2f, 0f, -0.5f, 2f, 0f, 1f, 8, 6),
            PlayerModelDefinition.FromVanilla("left_front_leg", 2f, 23.9f, -0.5f, 0f, 0f, -0.5f, 2f, 0f, 1f, 8, 7)
        ],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/turtle/turtle_baby");

    public static readonly IReadOnlyDictionary<int, EntityModelDefinition> Models =
        new Dictionary<int, EntityModelDefinition>
        {
            [63] = Goat,
            [97] = Panda,
            [105] = PolarBear,
            [141] = Turtle
        };
}
