// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>Baby piglin/zombified piglin/hoglin/zoglin models. Parts are declared with the
/// <c>VanillaMeshDefinition</c> DSL and baked via <c>VanillaMeshBake</c> (T-694).</summary>
public static class BabyModelDefinitionsNether
{
    private static readonly VanillaMeshDefinition PiglinMesh = BuildPiglinMesh();

    private static readonly EntityModelPart[] PiglinParts = [.. VanillaMeshBake.Bake(PiglinMesh, 0, 0)];

    public static readonly EntityModelDefinition Piglin = new(
        "baby_piglin",
        64,
        64,
        PiglinParts,
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/piglin/piglin_baby");

    public static readonly EntityModelDefinition ZombifiedPiglin = new(
        "baby_zombified_piglin",
        64,
        64,
        PiglinParts,
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/piglin/zombified_piglin_baby");

    private static readonly VanillaMeshDefinition HoglinMesh = BuildHoglinMesh();

    private static readonly EntityModelPart[] HoglinParts = [.. VanillaMeshBake.Bake(HoglinMesh, 0, 0)];

    public static readonly EntityModelDefinition Hoglin = new(
        "baby_hoglin",
        64,
        64,
        HoglinParts,
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/hoglin/hoglin_baby");

    public static readonly EntityModelDefinition Zoglin = new(
        "baby_zoglin",
        64,
        64,
        HoglinParts,
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/hoglin/zoglin_baby");

    public static readonly IReadOnlyDictionary<int, EntityModelDefinition> Models =
        new Dictionary<int, EntityModelDefinition>
        {
            [102] = Piglin,
            [158] = ZombifiedPiglin,
            [65] = Hoglin,
            [153] = Zoglin
        };

    private static VanillaMeshDefinition BuildPiglinMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        root.AddOrReplaceChild("body", VanillaPartPose.Offset(0f, 18f, -0.5f),
            new VanillaCubeDefinition("body", 0, 13, -3f, -3f, -1f, 6f, 5f, 3f));
        var head = root.AddOrReplaceChild("head", VanillaPartPose.Offset(0f, 15f, 0f),
            new VanillaCubeDefinition("head", 0, 0, -4.5f, -6f, -3.5f, 9f, 6f, 7f));
        head.AddOrReplaceChild("head_snout", VanillaPartPose.Zero,
            new VanillaCubeDefinition("head_snout", 21, 30, -1.5f, -3f, -4.5f, 3f, 3f, 1f));
        head.AddOrReplaceChild("hat", VanillaPartPose.Zero,
            new VanillaCubeDefinition("hat", 0, 0, 0f, 0f, 0f, 0f, 0f, 0f));
        var leftEar = head.AddOrReplaceChild("left_ear", VanillaPartPose.Offset(4.2f, -4f, 0f),
            new VanillaCubeDefinition("left_ear", 0, 0, 0f, 0f, 0f, 0f, 0f, 0f));
        leftEar.AddOrReplaceChild("left_ear_r1", VanillaPartPose.OffsetAndRotation(1f, 1.75f, 0f, 0f, 0f, -0.6109f),
            new VanillaCubeDefinition("left_ear_r1", 0, 21, -0.5f, -3f, -2f, 1f, 6f, 4f));
        var rightEar = head.AddOrReplaceChild("right_ear", VanillaPartPose.Offset(-4.2f, -4f, 0f),
            new VanillaCubeDefinition("right_ear", 0, 0, 0f, 0f, 0f, 0f, 0f, 0f));
        rightEar.AddOrReplaceChild("right_ear_r1", VanillaPartPose.OffsetAndRotation(-1f, 1.75f, 0f, 0f, 0f, 0.6109f),
            new VanillaCubeDefinition("right_ear_r1", 18, 13, -0.5f, -3f, -2f, 1f, 6f, 4f));
        root.AddOrReplaceChild("left_arm", VanillaPartPose.Offset(4f, 15f, 0f),
            new VanillaCubeDefinition("left_arm", 28, 13, -1f, 0f, -1.5f, 2f, 5f, 3f));
        root.AddOrReplaceChild("right_arm", VanillaPartPose.Offset(-4f, 15f, 0f),
            new VanillaCubeDefinition("right_arm", 10, 30, -1f, 0f, -1.5f, 2f, 5f, 3f));
        root.AddOrReplaceChild("right_leg", VanillaPartPose.Offset(-1.5f, 20f, 0f),
            new VanillaCubeDefinition("right_leg", 22, 23, -1.5f, 0f, -1.5f, 3f, 4f, 3f));
        root.AddOrReplaceChild("left_leg", VanillaPartPose.Offset(1.5f, 20f, 0f),
            new VanillaCubeDefinition("left_leg", 10, 23, -1.5f, 0f, -1.5f, 3f, 4f, 3f));
        return new VanillaMeshDefinition(root);
    }

    private static VanillaMeshDefinition BuildHoglinMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        var head = root.AddOrReplaceChild("head", VanillaPartPose.OffsetAndRotation(0f, 13f, -7f, -0.8727f, 0f, 0f),
            new VanillaCubeDefinition("head", 0, 0, -5f, -2.2605f, -10.547f, 10f, 4f, 12f));
        head.AddOrReplaceChild("head_right", VanillaPartPose.Zero,
            new VanillaCubeDefinition("head_right", 44, 29, -7f, -4.0981f, -8.4879f, 2f, 5f, 2f));
        head.AddOrReplaceChild("head_left", VanillaPartPose.Zero,
            new VanillaCubeDefinition("head_left", 52, 29, 5f, -4.0981f, -8.4879f, 2f, 5f, 2f));
        head.AddOrReplaceChild("right_ear", VanillaPartPose.OffsetAndRotation(-5f, -1f, -1.5f, 0f, 0f, -0.8727f),
            new VanillaCubeDefinition("right_ear", 32, 5, -5.1f, -0.5f, -2f, 6f, 1f, 4f));
        head.AddOrReplaceChild("left_ear", VanillaPartPose.OffsetAndRotation(5f, -1f, -1.5f, 0f, 0f, 0.8727f),
            new VanillaCubeDefinition("left_ear", 32, 0, -0.9f, -0.5f, -2f, 6f, 1f, 4f, Mirror: true));
        var body = root.AddOrReplaceChild("body", VanillaPartPose.Offset(0f, 24f, 0f),
            new VanillaCubeDefinition("body", 0, 16, -4f, -14f, -7f, 8f, 8f, 14f, Grow: 0.02f));
        body.AddOrReplaceChild("body_mane", VanillaPartPose.Zero,
            new VanillaCubeDefinition("body_mane", 24, 39, 0f, -18f, -8f, 0f, 6f, 11f, Grow: 0.02f));
        root.AddOrReplaceChild("right_hind_leg", VanillaPartPose.Offset(-2.5f, 18f, 4.5f),
            new VanillaCubeDefinition("right_hind_leg", 0, 47, -1.5f, 0f, -1.5f, 3f, 6f, 3f));
        root.AddOrReplaceChild("left_hind_leg", VanillaPartPose.Offset(2.5f, 18f, 4.5f),
            new VanillaCubeDefinition("left_hind_leg", 12, 47, -1.5f, 0f, -1.5f, 3f, 6f, 3f));
        root.AddOrReplaceChild("right_front_leg", VanillaPartPose.Offset(-2.5f, 18f, -4.5f),
            new VanillaCubeDefinition("right_front_leg", 0, 38, -1.5f, 0f, -1.5f, 3f, 6f, 3f));
        root.AddOrReplaceChild("left_front_leg", VanillaPartPose.Offset(2.5f, 18f, -4.5f),
            new VanillaCubeDefinition("left_front_leg", 12, 38, -1.5f, 0f, -1.5f, 3f, 6f, 3f));
        return new VanillaMeshDefinition(root);
    }
}
