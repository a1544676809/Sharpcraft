// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>Baby horse/llama/camel/donkey models, with the equine and llama coat variants using *_baby textures.</summary>
public static class BabyModelDefinitionsEquine
{
    public static readonly EntityModelDefinition Horse = new(
        "baby_horse",
        64,
        64,
        EntityModelLayerHelpers.WithHorseMarkings(
        [
            PlayerModelDefinition.FromVanilla("body", 0f, 12.5f, 0f, -4f, -3.5f, -7f, 8f, 7f, 14f, 0, 13),
            PlayerModelDefinition.FromVanillaChild("tail", "body", 0f, -1f, 7f, -1.5f, -1.5f, -1f, 3f, 3f, 8f, 24, 34, rotationX: -0.7418f),
            PlayerModelDefinition.FromVanilla("left_hind_leg", 2.4f, 16f, 5.4f, -1.5f, -1f, -1.5f, 3f, 9f, 3f, 12, 46),
            PlayerModelDefinition.FromVanilla("right_hind_leg", -2.4f, 16f, 5.4f, -1.5f, -1f, -1.5f, 3f, 9f, 3f, 0, 46),
            PlayerModelDefinition.FromVanilla("left_front_leg", 2.4f, 16f, -5.4f, -1.5f, -1f, -1.5f, 3f, 9f, 3f, 12, 34),
            PlayerModelDefinition.FromVanilla("right_front_leg", -2.4f, 16f, -5.4f, -1.5f, -1f, -1.5f, 3f, 9f, 3f, 0, 34),
            PlayerModelDefinition.FromVanilla("head_parts", 0f, 10f, -6f, -2f, -6f, -2f, 4f, 8f, 4f, 30, 0, rotationX: -0.6109f),
            PlayerModelDefinition.FromVanillaChild("head", "head_parts", 0f, -6.0516f, -0.2951f, -3f, -3.9484f, -6.705f, 6f, 4f, 9f, 0, 0),
            PlayerModelDefinition.FromVanillaChild("left_ear", "head", 2f, -4.2484f, 1.9451f, -1f, -2.5f, -0.8f, 2f, 3f, 1f, 0, 4, rotationZ: 0.2618f),
            PlayerModelDefinition.FromVanillaChild("right_ear", "head", -2f, -4.2484f, 1.645f, -1f, -2.5f, -0.5f, 2f, 3f, 1f, 0, 0, rotationZ: -0.2618f),
        ],
        "entity/horse/horse_markings_",
        true),
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/horse/horse_brown_baby");

    public static readonly EntityModelDefinition Llama = new(
        "baby_llama",
        64,
        64,
        [
            PlayerModelDefinition.FromVanilla("head", 0f, 12f, -4f, -3f, -9f, -4f, 6f, 11f, 4f, 0, 0),
            PlayerModelDefinition.FromVanillaChild("snout", "head", 0f, 0f, 0f, -1.5f, -7f, -7f, 3f, 3f, 3f, 0, 15),
            PlayerModelDefinition.FromVanillaChild("ear_right", "head", 0f, 0f, 0f, 0.5f, -11f, -3f, 2f, 2f, 2f, 20, 4),
            PlayerModelDefinition.FromVanillaChild("ear_left", "head", 0f, 0f, 0f, -2.5f, -11f, -3f, 2f, 2f, 2f, 20, 0),
            PlayerModelDefinition.FromVanilla("right_hind_leg", -2.5f, 16.5f, 4.5f, -1.4f, -0.5f, -1.5f, 3f, 8f, 3f, 0, 45),
            PlayerModelDefinition.FromVanilla("left_hind_leg", 2.5f, 16.5f, 4.5f, -1.6f, -0.5f, -1.5f, 3f, 8f, 3f, 12, 45),
            PlayerModelDefinition.FromVanilla("right_front_leg", -2.5f, 16.5f, -3.5f, -1.4f, -0.5f, -1.5f, 3f, 8f, 3f, 0, 34),
            PlayerModelDefinition.FromVanilla("left_front_leg", 2.5f, 16.5f, -3.5f, -1.6f, -0.5f, -1.5f, 3f, 8f, 3f, 12, 34),
            PlayerModelDefinition.FromVanilla("body", 0f, 14f, 2.5f, -4f, -3f, -8.5f, 8f, 6f, 13f, 0, 15),
            PlayerModelDefinition.FromVanilla("right_chest", -8.5f, 4f, 3f, -3f, 0f, 0f, 8f, 8f, 3f, 45, 28, rotationY: -MathF.PI / 2f),
            PlayerModelDefinition.FromVanilla("left_chest", 5.5f, 4f, 3f, -3f, 0f, 0f, 8f, 8f, 3f, 45, 41, rotationY: -MathF.PI / 2f)
        ],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/llama/llama_brown_baby");

    public static readonly EntityModelDefinition Camel = new(
        "baby_camel",
        64,
        64,
        [
            PlayerModelDefinition.FromVanilla("body", 0f, 7f, 0f, -4.5f, -4f, -8f, 9f, 8f, 16f, 0, 14),
            PlayerModelDefinition.FromVanillaChild("tail", "body", 0f, -1.5f, 8.05f, -1.5f, -0.5f, 0f, 3f, 9f, 0f, 50, 38),
            PlayerModelDefinition.FromVanillaChild("head", "body", 0f, 1f, -7.5f, -2.5f, -3f, -7.5f, 5f, 5f, 7f, 20, 0),
            PlayerModelDefinition.FromVanillaChild("head_upper", "head", 0f, 0f, 0f, -2.5f, -12f, -7.5f, 5f, 9f, 5f, 0, 0),
            PlayerModelDefinition.FromVanillaChild("muzzle", "head", 0f, 0f, 0f, -2.5f, -12f, -10.5f, 5f, 4f, 3f, 0, 14),
            PlayerModelDefinition.FromVanillaChild("right_ear", "head", -2.5f, -11f, -4f, -3f, -0.5f, -1f, 3f, 1f, 2f, 37, 0),
            PlayerModelDefinition.FromVanillaChild("left_ear", "head", 2.5f, -11f, -4f, 0f, -0.5f, -1f, 3f, 1f, 2f, 47, 0),
            PlayerModelDefinition.FromVanilla("right_front_leg", -3f, 11.5f, -5.5f, -1.5f, -0.5f, -1.5f, 3f, 13f, 3f, 36, 14),
            PlayerModelDefinition.FromVanilla("left_front_leg", 3f, 11.5f, -5.5f, -1.5f, -0.5f, -1.5f, 3f, 13f, 3f, 48, 14),
            PlayerModelDefinition.FromVanilla("left_hind_leg", 3f, 11.5f, 5.5f, -1.5f, -0.5f, -1.5f, 3f, 13f, 3f, 12, 38),
            PlayerModelDefinition.FromVanilla("right_hind_leg", -3f, 11.5f, 5.5f, -1.5f, -0.5f, -1.5f, 3f, 13f, 3f, 0, 38)
        ],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/camel/camel_baby");

    public static readonly EntityModelDefinition Donkey = new(
        "baby_donkey",
        64,
        64,
        [
            PlayerModelDefinition.FromVanilla("body", 1f, 14f, 0f, -5f, -3f, -7f, 8f, 6f, 14f, 0, 13),
            PlayerModelDefinition.FromVanillaChild("tail", "body", 0f, -1.5f, 6.5f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0),
            PlayerModelDefinition.FromVanillaChild("tail_r1", "tail", 0f, 0f, 0f, -2.5f, -1f, -0.5f, 3f, 3f, 8f, 24, 33, rotationX: -0.7418f),
            PlayerModelDefinition.FromVanillaChild("left_hind_leg", "body", 2.25f, 3.5f, 5.25f, -2.5f, -1.5f, -1.5f, 3f, 8f, 3f, 12, 44),
            PlayerModelDefinition.FromVanillaChild("right_hind_leg", "body", -2.4f, 3.5f, 5.4f, -2.5f, -1.5f, -1.5f, 3f, 8f, 3f, 0, 44),
            PlayerModelDefinition.FromVanillaChild("left_front_leg", "body", 2.4f, 3.5f, -5.3f, -2.5f, -1.5f, -1.5f, 3f, 8f, 3f, 12, 33),
            PlayerModelDefinition.FromVanillaChild("right_front_leg", "body", -2.4f, 3.5f, -5.4f, -2.5f, -1.5f, -1.5f, 3f, 8f, 3f, 0, 33),
            PlayerModelDefinition.FromVanillaChild("head_parts", "body", 0f, -3f, -5f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0),
            PlayerModelDefinition.FromVanillaChild("neck_r1", "head_parts", 0f, 0f, 0f, -3f, -6f, -3f, 4f, 8f, 4f, 30, 9, rotationX: 0.3927f),
            PlayerModelDefinition.FromVanillaChild("head", "head_parts", 0f, -5f, -3f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0),
            PlayerModelDefinition.FromVanillaChild("head_r1", "head", 0f, -1f, 1f, -4f, -3.6f, -8.4f, 6f, 4f, 9f, 0, 0, rotationX: 0.3927f),
            PlayerModelDefinition.FromVanillaChild("left_ear", "head", 2f, -3.5f, -1f, -2f, -6.5f, -0.3f, 2f, 7f, 1f, 0, 0, rotationX: 0.48f, rotationZ: 0.48f),
            PlayerModelDefinition.FromVanillaChild("right_ear", "head", -2f, -3.5f, -1f, -2f, -6.5f, -0.3f, 2f, 7f, 1f, 22, 0, mirror: true, rotationX: 0.48f, rotationZ: -0.48f),
            PlayerModelDefinition.FromVanillaChild("right_chest", "body", -1f, 10f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0),
            PlayerModelDefinition.FromVanillaChild("left_chest", "body", -1f, 10f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0)
        ],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/horse/donkey_baby");

    public static readonly IReadOnlyDictionary<int, EntityModelDefinition> Models =
        new Dictionary<int, EntityModelDefinition>
        {
            [HorseModelDefinition.HorseEntityTypeId] = Horse,
            [LlamaModelDefinition.LlamaEntityTypeId] = Llama,
            [CamelModelDefinition.CamelEntityTypeId] = Camel,
            [37] = Donkey
        };

    public static readonly IReadOnlyDictionary<(int TypeId, string Key), EntityModelDefinition> Variants = BuildVariants();

    private static Dictionary<(int, string), EntityModelDefinition> BuildVariants()
    {
        var variants = new Dictionary<(int, string), EntityModelDefinition>
        {
            [(HorseModelDefinition.HorseEntityTypeId, "brown")] = Horse,
            [(LlamaModelDefinition.LlamaEntityTypeId, "brown")] = Llama
        };

        var horseFiles = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["white"] = "horse_white",
            ["creamy"] = "horse_creamy",
            ["chestnut"] = "horse_chestnut",
            ["brown"] = "horse_brown",
            ["black"] = "horse_black",
            ["gray"] = "horse_gray",
            ["dark_brown"] = "horse_darkbrown"
        };
        foreach (var key in HorseModelDefinition.VariantKeys)
        {
            if (key != "brown")
            {
                variants[(HorseModelDefinition.HorseEntityTypeId, key)] = new("baby_horse_" + key, 64, 64, Horse.Parts, PlayerModelDefinition.DefaultFaceTransforms, "entity/horse/" + horseFiles[key] + "_baby");
            }
        }

        foreach (var key in LlamaModelDefinition.VariantKeys)
        {
            if (key != "brown")
            {
                variants[(LlamaModelDefinition.LlamaEntityTypeId, key)] = new("baby_llama_" + key, 64, 64, Llama.Parts, PlayerModelDefinition.DefaultFaceTransforms, "entity/llama/llama_" + key + "_baby");
            }
        }

        return variants;
    }
}


