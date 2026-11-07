// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>Baby horse/llama/camel/donkey models, with the equine and llama coat variants using *_baby textures.
/// Parts are declared with the <c>VanillaMeshDefinition</c> DSL and baked via <c>VanillaMeshBake</c> (T-697).</summary>
public static class BabyModelDefinitionsEquine
{
    private static readonly VanillaMeshDefinition HorseMesh = BuildHorseMesh();
    private static readonly EntityModelPart[] HorseParts = EntityModelLayerHelpers.WithHorseMarkings(
        [.. VanillaMeshBake.Bake(HorseMesh, 0, 0)], "entity/horse/horse_markings_", true);
    private static readonly VanillaMeshDefinition LlamaMesh = BuildLlamaMesh();
    private static readonly EntityModelPart[] LlamaParts = [.. VanillaMeshBake.Bake(LlamaMesh, 0, 0)];
    private static readonly VanillaMeshDefinition CamelMesh = BuildCamelMesh();
    private static readonly EntityModelPart[] CamelParts = [.. VanillaMeshBake.Bake(CamelMesh, 0, 0)];
    private static readonly VanillaMeshDefinition DonkeyMesh = BuildDonkeyMesh();
    private static readonly EntityModelPart[] DonkeyParts = [.. VanillaMeshBake.Bake(DonkeyMesh, 0, 0)];

    private static readonly CompiledEntityModel HorseCompiled = VanillaMeshBakery.Build(HorseMesh, 64, 64);
    private static readonly CompiledEntityModel HorseMarkedCompiled =
        EntityModelLayerHelpers.WithHorseMarkings(HorseCompiled, "entity/horse/horse_markings_", true);
    private static readonly CompiledEntityModel LlamaCompiled = VanillaMeshBakery.Build(LlamaMesh, 64, 64);
    private static readonly CompiledEntityModel CamelCompiled = VanillaMeshBakery.Build(CamelMesh, 64, 64);
    private static readonly CompiledEntityModel DonkeyCompiled = VanillaMeshBakery.Build(DonkeyMesh, 64, 64);

    public static readonly EntityModelDefinition Horse = new(
        "baby_horse", 64, 64, HorseParts, PlayerModelDefinition.DefaultFaceTransforms, "entity/horse/horse_brown_baby",
        compiled: HorseMarkedCompiled);

    public static readonly EntityModelDefinition Llama = new(
        "baby_llama", 64, 64, LlamaParts, PlayerModelDefinition.DefaultFaceTransforms, "entity/llama/llama_brown_baby",
        compiled: LlamaCompiled);

    public static readonly EntityModelDefinition Camel = new(
        "baby_camel", 64, 64, CamelParts, PlayerModelDefinition.DefaultFaceTransforms, "entity/camel/camel_baby",
        compiled: CamelCompiled);

    public static readonly EntityModelDefinition Donkey = new(
        "baby_donkey", 64, 64, DonkeyParts, PlayerModelDefinition.DefaultFaceTransforms, "entity/horse/donkey_baby",
        compiled: DonkeyCompiled);

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
                variants[(HorseModelDefinition.HorseEntityTypeId, key)] = new("baby_horse_" + key, 64, 64, HorseParts, PlayerModelDefinition.DefaultFaceTransforms, "entity/horse/" + horseFiles[key] + "_baby", compiled: HorseMarkedCompiled);
            }
        }

        foreach (var key in LlamaModelDefinition.VariantKeys)
        {
            if (key != "brown")
            {
                variants[(LlamaModelDefinition.LlamaEntityTypeId, key)] = new("baby_llama_" + key, 64, 64, LlamaParts, PlayerModelDefinition.DefaultFaceTransforms, "entity/llama/llama_" + key + "_baby", compiled: LlamaCompiled);
            }
        }

        return variants;
    }

    private static VanillaMeshDefinition BuildHorseMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        var body = root.AddOrReplaceChild("body", VanillaPartPose.Offset(0f, 12.5f, 0f),
            new VanillaCubeDefinition("body", 0, 13, -4f, -3.5f, -7f, 8f, 7f, 14f));
        body.AddOrReplaceChild("tail", VanillaPartPose.OffsetAndRotation(0f, -1f, 7f, -0.7418f, 0f, 0f),
            new VanillaCubeDefinition("tail", 24, 34, -1.5f, -1.5f, -1f, 3f, 3f, 8f));
        root.AddOrReplaceChild("left_hind_leg", VanillaPartPose.Offset(2.4f, 16f, 5.4f),
            new VanillaCubeDefinition("left_hind_leg", 12, 46, -1.5f, -1f, -1.5f, 3f, 9f, 3f));
        root.AddOrReplaceChild("right_hind_leg", VanillaPartPose.Offset(-2.4f, 16f, 5.4f),
            new VanillaCubeDefinition("right_hind_leg", 0, 46, -1.5f, -1f, -1.5f, 3f, 9f, 3f));
        root.AddOrReplaceChild("left_front_leg", VanillaPartPose.Offset(2.4f, 16f, -5.4f),
            new VanillaCubeDefinition("left_front_leg", 12, 34, -1.5f, -1f, -1.5f, 3f, 9f, 3f));
        root.AddOrReplaceChild("right_front_leg", VanillaPartPose.Offset(-2.4f, 16f, -5.4f),
            new VanillaCubeDefinition("right_front_leg", 0, 34, -1.5f, -1f, -1.5f, 3f, 9f, 3f));
        var headParts = root.AddOrReplaceChild("head_parts", VanillaPartPose.OffsetAndRotation(0f, 10f, -6f, -0.6109f, 0f, 0f),
            new VanillaCubeDefinition("head_parts", 30, 0, -2f, -6f, -2f, 4f, 8f, 4f));
        var head = headParts.AddOrReplaceChild("head", VanillaPartPose.Offset(0f, -6.0516f, -0.2951f),
            new VanillaCubeDefinition("head", 0, 0, -3f, -3.9484f, -6.705f, 6f, 4f, 9f));
        head.AddOrReplaceChild("left_ear", VanillaPartPose.OffsetAndRotation(2f, -4.2484f, 1.9451f, 0f, 0f, 0.2618f),
            new VanillaCubeDefinition("left_ear", 0, 4, -1f, -2.5f, -0.8f, 2f, 3f, 1f));
        head.AddOrReplaceChild("right_ear", VanillaPartPose.OffsetAndRotation(-2f, -4.2484f, 1.645f, 0f, 0f, -0.2618f),
            new VanillaCubeDefinition("right_ear", 0, 0, -1f, -2.5f, -0.5f, 2f, 3f, 1f));
        return new VanillaMeshDefinition(root);
    }

    private static VanillaMeshDefinition BuildLlamaMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        var head = root.AddOrReplaceChild("head", VanillaPartPose.Offset(0f, 12f, -4f),
            new VanillaCubeDefinition("head", 0, 0, -3f, -9f, -4f, 6f, 11f, 4f));
        head.AddOrReplaceChild("snout", VanillaPartPose.Zero,
            new VanillaCubeDefinition("snout", 0, 15, -1.5f, -7f, -7f, 3f, 3f, 3f));
        head.AddOrReplaceChild("ear_right", VanillaPartPose.Zero,
            new VanillaCubeDefinition("ear_right", 20, 4, 0.5f, -11f, -3f, 2f, 2f, 2f));
        head.AddOrReplaceChild("ear_left", VanillaPartPose.Zero,
            new VanillaCubeDefinition("ear_left", 20, 0, -2.5f, -11f, -3f, 2f, 2f, 2f));
        root.AddOrReplaceChild("right_hind_leg", VanillaPartPose.Offset(-2.5f, 16.5f, 4.5f),
            new VanillaCubeDefinition("right_hind_leg", 0, 45, -1.4f, -0.5f, -1.5f, 3f, 8f, 3f));
        root.AddOrReplaceChild("left_hind_leg", VanillaPartPose.Offset(2.5f, 16.5f, 4.5f),
            new VanillaCubeDefinition("left_hind_leg", 12, 45, -1.6f, -0.5f, -1.5f, 3f, 8f, 3f));
        root.AddOrReplaceChild("right_front_leg", VanillaPartPose.Offset(-2.5f, 16.5f, -3.5f),
            new VanillaCubeDefinition("right_front_leg", 0, 34, -1.4f, -0.5f, -1.5f, 3f, 8f, 3f));
        root.AddOrReplaceChild("left_front_leg", VanillaPartPose.Offset(2.5f, 16.5f, -3.5f),
            new VanillaCubeDefinition("left_front_leg", 12, 34, -1.6f, -0.5f, -1.5f, 3f, 8f, 3f));
        root.AddOrReplaceChild("body", VanillaPartPose.Offset(0f, 14f, 2.5f),
            new VanillaCubeDefinition("body", 0, 15, -4f, -3f, -8.5f, 8f, 6f, 13f));
        root.AddOrReplaceChild("right_chest", VanillaPartPose.OffsetAndRotation(-8.5f, 4f, 3f, 0f, -MathF.PI / 2f, 0f),
            new VanillaCubeDefinition("right_chest", 45, 28, -3f, 0f, 0f, 8f, 8f, 3f));
        root.AddOrReplaceChild("left_chest", VanillaPartPose.OffsetAndRotation(5.5f, 4f, 3f, 0f, -MathF.PI / 2f, 0f),
            new VanillaCubeDefinition("left_chest", 45, 41, -3f, 0f, 0f, 8f, 8f, 3f));
        return new VanillaMeshDefinition(root);
    }

    private static VanillaMeshDefinition BuildCamelMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        var body = root.AddOrReplaceChild("body", VanillaPartPose.Offset(0f, 7f, 0f),
            new VanillaCubeDefinition("body", 0, 14, -4.5f, -4f, -8f, 9f, 8f, 16f));
        body.AddOrReplaceChild("tail", VanillaPartPose.Offset(0f, -1.5f, 8.05f),
            new VanillaCubeDefinition("tail", 50, 38, -1.5f, -0.5f, 0f, 3f, 9f, 0f));
        var head = body.AddOrReplaceChild("head", VanillaPartPose.Offset(0f, 1f, -7.5f),
            new VanillaCubeDefinition("head", 20, 0, -2.5f, -3f, -7.5f, 5f, 5f, 7f));
        head.AddOrReplaceChild("head_upper", VanillaPartPose.Zero,
            new VanillaCubeDefinition("head_upper", 0, 0, -2.5f, -12f, -7.5f, 5f, 9f, 5f));
        head.AddOrReplaceChild("muzzle", VanillaPartPose.Zero,
            new VanillaCubeDefinition("muzzle", 0, 14, -2.5f, -12f, -10.5f, 5f, 4f, 3f));
        head.AddOrReplaceChild("right_ear", VanillaPartPose.Offset(-2.5f, -11f, -4f),
            new VanillaCubeDefinition("right_ear", 37, 0, -3f, -0.5f, -1f, 3f, 1f, 2f));
        head.AddOrReplaceChild("left_ear", VanillaPartPose.Offset(2.5f, -11f, -4f),
            new VanillaCubeDefinition("left_ear", 47, 0, 0f, -0.5f, -1f, 3f, 1f, 2f));
        root.AddOrReplaceChild("right_front_leg", VanillaPartPose.Offset(-3f, 11.5f, -5.5f),
            new VanillaCubeDefinition("right_front_leg", 36, 14, -1.5f, -0.5f, -1.5f, 3f, 13f, 3f));
        root.AddOrReplaceChild("left_front_leg", VanillaPartPose.Offset(3f, 11.5f, -5.5f),
            new VanillaCubeDefinition("left_front_leg", 48, 14, -1.5f, -0.5f, -1.5f, 3f, 13f, 3f));
        root.AddOrReplaceChild("left_hind_leg", VanillaPartPose.Offset(3f, 11.5f, 5.5f),
            new VanillaCubeDefinition("left_hind_leg", 12, 38, -1.5f, -0.5f, -1.5f, 3f, 13f, 3f));
        root.AddOrReplaceChild("right_hind_leg", VanillaPartPose.Offset(-3f, 11.5f, 5.5f),
            new VanillaCubeDefinition("right_hind_leg", 0, 38, -1.5f, -0.5f, -1.5f, 3f, 13f, 3f));
        return new VanillaMeshDefinition(root);
    }

    private static VanillaMeshDefinition BuildDonkeyMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        var body = root.AddOrReplaceChild("body", VanillaPartPose.Offset(1f, 14f, 0f),
            new VanillaCubeDefinition("body", 0, 13, -5f, -3f, -7f, 8f, 6f, 14f));
        var tail = body.AddOrReplaceChild("tail", VanillaPartPose.Offset(0f, -1.5f, 6.5f),
            new VanillaCubeDefinition("tail", 0, 0, 0f, 0f, 0f, 0f, 0f, 0f));
        tail.AddOrReplaceChild("tail_r1", VanillaPartPose.OffsetAndRotation(0f, 0f, 0f, -0.7418f, 0f, 0f),
            new VanillaCubeDefinition("tail_r1", 24, 33, -2.5f, -1f, -0.5f, 3f, 3f, 8f));
        body.AddOrReplaceChild("left_hind_leg", VanillaPartPose.Offset(2.25f, 3.5f, 5.25f),
            new VanillaCubeDefinition("left_hind_leg", 12, 44, -2.5f, -1.5f, -1.5f, 3f, 8f, 3f));
        body.AddOrReplaceChild("right_hind_leg", VanillaPartPose.Offset(-2.4f, 3.5f, 5.4f),
            new VanillaCubeDefinition("right_hind_leg", 0, 44, -2.5f, -1.5f, -1.5f, 3f, 8f, 3f));
        body.AddOrReplaceChild("left_front_leg", VanillaPartPose.Offset(2.4f, 3.5f, -5.3f),
            new VanillaCubeDefinition("left_front_leg", 12, 33, -2.5f, -1.5f, -1.5f, 3f, 8f, 3f));
        body.AddOrReplaceChild("right_front_leg", VanillaPartPose.Offset(-2.4f, 3.5f, -5.4f),
            new VanillaCubeDefinition("right_front_leg", 0, 33, -2.5f, -1.5f, -1.5f, 3f, 8f, 3f));
        var headParts = body.AddOrReplaceChild("head_parts", VanillaPartPose.Offset(0f, -3f, -5f),
            new VanillaCubeDefinition("head_parts", 0, 0, 0f, 0f, 0f, 0f, 0f, 0f));
        headParts.AddOrReplaceChild("neck_r1", VanillaPartPose.OffsetAndRotation(0f, 0f, 0f, 0.3927f, 0f, 0f),
            new VanillaCubeDefinition("neck_r1", 30, 9, -3f, -6f, -3f, 4f, 8f, 4f));
        var head = headParts.AddOrReplaceChild("head", VanillaPartPose.Offset(0f, -5f, -3f),
            new VanillaCubeDefinition("head", 0, 0, 0f, 0f, 0f, 0f, 0f, 0f));
        head.AddOrReplaceChild("head_r1", VanillaPartPose.OffsetAndRotation(0f, -1f, 1f, 0.3927f, 0f, 0f),
            new VanillaCubeDefinition("head_r1", 0, 0, -4f, -3.6f, -8.4f, 6f, 4f, 9f));
        head.AddOrReplaceChild("left_ear", VanillaPartPose.OffsetAndRotation(2f, -3.5f, -1f, 0.48f, 0f, 0.48f),
            new VanillaCubeDefinition("left_ear", 0, 0, -2f, -6.5f, -0.3f, 2f, 7f, 1f));
        head.AddOrReplaceChild("right_ear", VanillaPartPose.OffsetAndRotation(-2f, -3.5f, -1f, 0.48f, 0f, -0.48f),
            new VanillaCubeDefinition("right_ear", 22, 0, -2f, -6.5f, -0.3f, 2f, 7f, 1f, Mirror: true));
        body.AddOrReplaceChild("right_chest", VanillaPartPose.Offset(-1f, 10f, 0f),
            new VanillaCubeDefinition("right_chest", 0, 0, 0f, 0f, 0f, 0f, 0f, 0f));
        body.AddOrReplaceChild("left_chest", VanillaPartPose.Offset(-1f, 10f, 0f),
            new VanillaCubeDefinition("left_chest", 0, 0, 0f, 0f, 0f, 0f, 0f, 0f));
        return new VanillaMeshDefinition(root);
    }
}
