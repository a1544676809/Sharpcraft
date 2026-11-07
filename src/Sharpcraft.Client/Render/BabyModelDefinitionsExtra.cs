// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>Baby cat/wolf/fox/rabbit models. Parts are declared with the <c>VanillaMeshDefinition</c> DSL and baked
/// via <c>VanillaMeshBake</c> (T-699).</summary>
public static class BabyModelDefinitionsExtra
{
    private static readonly VanillaMeshDefinition CatMesh = BuildCatMesh();
    private static readonly VanillaMeshDefinition WolfMesh = BuildWolfMesh();
    private static readonly VanillaMeshDefinition FoxMesh = BuildFoxMesh();
    private static readonly VanillaMeshDefinition RabbitMesh = BuildRabbitMesh();

    public static readonly EntityModelDefinition Cat = new(
        "baby_cat", 32, 32,
        EntityModelLayerHelpers.WithCollar([.. VanillaMeshBake.Bake(CatMesh, 0, 0)], "entity/cat/cat_collar_baby"),
        PlayerModelDefinition.DefaultFaceTransforms, "entity/cat/cat_tabby_baby");

    public static readonly EntityModelDefinition Wolf = new(
        "baby_wolf", 32, 32,
        EntityModelLayerHelpers.WithCollar([.. VanillaMeshBake.Bake(WolfMesh, 0, 0)], "entity/wolf/wolf_collar_baby"),
        PlayerModelDefinition.DefaultFaceTransforms, "entity/wolf/wolf_baby",
        textureSelector: state => state.IsTame ? "entity/wolf/wolf_tame_baby" : state.IsAngry ? "entity/wolf/wolf_angry_baby" : "entity/wolf/wolf_baby",
        additionalTexturePaths: ["entity/wolf/wolf_tame_baby", "entity/wolf/wolf_angry_baby"]);

    public static readonly EntityModelDefinition Fox = new(
        "baby_fox", 32, 32, [.. VanillaMeshBake.Bake(FoxMesh, 0, 0)],
        PlayerModelDefinition.DefaultFaceTransforms, "entity/fox/fox_baby",
        textureSelector: state => state.IsSleeping ? "entity/fox/fox_sleep_baby" : "entity/fox/fox_baby",
        additionalTexturePaths: ["entity/fox/fox_sleep_baby"]);

    public static readonly EntityModelDefinition Rabbit = new(
        "baby_rabbit", 32, 32, [.. VanillaMeshBake.Bake(RabbitMesh, 0, 0)],
        PlayerModelDefinition.DefaultFaceTransforms, "entity/rabbit/rabbit_brown_baby");

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

    private static VanillaMeshDefinition BuildCatMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        var head = root.AddOrReplaceChild("head", VanillaPartPose.Offset(0f, 20f, -3.125f),
            new VanillaCubeDefinition("head", 0, 0, -2.5f, -3f, -2.875f, 5f, 4f, 4f));
        head.AddOrReplaceChild("right_ear", VanillaPartPose.Zero,
            new VanillaCubeDefinition("right_ear", 18, 0, -2f, -4f, -0.875f, 1f, 1f, 2f));
        head.AddOrReplaceChild("left_ear", VanillaPartPose.Zero,
            new VanillaCubeDefinition("left_ear", 24, 0, 1f, -4f, -0.875f, 1f, 1f, 2f));
        head.AddOrReplaceChild("nose", VanillaPartPose.Zero,
            new VanillaCubeDefinition("nose", 18, 3, -1.5f, -1f, -3.875f, 3f, 2f, 1f));
        root.AddOrReplaceChild("left_front_leg", VanillaPartPose.Offset(1f, 22f, -1.5f),
            new VanillaCubeDefinition("left_front_leg", 18, 18, -0.5f, 0f, -1f, 1f, 2f, 2f));
        root.AddOrReplaceChild("right_front_leg", VanillaPartPose.Offset(-1f, 22f, -1.5f),
            new VanillaCubeDefinition("right_front_leg", 12, 18, -0.5f, 0f, -1f, 1f, 2f, 2f));
        root.AddOrReplaceChild("left_hind_leg", VanillaPartPose.Offset(1f, 22f, 2.5f),
            new VanillaCubeDefinition("left_hind_leg", 18, 22, -0.5f, 0f, -1f, 1f, 2f, 2f));
        root.AddOrReplaceChild("right_hind_leg", VanillaPartPose.Offset(-1f, 22f, 2.5f),
            new VanillaCubeDefinition("right_hind_leg", 12, 22, -0.5f, 0f, -1f, 1f, 2f, 2f));
        root.AddOrReplaceChild("body", VanillaPartPose.Offset(0f, 20.5f, 0.5f),
            new VanillaCubeDefinition("body", 0, 8, -2f, -1.5f, -3.5f, 4f, 3f, 7f));
        root.AddOrReplaceChild("tail1", VanillaPartPose.OffsetAndRotation(0f, 19.107f, 3.9151f, 0.567232f, 0f, 0f),
            new VanillaCubeDefinition("tail1", 0, 18, -0.5f, -0.107f, 0.0849f, 1f, 1f, 5f));
        root.AddOrReplaceChild("tail2", VanillaPartPose.Zero,
            new VanillaCubeDefinition("tail2", 0, 0, 0f, 0f, 0f, 0f, 0f, 0f));
        return new VanillaMeshDefinition(root);
    }

    private static VanillaMeshDefinition BuildWolfMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        var head = root.AddOrReplaceChild("head", VanillaPartPose.Offset(0f, 18.25f, -4f),
            new VanillaCubeDefinition("head", 0, 12, -2.99f, -3.25f, -3f, 6f, 5f, 5f, Grow: 0.025f));
        head.AddOrReplaceChild("snout", VanillaPartPose.Zero,
            new VanillaCubeDefinition("snout", 17, 12, -1.5f, -0.24f, -5f, 3f, 2f, 2f));
        head.AddOrReplaceChild("right_ear", VanillaPartPose.Offset(-2f, -4.25f, -0.5f),
            new VanillaCubeDefinition("right_ear", 0, 5, -1f, -1f, -0.5f, 2f, 2f, 1f));
        head.AddOrReplaceChild("left_ear", VanillaPartPose.Offset(2f, -4.25f, -0.5f),
            new VanillaCubeDefinition("left_ear", 20, 5, -1f, -1f, -0.5f, 2f, 2f, 1f));
        root.AddOrReplaceChild("body", VanillaPartPose.Offset(0f, 19f, 0f),
            new VanillaCubeDefinition("body", 0, 0, -3f, -2f, -4f, 6f, 4f, 8f));
        root.AddOrReplaceChild("right_hind_leg", VanillaPartPose.Offset(-1.5f, 21f, 3f),
            new VanillaCubeDefinition("right_hind_leg", 0, 22, -1f, 0f, -1f, 2f, 3f, 2f));
        root.AddOrReplaceChild("left_hind_leg", VanillaPartPose.Offset(1.5f, 21f, 3f),
            new VanillaCubeDefinition("left_hind_leg", 8, 22, -1f, 0f, -1f, 2f, 3f, 2f));
        root.AddOrReplaceChild("right_front_leg", VanillaPartPose.Offset(-1.5f, 21f, -3f),
            new VanillaCubeDefinition("right_front_leg", 0, 0, -1f, 0f, -1f, 2f, 3f, 2f));
        root.AddOrReplaceChild("left_front_leg", VanillaPartPose.Offset(1.5f, 21f, -3f),
            new VanillaCubeDefinition("left_front_leg", 20, 0, -1f, 0f, -1f, 2f, 3f, 2f));
        var tail = root.AddOrReplaceChild("tail", VanillaPartPose.OffsetAndRotation(0f, 19f, 3f, 0.5236f, 0f, 0f),
            new VanillaCubeDefinition("tail", 0, 0, 0f, 0f, 0f, 0f, 0f, 0f));
        tail.AddOrReplaceChild("tail_r1", VanillaPartPose.OffsetAndRotation(0f, -0.6f, 0.2f, -3.1f, 0f, 0f),
            new VanillaCubeDefinition("tail_r1", 22, 16, -1f, -5.7f, -1f, 2f, 6f, 2f));
        return new VanillaMeshDefinition(root);
    }

    private static VanillaMeshDefinition BuildFoxMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        var head = root.AddOrReplaceChild("head", VanillaPartPose.Offset(0f, 18.125f, 0.125f),
            new VanillaCubeDefinition("head", 0, 0, -3f, -2.125f, -5.125f, 6f, 5f, 5f));
        head.AddOrReplaceChild("snout", VanillaPartPose.Zero,
            new VanillaCubeDefinition("snout", 18, 20, -1f, 0.875f, -7.125f, 2f, 2f, 2f));
        head.AddOrReplaceChild("right_ear", VanillaPartPose.Zero,
            new VanillaCubeDefinition("right_ear", 22, 8, -3f, -4.125f, -4.125f, 2f, 2f, 1f));
        head.AddOrReplaceChild("left_ear", VanillaPartPose.Zero,
            new VanillaCubeDefinition("left_ear", 22, 11, 1f, -4.125f, -4.125f, 2f, 2f, 1f));
        root.AddOrReplaceChild("right_hind_leg", VanillaPartPose.Offset(-1.5f, 22f, 4f),
            new VanillaCubeDefinition("right_hind_leg", 22, 4, -1f, 0f, -1f, 2f, 2f, 2f));
        root.AddOrReplaceChild("left_hind_leg", VanillaPartPose.Offset(1.5f, 22f, 4f),
            new VanillaCubeDefinition("left_hind_leg", 22, 0, -1f, 0f, -1f, 2f, 2f, 2f));
        root.AddOrReplaceChild("right_front_leg", VanillaPartPose.Offset(-1.5f, 22f, 0f),
            new VanillaCubeDefinition("right_front_leg", 22, 4, -1f, 0f, -1f, 2f, 2f, 2f));
        root.AddOrReplaceChild("left_front_leg", VanillaPartPose.Offset(1.5f, 22f, 0f),
            new VanillaCubeDefinition("left_front_leg", 22, 0, -1f, 0f, -1f, 2f, 2f, 2f));
        var body = root.AddOrReplaceChild("body", VanillaPartPose.Offset(0f, 20f, 2f),
            new VanillaCubeDefinition("body", 0, 10, -2.5f, -2f, -3f, 5f, 4f, 6f));
        body.AddOrReplaceChild("tail", VanillaPartPose.Offset(0f, -0.5f, 3f),
            new VanillaCubeDefinition("tail", 0, 20, -1.5f, -1.48f, -1f, 3f, 3f, 6f));
        return new VanillaMeshDefinition(root);
    }

    private static VanillaMeshDefinition BuildRabbitMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        var body = root.AddOrReplaceChild("body", VanillaPartPose.Offset(0f, 23f, 1.6f),
            new VanillaCubeDefinition("body", 0, 0, 0f, 0f, 0f, 0f, 0f, 0f));
        body.AddOrReplaceChild("body_r1", VanillaPartPose.OffsetAndRotation(0f, -2f, -1.6f, -0.5236f, 0f, 0f),
            new VanillaCubeDefinition("body_r1", 0, 8, -2f, -2f, -3f, 4f, 3f, 6f));
        var tail = body.AddOrReplaceChild("tail", VanillaPartPose.Offset(0f, -2.2f, 2f),
            new VanillaCubeDefinition("tail", 0, 0, 0f, 0f, 0f, 0f, 0f, 0f));
        tail.AddOrReplaceChild("tail_r1", VanillaPartPose.OffsetAndRotation(-0.1f, 0f, 0f, -0.5236f, 0f, 0f),
            new VanillaCubeDefinition("tail_r1", 0, 21, -1.4f, -2.0268f, -1.0177f, 3f, 3f, 3f));
        var head = body.AddOrReplaceChild("head", VanillaPartPose.Offset(0f, -5f, -2.6f),
            new VanillaCubeDefinition("head", 0, 0, -2.5f, -3f, -3f, 5f, 4f, 4f));
        head.AddOrReplaceChild("right_ear", VanillaPartPose.Offset(-1.5f, -3.5f, -0.5f),
            new VanillaCubeDefinition("right_ear", 18, 0, -1f, -3.5f, -0.5f, 2f, 4f, 1f));
        head.AddOrReplaceChild("left_ear", VanillaPartPose.Offset(1.5f, -3.5f, -0.5f),
            new VanillaCubeDefinition("left_ear", 24, 0, -1f, -3.5f, -0.5f, 2f, 4f, 1f));
        var frontLegs = body.AddOrReplaceChild("frontlegs", VanillaPartPose.Offset(0f, -2.5f, -2.6f),
            new VanillaCubeDefinition("frontlegs", 0, 0, 0f, 0f, 0f, 0f, 0f, 0f));
        var leftFront = frontLegs.AddOrReplaceChild("left_front_leg", VanillaPartPose.OffsetAndRotation(1f, 1f, -0.5f, 0.3927f, 0f, 0f),
            new VanillaCubeDefinition("left_front_leg", 0, 0, 0f, 0f, 0f, 0f, 0f, 0f));
        leftFront.AddOrReplaceChild("left_front_leg_r1", VanillaPartPose.OffsetAndRotation(0f, 1f, 0f, -0.3927f, 0f, 0f),
            new VanillaCubeDefinition("left_front_leg_r1", 18, 8, -0.5f, -1.5f, -0.5f, 1f, 3f, 1f));
        var rightFront = frontLegs.AddOrReplaceChild("right_front_leg", VanillaPartPose.OffsetAndRotation(-1f, 1f, -0.5f, 0.3927f, 0f, 0f),
            new VanillaCubeDefinition("right_front_leg", 0, 0, 0f, 0f, 0f, 0f, 0f, 0f));
        rightFront.AddOrReplaceChild("right_front_leg_r1", VanillaPartPose.OffsetAndRotation(0f, 1f, 0f, -0.3927f, 0f, 0f),
            new VanillaCubeDefinition("right_front_leg_r1", 14, 8, -0.5f, -1.5f, -0.5f, 1f, 3f, 1f));
        var backLegs = root.AddOrReplaceChild("backlegs", VanillaPartPose.Offset(0f, 23f, 2f),
            new VanillaCubeDefinition("backlegs", 0, 0, 0f, 0f, 0f, 0f, 0f, 0f));
        var leftHind = backLegs.AddOrReplaceChild("left_hind_leg", VanillaPartPose.OffsetAndRotation(1.5f, 0.5f, 0.5f, 0f, 3.1416f, 0f),
            new VanillaCubeDefinition("left_hind_leg", 0, 0, 0f, 0f, 0f, 0f, 0f, 0f));
        leftHind.AddOrReplaceChild("left_haunch", VanillaPartPose.OffsetAndRotation(1f, 0f, 0.5f, 0f, -0.7854f, 0f),
            new VanillaCubeDefinition("left_haunch", 10, 17, -2f, -0.5f, 0f, 2f, 1f, 3f));
        var rightHind = backLegs.AddOrReplaceChild("right_hind_leg", VanillaPartPose.OffsetAndRotation(-1.5f, 0.5f, 0.5f, 0f, 3.1416f, 0f),
            new VanillaCubeDefinition("right_hind_leg", 0, 0, 0f, 0f, 0f, 0f, 0f, 0f));
        rightHind.AddOrReplaceChild("right_haunch", VanillaPartPose.OffsetAndRotation(0.5f, 0f, -0.9f, 0f, 0.7854f, 0f),
            new VanillaCubeDefinition("right_haunch", 0, 17, -2f, -0.5f, 0f, 2f, 1f, 3f));
        return new VanillaMeshDefinition(root);
    }
}
