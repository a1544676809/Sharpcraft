// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// Baby models whose geometry differs from the adult model (pig, cow, chicken). Each variant keeps the same geometry and
/// swaps to the matching <c>*_baby</c> texture. Parts are declared with the <c>VanillaMeshDefinition</c> DSL and baked
/// via <c>VanillaMeshBake</c> (T-701).
/// </summary>
public static class BabyModelDefinitions
{
    private static readonly VanillaMeshDefinition PigMesh = BuildPigMesh();
    private static readonly VanillaMeshDefinition CowMesh = BuildCowMesh();
    private static readonly VanillaMeshDefinition ChickenMesh = BuildChickenMesh();

    private static readonly CompiledEntityModel PigCompiled = VanillaMeshBakery.Build(PigMesh, 32, 32);
    private static readonly CompiledEntityModel CowCompiled = VanillaMeshBakery.Build(CowMesh, 64, 64);
    private static readonly CompiledEntityModel ChickenCompiled = VanillaMeshBakery.Build(ChickenMesh, 16, 16);

    public static readonly EntityModelDefinition Pig = new(
        "baby_pig", 32, 32, [.. VanillaMeshBake.Bake(PigMesh, 0, 0)],
        PlayerModelDefinition.DefaultFaceTransforms, "entity/pig/pig_temperate_baby",
        compiled: PigCompiled);

    public static readonly EntityModelDefinition Cow = new(
        "baby_cow", 64, 64, [.. VanillaMeshBake.Bake(CowMesh, 0, 0)],
        PlayerModelDefinition.DefaultFaceTransforms, "entity/cow/cow_temperate_baby",
        compiled: CowCompiled);

    public static readonly EntityModelDefinition Chicken = new(
        "baby_chicken", 16, 16, [.. VanillaMeshBake.Bake(ChickenMesh, 0, 0)],
        PlayerModelDefinition.DefaultFaceTransforms, "entity/chicken/chicken_temperate_baby",
        compiled: ChickenCompiled);

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

        variants[(PigModelDefinition.PigEntityTypeId, "warm")] = new("baby_pig_warm", 32, 32, Pig.Parts, PlayerModelDefinition.DefaultFaceTransforms, "entity/pig/pig_warm_baby", compiled: PigCompiled);
        variants[(PigModelDefinition.PigEntityTypeId, "cold")] = new("baby_pig_cold", 32, 32, Pig.Parts, PlayerModelDefinition.DefaultFaceTransforms, "entity/pig/pig_cold_baby", compiled: PigCompiled);
        variants[(CowModelDefinition.CowEntityTypeId, "warm")] = new("baby_cow_warm", 64, 64, Cow.Parts, PlayerModelDefinition.DefaultFaceTransforms, "entity/cow/cow_warm_baby", compiled: CowCompiled);
        variants[(CowModelDefinition.CowEntityTypeId, "cold")] = new("baby_cow_cold", 64, 64, Cow.Parts, PlayerModelDefinition.DefaultFaceTransforms, "entity/cow/cow_cold_baby", compiled: CowCompiled);
        variants[(ChickenModelDefinition.ChickenEntityTypeId, "warm")] = new("baby_chicken_warm", 16, 16, Chicken.Parts, PlayerModelDefinition.DefaultFaceTransforms, "entity/chicken/chicken_warm_baby", compiled: ChickenCompiled);
        variants[(ChickenModelDefinition.ChickenEntityTypeId, "cold")] = new("baby_chicken_cold", 16, 16, Chicken.Parts, PlayerModelDefinition.DefaultFaceTransforms, "entity/chicken/chicken_cold_baby", compiled: ChickenCompiled);
        return variants;
    }

    private static VanillaMeshDefinition BuildPigMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        root.AddOrReplaceChild("body", VanillaPartPose.Offset(0f, 19f, 0.5f),
            new VanillaCubeDefinition("body", 0, 0, -3.5f, -3f, -4.5f, 7f, 6f, 9f));
        var head = root.AddOrReplaceChild("head", VanillaPartPose.Offset(0f, 19f, -2f),
            new VanillaCubeDefinition("head", 0, 15, -3.5f, -5f, -5f, 7f, 6f, 6f, Grow: 0.025f));
        head.AddOrReplaceChild("snout", VanillaPartPose.Zero,
            new VanillaCubeDefinition("snout", 6, 27, -1.5f, -1.975f, -6f, 3f, 2f, 1f, Grow: 0.015f));
        root.AddOrReplaceChild("left_front_leg", VanillaPartPose.Offset(2.5f, 22f, -3f),
            new VanillaCubeDefinition("left_front_leg", 0, 0, -1f, 0f, -1f, 2f, 2f, 2f));
        root.AddOrReplaceChild("right_front_leg", VanillaPartPose.Offset(-2.5f, 22f, -3f),
            new VanillaCubeDefinition("right_front_leg", 23, 0, -1f, 0f, -1f, 2f, 2f, 2f));
        root.AddOrReplaceChild("left_hind_leg", VanillaPartPose.Offset(2.5f, 22f, 4f),
            new VanillaCubeDefinition("left_hind_leg", 0, 4, -1f, 0f, -1f, 2f, 2f, 2f));
        root.AddOrReplaceChild("right_hind_leg", VanillaPartPose.Offset(-2.5f, 22f, 4f),
            new VanillaCubeDefinition("right_hind_leg", 23, 4, -1f, 0f, -1f, 2f, 2f, 2f));
        return new VanillaMeshDefinition(root);
    }

    private static VanillaMeshDefinition BuildCowMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        var head = root.AddOrReplaceChild("head", VanillaPartPose.Offset(0f, 13.569f, -5.1667f),
            new VanillaCubeDefinition("head", 0, 18, -3f, -4.569f, -4.8333f, 6f, 6f, 5f));
        head.AddOrReplaceChild("ear_right", VanillaPartPose.Zero,
            new VanillaCubeDefinition("ear_right", 8, 29, 3f, -5.569f, -3.8333f, 1f, 2f, 1f));
        head.AddOrReplaceChild("ear_left", VanillaPartPose.Zero,
            new VanillaCubeDefinition("ear_left", 4, 29, -4f, -5.569f, -3.8333f, 1f, 2f, 1f, Mirror: true));
        head.AddOrReplaceChild("snout", VanillaPartPose.Zero,
            new VanillaCubeDefinition("snout", 12, 29, -2f, -1.569f, -5.8333f, 4f, 3f, 1f));
        root.AddOrReplaceChild("body", VanillaPartPose.Offset(3f, 19f, -5f),
            new VanillaCubeDefinition("body", 0, 0, -7f, -7f, -1f, 8f, 6f, 12f));
        root.AddOrReplaceChild("right_front_leg", VanillaPartPose.Offset(-2.5f, 18f, -3.5f),
            new VanillaCubeDefinition("right_front_leg", 22, 18, -1.5f, 0f, -1.5f, 3f, 6f, 3f));
        root.AddOrReplaceChild("left_front_leg", VanillaPartPose.Offset(2.5f, 18f, -3.5f),
            new VanillaCubeDefinition("left_front_leg", 34, 18, -1.5f, 0f, -1.5f, 3f, 6f, 3f));
        root.AddOrReplaceChild("right_hind_leg", VanillaPartPose.Offset(-2.5f, 18f, 3.5f),
            new VanillaCubeDefinition("right_hind_leg", 22, 27, -1.5f, 0f, -1.5f, 3f, 6f, 3f));
        root.AddOrReplaceChild("left_hind_leg", VanillaPartPose.Offset(2.5f, 18f, 3.5f),
            new VanillaCubeDefinition("left_hind_leg", 34, 27, -1.5f, 0f, -1.5f, 3f, 6f, 3f));
        return new VanillaMeshDefinition(root);
    }

    private static VanillaMeshDefinition BuildChickenMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        var body = root.AddOrReplaceChild("body", VanillaPartPose.Offset(0f, 20.25f, -1.25f),
            new VanillaCubeDefinition("body", 0, 0, -2f, -2.25f, -0.75f, 4f, 4f, 4f));
        body.AddOrReplaceChild("beak", VanillaPartPose.Zero,
            new VanillaCubeDefinition("beak", 10, 8, -1f, -0.25f, -1.75f, 2f, 1f, 1f));
        var leftLeg = root.AddOrReplaceChild("left_leg", VanillaPartPose.Offset(1f, 22f, 0.5f),
            new VanillaCubeDefinition("left_leg", 2, 2, -0.5f, 0f, 0f, 1f, 2f, 0f));
        leftLeg.AddOrReplaceChild("left_foot", VanillaPartPose.Zero,
            new VanillaCubeDefinition("left_foot", 0, 1, -0.5f, 2f, -1f, 1f, 0f, 1f));
        var rightLeg = root.AddOrReplaceChild("right_leg", VanillaPartPose.Offset(-1f, 22f, 0.5f),
            new VanillaCubeDefinition("right_leg", 0, 2, -0.5f, 0f, 0f, 1f, 2f, 0f));
        rightLeg.AddOrReplaceChild("right_foot", VanillaPartPose.Zero,
            new VanillaCubeDefinition("right_foot", 0, 0, -0.5f, 2f, -1f, 1f, 0f, 1f));
        root.AddOrReplaceChild("right_wing", VanillaPartPose.Offset(2f, 20f, 0f),
            new VanillaCubeDefinition("right_wing", 6, 8, 0f, 0f, -1f, 1f, 0f, 2f));
        root.AddOrReplaceChild("left_wing", VanillaPartPose.Offset(-2f, 20f, 0f),
            new VanillaCubeDefinition("left_wing", 4, 8, -1f, 0f, -1f, 1f, 0f, 2f));
        return new VanillaMeshDefinition(root);
    }
}
