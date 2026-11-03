// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// Baby models whose geometry differs from the adult model (pig, cow, chicken). Each variant keeps the same geometry and
/// swaps to the matching <c>*_baby</c> texture.
/// </summary>
public static class BabyModelDefinitions
{
    public static readonly EntityModelDefinition Pig = new(
        "baby_pig",
        32,
        32,
        [
            PlayerModelDefinition.FromVanilla("body", 0f, 19f, 0.5f, -3.5f, -3f, -4.5f, 7f, 6f, 9f, 0, 0),
            PlayerModelDefinition.FromVanilla("head", 0f, 19f, -2f, -3.5f, -5f, -5f, 7f, 6f, 6f, 0, 15, grow: 0.025f),
            PlayerModelDefinition.FromVanillaChild("snout", "head", 0f, 0f, 0f, -1.5f, -1.975f, -6f, 3f, 2f, 1f, 6, 27, grow: 0.015f),
            PlayerModelDefinition.FromVanilla("left_front_leg", 2.5f, 22f, -3f, -1f, 0f, -1f, 2f, 2f, 2f, 0, 0),
            PlayerModelDefinition.FromVanilla("right_front_leg", -2.5f, 22f, -3f, -1f, 0f, -1f, 2f, 2f, 2f, 23, 0),
            PlayerModelDefinition.FromVanilla("left_hind_leg", 2.5f, 22f, 4f, -1f, 0f, -1f, 2f, 2f, 2f, 0, 4),
            PlayerModelDefinition.FromVanilla("right_hind_leg", -2.5f, 22f, 4f, -1f, 0f, -1f, 2f, 2f, 2f, 23, 4)
        ],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/pig/pig_temperate_baby");

    public static readonly EntityModelDefinition Cow = new(
        "baby_cow",
        64,
        64,
        [
            PlayerModelDefinition.FromVanilla("head", 0f, 13.569f, -5.1667f, -3f, -4.569f, -4.8333f, 6f, 6f, 5f, 0, 18),
            PlayerModelDefinition.FromVanillaChild("ear_right", "head", 0f, 0f, 0f, 3f, -5.569f, -3.8333f, 1f, 2f, 1f, 8, 29),
            PlayerModelDefinition.FromVanillaChild("ear_left", "head", 0f, 0f, 0f, -4f, -5.569f, -3.8333f, 1f, 2f, 1f, 4, 29, mirror: true),
            PlayerModelDefinition.FromVanillaChild("snout", "head", 0f, 0f, 0f, -2f, -1.569f, -5.8333f, 4f, 3f, 1f, 12, 29),
            PlayerModelDefinition.FromVanilla("body", 3f, 19f, -5f, -7f, -7f, -1f, 8f, 6f, 12f, 0, 0),
            PlayerModelDefinition.FromVanilla("right_front_leg", -2.5f, 18f, -3.5f, -1.5f, 0f, -1.5f, 3f, 6f, 3f, 22, 18),
            PlayerModelDefinition.FromVanilla("left_front_leg", 2.5f, 18f, -3.5f, -1.5f, 0f, -1.5f, 3f, 6f, 3f, 34, 18),
            PlayerModelDefinition.FromVanilla("right_hind_leg", -2.5f, 18f, 3.5f, -1.5f, 0f, -1.5f, 3f, 6f, 3f, 22, 27),
            PlayerModelDefinition.FromVanilla("left_hind_leg", 2.5f, 18f, 3.5f, -1.5f, 0f, -1.5f, 3f, 6f, 3f, 34, 27)
        ],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/cow/cow_temperate_baby");

    public static readonly EntityModelDefinition Chicken = new(
        "baby_chicken",
        16,
        16,
        [
            PlayerModelDefinition.FromVanilla("body", 0f, 20.25f, -1.25f, -2f, -2.25f, -0.75f, 4f, 4f, 4f, 0, 0),
            PlayerModelDefinition.FromVanillaChild("beak", "body", 0f, 0f, 0f, -1f, -0.25f, -1.75f, 2f, 1f, 1f, 10, 8),
            PlayerModelDefinition.FromVanilla("left_leg", 1f, 22f, 0.5f, -0.5f, 0f, 0f, 1f, 2f, 0f, 2, 2),
            PlayerModelDefinition.FromVanillaChild("left_foot", "left_leg", 0f, 0f, 0f, -0.5f, 2f, -1f, 1f, 0f, 1f, 0, 1),
            PlayerModelDefinition.FromVanilla("right_leg", -1f, 22f, 0.5f, -0.5f, 0f, 0f, 1f, 2f, 0f, 0, 2),
            PlayerModelDefinition.FromVanillaChild("right_foot", "right_leg", 0f, 0f, 0f, -0.5f, 2f, -1f, 1f, 0f, 1f, 0, 0),
            PlayerModelDefinition.FromVanilla("right_wing", 2f, 20f, 0f, 0f, 0f, -1f, 1f, 0f, 2f, 6, 8),
            PlayerModelDefinition.FromVanilla("left_wing", -2f, 20f, 0f, -1f, 0f, -1f, 1f, 0f, 2f, 4, 8)
        ],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/chicken/chicken_temperate_baby");

    public static readonly IReadOnlyDictionary<int, EntityModelDefinition> Models =
        new Dictionary<int, EntityModelDefinition>
        {
            [PigModelDefinition.PigEntityTypeId] = Pig,
            [CowModelDefinition.CowEntityTypeId] = Cow,
            [ChickenModelDefinition.ChickenEntityTypeId] = Chicken
        };

    public static readonly IReadOnlyDictionary<(int TypeId, string Key), EntityModelDefinition> Variants = BuildVariants();

    private static Dictionary<(int, string), EntityModelDefinition> BuildVariants()
    {
        var variants = new Dictionary<(int, string), EntityModelDefinition>
        {
            [(PigModelDefinition.PigEntityTypeId, "temperate")] = Pig,
            [(CowModelDefinition.CowEntityTypeId, "temperate")] = Cow,
            [(ChickenModelDefinition.ChickenEntityTypeId, "temperate")] = Chicken
        };

        variants[(PigModelDefinition.PigEntityTypeId, "warm")] = new("baby_pig_warm", 32, 32, Pig.Parts, PlayerModelDefinition.DefaultFaceTransforms, "entity/pig/pig_warm_baby");
        variants[(PigModelDefinition.PigEntityTypeId, "cold")] = new("baby_pig_cold", 32, 32, Pig.Parts, PlayerModelDefinition.DefaultFaceTransforms, "entity/pig/pig_cold_baby");
        variants[(CowModelDefinition.CowEntityTypeId, "warm")] = new("baby_cow_warm", 64, 64, Cow.Parts, PlayerModelDefinition.DefaultFaceTransforms, "entity/cow/cow_warm_baby");
        variants[(CowModelDefinition.CowEntityTypeId, "cold")] = new("baby_cow_cold", 64, 64, Cow.Parts, PlayerModelDefinition.DefaultFaceTransforms, "entity/cow/cow_cold_baby");
        variants[(ChickenModelDefinition.ChickenEntityTypeId, "warm")] = new("baby_chicken_warm", 16, 16, Chicken.Parts, PlayerModelDefinition.DefaultFaceTransforms, "entity/chicken/chicken_warm_baby");
        variants[(ChickenModelDefinition.ChickenEntityTypeId, "cold")] = new("baby_chicken_cold", 16, 16, Chicken.Parts, PlayerModelDefinition.DefaultFaceTransforms, "entity/chicken/chicken_cold_baby");
        return variants;
    }
}

