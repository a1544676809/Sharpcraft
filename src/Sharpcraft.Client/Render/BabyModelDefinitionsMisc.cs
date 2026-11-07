// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>Baby goat/panda/polar bear/turtle models (default textures). Parts are declared with the
/// <c>VanillaMeshDefinition</c> DSL and baked via <c>VanillaMeshBake</c> (T-695).</summary>
public static class BabyModelDefinitionsMisc
{
    private static readonly VanillaMeshDefinition GoatMesh = BuildGoatMesh();
    private static readonly EntityModelPart[] GoatParts = [.. VanillaMeshBake.Bake(GoatMesh, 0, 0)];
    private static readonly VanillaMeshDefinition PandaMesh = BuildPandaMesh();
    private static readonly EntityModelPart[] PandaParts = [.. VanillaMeshBake.Bake(PandaMesh, 0, 0)];
    private static readonly VanillaMeshDefinition PolarBearMesh = BuildPolarBearMesh();
    private static readonly EntityModelPart[] PolarBearParts = [.. VanillaMeshBake.Bake(PolarBearMesh, 0, 0)];
    private static readonly VanillaMeshDefinition TurtleMesh = BuildTurtleMesh();
    private static readonly EntityModelPart[] TurtleParts = [.. VanillaMeshBake.Bake(TurtleMesh, 0, 0)];

    public static readonly EntityModelDefinition Goat = new(
        "baby_goat", 64, 64, GoatParts, PlayerModelDefinition.DefaultFaceTransforms, "entity/goat/goat_baby");

    public static readonly EntityModelDefinition Panda = new(
        "baby_panda", 64, 64, PandaParts, PlayerModelDefinition.DefaultFaceTransforms, "entity/panda/panda_baby");

    public static readonly EntityModelDefinition PolarBear = new(
        "baby_polar_bear", 64, 64, PolarBearParts, PlayerModelDefinition.DefaultFaceTransforms, "entity/bear/polarbear_baby");

    public static readonly EntityModelDefinition Turtle = new(
        "baby_turtle", 16, 16, TurtleParts, PlayerModelDefinition.DefaultFaceTransforms, "entity/turtle/turtle_baby");

    public static readonly IReadOnlyDictionary<int, EntityModelDefinition> Models =
        new Dictionary<int, EntityModelDefinition>
        {
            [63] = Goat,
            [97] = Panda,
            [105] = PolarBear,
            [141] = Turtle
        };

    public static readonly IReadOnlyDictionary<(int TypeId, string Key), EntityModelDefinition> Variants = BuildVariants();

    private static Dictionary<(int, string), EntityModelDefinition> BuildVariants()
    {
        var variants = new Dictionary<(int, string), EntityModelDefinition>
        {
            [(97, "normal")] = Panda
        };
        foreach (var key in PandaModelDefinition.VariantKeys)
        {
            if (key != "normal")
            {
                variants[(97, key)] = new EntityModelDefinition(
                    "baby_panda_" + key,
                    64,
                    64,
                    PandaParts,
                    PlayerModelDefinition.DefaultFaceTransforms,
                    "entity/panda/panda_" + key + "_baby");
            }
        }

        return variants;
    }

    private static VanillaMeshDefinition BuildGoatMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        root.AddOrReplaceChild("left_hind_leg", VanillaPartPose.Offset(1.5f, 19.5f, 3f),
            new VanillaCubeDefinition("left_hind_leg", 29, 12, -1f, -0.5f, -1f, 2f, 5f, 2f));
        root.AddOrReplaceChild("right_hind_leg", VanillaPartPose.Offset(-1.5f, 19.5f, 3f),
            new VanillaCubeDefinition("right_hind_leg", 21, 12, -1f, -0.5f, -1f, 2f, 5f, 2f));
        root.AddOrReplaceChild("right_front_leg", VanillaPartPose.Offset(-1.5f, 19.5f, -2f),
            new VanillaCubeDefinition("right_front_leg", 21, 5, -1f, -0.5f, -1f, 2f, 5f, 2f));
        root.AddOrReplaceChild("left_front_leg", VanillaPartPose.Offset(1.5f, 19.5f, -2f),
            new VanillaCubeDefinition("left_front_leg", 29, 5, -1f, -0.5f, -1f, 2f, 5f, 2f));
        var body = root.AddOrReplaceChild("body", VanillaPartPose.Offset(0f, 17.8f, 0f),
            new VanillaCubeDefinition("body", 0, 10, -3f, -2.3f, -4.5f, 6f, 5f, 9f));
        body.AddOrReplaceChild("body_lower", VanillaPartPose.Zero,
            new VanillaCubeDefinition("body_lower", 0, 24, -2.5f, -2.2f, -4f, 5f, 4f, 8f));
        var head = root.AddOrReplaceChild("head", VanillaPartPose.OffsetAndRotation(0f, 15.5f, -3f, -0.4363f, 0f, 0f),
            new VanillaCubeDefinition("head", 0, 0, -2f, -3.8126f, -5.1548f, 4f, 4f, 6f));
        head.AddOrReplaceChild("right_horn", VanillaPartPose.OffsetAndRotation(-1.5f, -1.5f, -1f, -0.3927f, 0f, 0f),
            new VanillaCubeDefinition("right_horn", 24, 0, 0f, -4.5f, 0f, 1f, 2f, 1f));
        head.AddOrReplaceChild("left_horn", VanillaPartPose.OffsetAndRotation(-1.5f, -1.5f, -1f, -0.3927f, 0f, 0f),
            new VanillaCubeDefinition("left_horn", 24, 0, 2f, -4.5f, 0f, 1f, 2f, 1f));
        head.AddOrReplaceChild("right_ear", VanillaPartPose.OffsetAndRotation(-1.7f, -2.3126f, 0.1452f, 0f, -0.5236f, 0f),
            new VanillaCubeDefinition("right_ear", 0, 12, -2f, -0.5f, -0.5f, 2f, 1f, 1f));
        head.AddOrReplaceChild("left_ear", VanillaPartPose.OffsetAndRotation(1.7f, -2.3126f, 0.1452f, 0f, 0.5236f, 0f),
            new VanillaCubeDefinition("left_ear", 0, 12, 0f, -0.5f, -0.5f, 2f, 1f, 1f));
        head.AddOrReplaceChild("head_main", VanillaPartPose.Offset(0f, -1.3126f, -1.1548f),
            new VanillaCubeDefinition("head_main", 0, 0, -2f, -2.5f, -4f, 4f, 4f, 6f));
        return new VanillaMeshDefinition(root);
    }

    private static VanillaMeshDefinition BuildPandaMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        root.AddOrReplaceChild("body", VanillaPartPose.Offset(0f, 18.5f, 2.5f),
            new VanillaCubeDefinition("body", 0, 11, -4.5f, -3.5f, -5.5f, 9f, 7f, 11f));
        var head = root.AddOrReplaceChild("head", VanillaPartPose.Offset(0f, 19f, -3f),
            new VanillaCubeDefinition("head", 0, 0, -3.5f, -3f, -5f, 7f, 6f, 5f));
        head.AddOrReplaceChild("mouth", VanillaPartPose.Zero,
            new VanillaCubeDefinition("mouth", 24, 6, -2f, 1f, -6f, 4f, 2f, 1f));
        head.AddOrReplaceChild("right_ear", VanillaPartPose.Zero,
            new VanillaCubeDefinition("right_ear", 24, 0, -4.5f, -4f, -3.5f, 3f, 3f, 1f));
        head.AddOrReplaceChild("left_ear", VanillaPartPose.Zero,
            new VanillaCubeDefinition("left_ear", 33, 0, 1.5f, -4f, -3.5f, 3f, 3f, 1f));
        root.AddOrReplaceChild("right_hind_leg", VanillaPartPose.Offset(-3f, 22f, 6.5f),
            new VanillaCubeDefinition("right_hind_leg", 0, 34, -1.5f, 0f, -1.5f, 3f, 2f, 3f));
        root.AddOrReplaceChild("left_hind_leg", VanillaPartPose.Offset(3f, 22f, 6.5f),
            new VanillaCubeDefinition("left_hind_leg", 12, 34, -1.5f, 0f, -1.5f, 3f, 2f, 3f));
        root.AddOrReplaceChild("right_front_leg", VanillaPartPose.Offset(-3f, 22f, -1.5f),
            new VanillaCubeDefinition("right_front_leg", 0, 29, -1.5f, 0f, -1.5f, 3f, 2f, 3f));
        root.AddOrReplaceChild("left_front_leg", VanillaPartPose.Offset(3f, 22f, -1.5f),
            new VanillaCubeDefinition("left_front_leg", 12, 29, -1.5f, 0f, -1.5f, 3f, 2f, 3f));
        return new VanillaMeshDefinition(root);
    }

    private static VanillaMeshDefinition BuildPolarBearMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        root.AddOrReplaceChild("body", VanillaPartPose.Offset(0f, 17.5f, 0f),
            new VanillaCubeDefinition("body", 0, 9, -4f, -3.5f, -6f, 8f, 7f, 12f));
        var head = root.AddOrReplaceChild("head", VanillaPartPose.Offset(0f, 18.625f, -5.75f),
            new VanillaCubeDefinition("head", 0, 0, -3f, -2.625f, -4.25f, 6f, 5f, 4f));
        head.AddOrReplaceChild("snout", VanillaPartPose.Zero,
            new VanillaCubeDefinition("snout", 20, 3, -2f, 0.375f, -6.25f, 4f, 2f, 2f));
        head.AddOrReplaceChild("right_ear", VanillaPartPose.Zero,
            new VanillaCubeDefinition("right_ear", 20, 0, -4f, -3.625f, -2.75f, 2f, 2f, 1f));
        head.AddOrReplaceChild("left_ear", VanillaPartPose.Zero,
            new VanillaCubeDefinition("left_ear", 26, 0, 2f, -3.625f, -2.75f, 2f, 2f, 1f));
        root.AddOrReplaceChild("right_hind_leg", VanillaPartPose.Offset(-2.5f, 21.5f, 4.5f),
            new VanillaCubeDefinition("right_hind_leg", 0, 34, -1.5f, -0.5f, -1.5f, 3f, 3f, 3f));
        root.AddOrReplaceChild("left_hind_leg", VanillaPartPose.Offset(2.5f, 21.5f, 4.5f),
            new VanillaCubeDefinition("left_hind_leg", 12, 34, -1.5f, -0.5f, -1.5f, 3f, 3f, 3f));
        root.AddOrReplaceChild("right_front_leg", VanillaPartPose.Offset(-2.5f, 21.5f, -4.5f),
            new VanillaCubeDefinition("right_front_leg", 0, 28, -1.5f, -0.5f, -1.5f, 3f, 3f, 3f));
        root.AddOrReplaceChild("left_front_leg", VanillaPartPose.Offset(2.5f, 21.5f, -4.5f),
            new VanillaCubeDefinition("left_front_leg", 12, 28, -1.5f, -0.5f, -1.5f, 3f, 3f, 3f));
        return new VanillaMeshDefinition(root);
    }

    private static VanillaMeshDefinition BuildTurtleMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        root.AddOrReplaceChild("body", VanillaPartPose.Offset(0f, 22.9f, 1f),
            new VanillaCubeDefinition("body", 0, 0, -2f, -1f, -2f, 4f, 2f, 4f));
        root.AddOrReplaceChild("head", VanillaPartPose.Offset(0f, 22.9f, -1f),
            new VanillaCubeDefinition("head", 0, 6, -1.5f, -2f, -3f, 3f, 3f, 3f));
        root.AddOrReplaceChild("right_hind_leg", VanillaPartPose.Offset(-2f, 23.9f, 2.5f),
            new VanillaCubeDefinition("right_hind_leg", -1, 0, -2f, 0f, -0.5f, 2f, 0f, 1f));
        root.AddOrReplaceChild("left_hind_leg", VanillaPartPose.Offset(2f, 23.9f, 2.5f),
            new VanillaCubeDefinition("left_hind_leg", -1, 1, 0f, 0f, -0.5f, 2f, 0f, 1f));
        root.AddOrReplaceChild("right_front_leg", VanillaPartPose.Offset(-2f, 23.9f, -0.5f),
            new VanillaCubeDefinition("right_front_leg", 8, 6, -2f, 0f, -0.5f, 2f, 0f, 1f));
        root.AddOrReplaceChild("left_front_leg", VanillaPartPose.Offset(2f, 23.9f, -0.5f),
            new VanillaCubeDefinition("left_front_leg", 8, 7, 0f, 0f, -0.5f, 2f, 0f, 1f));
        return new VanillaMeshDefinition(root);
    }
}
