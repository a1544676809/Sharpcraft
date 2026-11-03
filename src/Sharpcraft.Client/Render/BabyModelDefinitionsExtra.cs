// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>Baby cat/wolf/fox/rabbit models. Each has its own geometry and uses the matching *_baby textures.</summary>
public static class BabyModelDefinitionsExtra
{
    public static readonly EntityModelDefinition Cat = new(
        "baby_cat",
        32,
        32,
        EntityModelLayerHelpers.WithCollar(
        [
            PlayerModelDefinition.FromVanilla("head", 0f, 20f, -3.125f, -2.5f, -3f, -2.875f, 5f, 4f, 4f, 0, 0),
            PlayerModelDefinition.FromVanillaChild("right_ear", "head", 0f, 0f, 0f, -2f, -4f, -0.875f, 1f, 1f, 2f, 18, 0),
            PlayerModelDefinition.FromVanillaChild("left_ear", "head", 0f, 0f, 0f, 1f, -4f, -0.875f, 1f, 1f, 2f, 24, 0),
            PlayerModelDefinition.FromVanillaChild("nose", "head", 0f, 0f, 0f, -1.5f, -1f, -3.875f, 3f, 2f, 1f, 18, 3),
            PlayerModelDefinition.FromVanilla("left_front_leg", 1f, 22f, -1.5f, -0.5f, 0f, -1f, 1f, 2f, 2f, 18, 18),
            PlayerModelDefinition.FromVanilla("right_front_leg", -1f, 22f, -1.5f, -0.5f, 0f, -1f, 1f, 2f, 2f, 12, 18),
            PlayerModelDefinition.FromVanilla("left_hind_leg", 1f, 22f, 2.5f, -0.5f, 0f, -1f, 1f, 2f, 2f, 18, 22),
            PlayerModelDefinition.FromVanilla("right_hind_leg", -1f, 22f, 2.5f, -0.5f, 0f, -1f, 1f, 2f, 2f, 12, 22),
            PlayerModelDefinition.FromVanilla("body", 0f, 20.5f, 0.5f, -2f, -1.5f, -3.5f, 4f, 3f, 7f, 0, 8),
            PlayerModelDefinition.FromVanilla("tail1", 0f, 19.107f, 3.9151f, -0.5f, -0.107f, 0.0849f, 1f, 1f, 5f, 0, 18, rotationX: 0.567232f),
            PlayerModelDefinition.FromVanilla("tail2", 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0)
        ],
        "entity/cat/cat_collar_baby"),
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/cat/cat_tabby_baby");

    public static readonly EntityModelDefinition Wolf = new(
        "baby_wolf",
        32,
        32,
        EntityModelLayerHelpers.WithCollar(
        [
            PlayerModelDefinition.FromVanilla("head", 0f, 18.25f, -4f, -2.99f, -3.25f, -3f, 6f, 5f, 5f, 0, 12, grow: 0.025f),
            PlayerModelDefinition.FromVanillaChild("snout", "head", 0f, 0f, 0f, -1.5f, -0.24f, -5f, 3f, 2f, 2f, 17, 12),
            PlayerModelDefinition.FromVanillaChild("right_ear", "head", -2f, -4.25f, -0.5f, -1f, -1f, -0.5f, 2f, 2f, 1f, 0, 5),
            PlayerModelDefinition.FromVanillaChild("left_ear", "head", 2f, -4.25f, -0.5f, -1f, -1f, -0.5f, 2f, 2f, 1f, 20, 5),
            PlayerModelDefinition.FromVanilla("body", 0f, 19f, 0f, -3f, -2f, -4f, 6f, 4f, 8f, 0, 0),
            PlayerModelDefinition.FromVanilla("right_hind_leg", -1.5f, 21f, 3f, -1f, 0f, -1f, 2f, 3f, 2f, 0, 22),
            PlayerModelDefinition.FromVanilla("left_hind_leg", 1.5f, 21f, 3f, -1f, 0f, -1f, 2f, 3f, 2f, 8, 22),
            PlayerModelDefinition.FromVanilla("right_front_leg", -1.5f, 21f, -3f, -1f, 0f, -1f, 2f, 3f, 2f, 0, 0),
            PlayerModelDefinition.FromVanilla("left_front_leg", 1.5f, 21f, -3f, -1f, 0f, -1f, 2f, 3f, 2f, 20, 0),
            PlayerModelDefinition.FromVanilla("tail", 0f, 19f, 3f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0, rotationX: 0.5236f),
            PlayerModelDefinition.FromVanillaChild("tail_r1", "tail", 0f, -0.6f, 0.2f, -1f, -5.7f, -1f, 2f, 6f, 2f, 22, 16, rotationX: -3.1f),
        ],
        "entity/wolf/wolf_collar_baby"),
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/wolf/wolf_baby",
        textureSelector: state => state.IsTame ? "entity/wolf/wolf_tame_baby" : state.IsAngry ? "entity/wolf/wolf_angry_baby" : "entity/wolf/wolf_baby",
        additionalTexturePaths: ["entity/wolf/wolf_tame_baby", "entity/wolf/wolf_angry_baby"]);

    public static readonly EntityModelDefinition Fox = new(
        "baby_fox",
        32,
        32,
        [
            PlayerModelDefinition.FromVanilla("head", 0f, 18.125f, 0.125f, -3f, -2.125f, -5.125f, 6f, 5f, 5f, 0, 0),
            PlayerModelDefinition.FromVanillaChild("snout", "head", 0f, 0f, 0f, -1f, 0.875f, -7.125f, 2f, 2f, 2f, 18, 20),
            PlayerModelDefinition.FromVanillaChild("right_ear", "head", 0f, 0f, 0f, -3f, -4.125f, -4.125f, 2f, 2f, 1f, 22, 8),
            PlayerModelDefinition.FromVanillaChild("left_ear", "head", 0f, 0f, 0f, 1f, -4.125f, -4.125f, 2f, 2f, 1f, 22, 11),
            PlayerModelDefinition.FromVanilla("right_hind_leg", -1.5f, 22f, 4f, -1f, 0f, -1f, 2f, 2f, 2f, 22, 4),
            PlayerModelDefinition.FromVanilla("left_hind_leg", 1.5f, 22f, 4f, -1f, 0f, -1f, 2f, 2f, 2f, 22, 0),
            PlayerModelDefinition.FromVanilla("right_front_leg", -1.5f, 22f, 0f, -1f, 0f, -1f, 2f, 2f, 2f, 22, 4),
            PlayerModelDefinition.FromVanilla("left_front_leg", 1.5f, 22f, 0f, -1f, 0f, -1f, 2f, 2f, 2f, 22, 0),
            PlayerModelDefinition.FromVanilla("body", 0f, 20f, 2f, -2.5f, -2f, -3f, 5f, 4f, 6f, 0, 10),
            PlayerModelDefinition.FromVanillaChild("tail", "body", 0f, -0.5f, 3f, -1.5f, -1.48f, -1f, 3f, 3f, 6f, 0, 20)
        ],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/fox/fox_baby",
        textureSelector: state => state.IsSleeping ? "entity/fox/fox_sleep_baby" : "entity/fox/fox_baby",
        additionalTexturePaths: ["entity/fox/fox_sleep_baby"]);

    public static readonly EntityModelDefinition Rabbit = new(
        "baby_rabbit",
        32,
        32,
        [
            PlayerModelDefinition.FromVanilla("body", 0f, 23f, 1.6f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0),
            PlayerModelDefinition.FromVanillaChild("body_r1", "body", 0f, -2f, -1.6f, -2f, -2f, -3f, 4f, 3f, 6f, 0, 8, rotationX: -0.5236f),
            PlayerModelDefinition.FromVanillaChild("tail", "body", 0f, -2.2f, 2f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0),
            PlayerModelDefinition.FromVanillaChild("tail_r1", "tail", -0.1f, 0f, 0f, -1.4f, -2.0268f, -1.0177f, 3f, 3f, 3f, 0, 21, rotationX: -0.5236f),
            PlayerModelDefinition.FromVanillaChild("head", "body", 0f, -5f, -2.6f, -2.5f, -3f, -3f, 5f, 4f, 4f, 0, 0),
            PlayerModelDefinition.FromVanillaChild("right_ear", "head", -1.5f, -3.5f, -0.5f, -1f, -3.5f, -0.5f, 2f, 4f, 1f, 18, 0),
            PlayerModelDefinition.FromVanillaChild("left_ear", "head", 1.5f, -3.5f, -0.5f, -1f, -3.5f, -0.5f, 2f, 4f, 1f, 24, 0),
            PlayerModelDefinition.FromVanillaChild("frontlegs", "body", 0f, -2.5f, -2.6f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0),
            PlayerModelDefinition.FromVanillaChild("left_front_leg", "frontlegs", 1f, 1f, -0.5f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0, rotationX: 0.3927f),
            PlayerModelDefinition.FromVanillaChild("left_front_leg_r1", "left_front_leg", 0f, 1f, 0f, -0.5f, -1.5f, -0.5f, 1f, 3f, 1f, 18, 8, rotationX: -0.3927f),
            PlayerModelDefinition.FromVanillaChild("right_front_leg", "frontlegs", -1f, 1f, -0.5f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0, rotationX: 0.3927f),
            PlayerModelDefinition.FromVanillaChild("right_front_leg_r1", "right_front_leg", 0f, 1f, 0f, -0.5f, -1.5f, -0.5f, 1f, 3f, 1f, 14, 8, rotationX: -0.3927f),
            PlayerModelDefinition.FromVanilla("backlegs", 0f, 23f, 2f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0),
            PlayerModelDefinition.FromVanillaChild("left_hind_leg", "backlegs", 1.5f, 0.5f, 0.5f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0, rotationY: 3.1416f),
            PlayerModelDefinition.FromVanillaChild("left_haunch", "left_hind_leg", 1f, 0f, 0.5f, -2f, -0.5f, 0f, 2f, 1f, 3f, 10, 17, rotationY: -0.7854f),
            PlayerModelDefinition.FromVanillaChild("right_hind_leg", "backlegs", -1.5f, 0.5f, 0.5f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0, rotationY: 3.1416f),
            PlayerModelDefinition.FromVanillaChild("right_haunch", "right_hind_leg", 0.5f, 0f, -0.9f, -2f, -0.5f, 0f, 2f, 1f, 3f, 0, 17, rotationY: 0.7854f)
        ],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/rabbit/rabbit_brown_baby");

    public static readonly IReadOnlyDictionary<int, EntityModelDefinition> Models =
        new Dictionary<int, EntityModelDefinition>
        {
            [CatModelDefinition.CatEntityTypeId] = Cat,
            [WolfModelDefinition.WolfEntityTypeId] = Wolf,
            [FoxModelDefinition.FoxEntityTypeId] = Fox,
            [RabbitModelDefinition.RabbitEntityTypeId] = Rabbit
        };

    public static readonly IReadOnlyDictionary<(int TypeId, string Key), EntityModelDefinition> Variants = BuildVariants();

    private static Dictionary<(int, string), EntityModelDefinition> BuildVariants()
    {
        var variants = new Dictionary<(int, string), EntityModelDefinition>
        {
            [(CatModelDefinition.CatEntityTypeId, "tabby")] = Cat,
            [(WolfModelDefinition.WolfEntityTypeId, "pale")] = Wolf,
            [(FoxModelDefinition.FoxEntityTypeId, "red")] = Fox,
            [(RabbitModelDefinition.RabbitEntityTypeId, "brown")] = Rabbit
        };

        foreach (var key in CatModelDefinition.VariantKeys)
        {
            if (key != "tabby")
            {
                variants[(CatModelDefinition.CatEntityTypeId, key)] = new("baby_cat_" + key, 32, 32, Cat.Parts, PlayerModelDefinition.DefaultFaceTransforms, "entity/cat/cat_" + key + "_baby");
            }
        }

        string[] wolfFiles =
            ["wolf", "wolf_spotted", "wolf_snowy", "wolf_black", "wolf_ashen", "wolf_rusty", "wolf_woods", "wolf_chestnut", "wolf_striped"];
        for (var i = 0; i < WolfModelDefinition.VariantKeys.Count; i++)
        {
            var key = WolfModelDefinition.VariantKeys[i];
            if (key == "pale")
            {
                continue;
            }

            var file = wolfFiles[i];
            var wild = "entity/wolf/" + file + "_baby";
            var tame = "entity/wolf/" + file + "_tame_baby";
            var angry = "entity/wolf/" + file + "_angry_baby";
            var captured = wild;
            variants[(WolfModelDefinition.WolfEntityTypeId, key)] = new(
                "baby_wolf_" + key,
                32,
                32,
                Wolf.Parts,
                PlayerModelDefinition.DefaultFaceTransforms,
                captured,
                textureSelector: state => state.IsTame ? tame : state.IsAngry ? angry : captured,
                additionalTexturePaths: [tame, angry]);
        }

        variants[(FoxModelDefinition.FoxEntityTypeId, "snow")] = new(
            "baby_fox_snow",
            32,
            32,
            Fox.Parts,
            PlayerModelDefinition.DefaultFaceTransforms,
            "entity/fox/fox_snow_baby",
            textureSelector: state => state.IsSleeping ? "entity/fox/fox_snow_sleep_baby" : "entity/fox/fox_snow_baby",
            additionalTexturePaths: ["entity/fox/fox_snow_sleep_baby"]);

        var rabbitFiles = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["brown"] = "rabbit_brown",
            ["white"] = "rabbit_white",
            ["black"] = "rabbit_black",
            ["white_splotched"] = "rabbit_white_splotched",
            ["gold"] = "rabbit_gold",
            ["salt"] = "rabbit_salt",
            ["evil"] = "rabbit_caerbannog"
        };
        foreach (var key in RabbitModelDefinition.VariantKeys)
        {
            if (key != "brown")
            {
                variants[(RabbitModelDefinition.RabbitEntityTypeId, key)] = new(
                    "baby_rabbit_" + key,
                    32,
                    32,
                    Rabbit.Parts,
                    PlayerModelDefinition.DefaultFaceTransforms,
                    "entity/rabbit/" + rabbitFiles[key] + "_baby");
            }
        }

        return variants;
    }

    private static readonly string[] WolfVariantFiles =
        ["wolf", "wolf_spotted", "wolf_snowy", "wolf_black", "wolf_ashen", "wolf_rusty", "wolf_woods", "wolf_chestnut", "wolf_striped"];
}







