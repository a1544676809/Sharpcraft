// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// AbstractEquineModel / HorseModel: body, head parts with mane and mouth, four legs and tail. Parts are declared with
/// the <c>VanillaMeshDefinition</c> DSL and baked via <c>VanillaMeshBake.Bake</c> (T-685); WithHorseMarkings, the five
/// equine definitions and the seven coat variants are unchanged.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.animal.equine.HorseModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/equine/HorseModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class HorseModelDefinition
{
    public const int HorseEntityTypeId = 67;
    public const int DonkeyEntityTypeId = 37;
    public const int MuleEntityTypeId = 88;
    public const int ZombieHorseEntityTypeId = 155;
    public const int SkeletonHorseEntityTypeId = 119;

    /// <summary>Declarative DSL tree for the twelve horse parts (T-685).</summary>
    private static readonly VanillaMeshDefinition HorseMesh = BuildHorseMesh();

    private static EntityModelPart[] BuildParts()
    {
        // The reference lists all head_parts children before the head's ear grandchildren, and the tail (child of body)
        // last; Bake emits children depth-first. Reorder by name (values untouched) to keep the flat order identical.
        var baked = VanillaMeshBake.Bake(HorseMesh, 0, 0);
        var byName = new Dictionary<string, EntityModelPart>(StringComparer.Ordinal);
        foreach (var part in baked)
        {
            byName[part.Name] = part;
        }

        return
        [
            byName["body"], byName["head_parts"], byName["head"], byName["mane"], byName["upper_mouth"],
            byName["left_ear"], byName["right_ear"], byName["left_hind_leg"], byName["right_hind_leg"],
            byName["left_front_leg"], byName["right_front_leg"], byName["tail"]
        ];
    }

    public static readonly EntityModelDefinition Definition = new(
        "horse",
        64,
        64,
        EntityModelLayerHelpers.WithHorseMarkings(
            BuildParts(),
            "entity/horse/horse_markings_",
            false),
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/horse/horse_brown");

    public static readonly EntityModelDefinition Donkey = new(
        "donkey",
        64,
        64,
        HorseModelDefinition.Definition.Parts,
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/horse/donkey",
        0.87f);

    public static readonly EntityModelDefinition Mule = new(
        "mule",
        64,
        64,
        HorseModelDefinition.Definition.Parts,
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/horse/mule",
        0.92f);

    public static readonly EntityModelDefinition ZombieHorse = new(
        "zombie_horse", 64, 64, HorseModelDefinition.Definition.Parts,
        PlayerModelDefinition.DefaultFaceTransforms, "entity/horse/horse_zombie");

    public static readonly EntityModelDefinition SkeletonHorse = new(
        "skeleton_horse", 64, 64, HorseModelDefinition.Definition.Parts,
        PlayerModelDefinition.DefaultFaceTransforms, "entity/horse/horse_skeleton");

    /// <summary>Horse coat ids, in the <c>Variant</c> enum order used on the wire.</summary>
    public static readonly IReadOnlyList<string> VariantKeys =
    [
        "white", "creamy", "chestnut", "brown", "black", "gray", "dark_brown"
    ];

    /// <summary>Texture file stem per coat; the enum name "dark_brown" maps to "horse_darkbrown".</summary>
    private static readonly string[] VariantFiles =
    [
        "horse_white", "horse_creamy", "horse_chestnut", "horse_brown", "horse_black", "horse_gray", "horse_darkbrown"
    ];

    /// <summary>Coat key to definition; the geometry is identical, only the texture changes.</summary>
    public static readonly IReadOnlyDictionary<string, EntityModelDefinition> Variants = BuildVariants();

    private static Dictionary<string, EntityModelDefinition> BuildVariants()
    {
        var variants = new Dictionary<string, EntityModelDefinition>(StringComparer.Ordinal)
        {
            ["brown"] = Definition
        };

        for (var i = 0; i < VariantKeys.Count; i++)
        {
            var key = VariantKeys[i];
            if (key == "brown")
            {
                continue;
            }

            variants[key] = new EntityModelDefinition(
                "horse_" + key,
                64,
                64,
                Definition.Parts,
                PlayerModelDefinition.DefaultFaceTransforms,
                "entity/horse/" + VariantFiles[i]);
        }

        return variants;
    }

    private static VanillaMeshDefinition BuildHorseMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        var body = root.AddOrReplaceChild("body", VanillaPartPose.Offset(0f, 11f, 5f),
            new VanillaCubeDefinition("body", 0, 32, -5f, -8f, -17f, 10f, 10f, 22f, Grow: 0.05f));
        body.AddOrReplaceChild("tail", VanillaPartPose.OffsetAndRotation(0f, -5f, 2f, MathF.PI / 6f, 0f, 0f),
            new VanillaCubeDefinition("tail", 42, 36, -1.5f, 0f, 0f, 3f, 14f, 4f, Grow: 0.05f));
        var headParts = root.AddOrReplaceChild("head_parts", VanillaPartPose.OffsetAndRotation(0f, 4f, -12f, -MathF.PI / 6f, 0f, 0f),
            new VanillaCubeDefinition("head_parts", 0, 35, -2.05f, -6f, -2f, 4f, 12f, 7f));
        var head = headParts.AddOrReplaceChild("head", VanillaPartPose.Zero,
            new VanillaCubeDefinition("head", 0, 13, -3f, -11f, -2f, 6f, 5f, 7f, Grow: 0.05f));
        head.AddOrReplaceChild("left_ear", VanillaPartPose.Zero,
            new VanillaCubeDefinition("left_ear", 19, 16, 0.55f, -13f, 4f, 2f, 3f, 1f, Grow: -0.001f));
        head.AddOrReplaceChild("right_ear", VanillaPartPose.Zero,
            new VanillaCubeDefinition("right_ear", 19, 16, -2.55f, -13f, 4f, 2f, 3f, 1f, Grow: -0.001f));
        headParts.AddOrReplaceChild("mane", VanillaPartPose.Zero,
            new VanillaCubeDefinition("mane", 56, 36, -1f, -11f, 5.01f, 2f, 16f, 2f, Grow: 0.05f));
        headParts.AddOrReplaceChild("upper_mouth", VanillaPartPose.Zero,
            new VanillaCubeDefinition("upper_mouth", 0, 25, -2f, -11f, -7f, 4f, 5f, 5f, Grow: 0.05f));
        root.AddOrReplaceChild("left_hind_leg", VanillaPartPose.Offset(4f, 14f, 7f),
            new VanillaCubeDefinition("left_hind_leg", 48, 21, -3f, -1.01f, -1f, 4f, 11f, 4f, Mirror: true));
        root.AddOrReplaceChild("right_hind_leg", VanillaPartPose.Offset(-4f, 14f, 7f),
            new VanillaCubeDefinition("right_hind_leg", 48, 21, -1f, -1.01f, -1f, 4f, 11f, 4f));
        root.AddOrReplaceChild("left_front_leg", VanillaPartPose.Offset(4f, 14f, -10f),
            new VanillaCubeDefinition("left_front_leg", 48, 21, -3f, -1.01f, -1.9f, 4f, 11f, 4f, Mirror: true));
        root.AddOrReplaceChild("right_front_leg", VanillaPartPose.Offset(-4f, 14f, -10f),
            new VanillaCubeDefinition("right_front_leg", 48, 21, -1f, -1.01f, -1.9f, 4f, 11f, 4f));
        return new VanillaMeshDefinition(root);
    }
}



