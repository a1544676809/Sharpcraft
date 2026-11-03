// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// AbstractEquineModel / HorseModel: body, head parts with mane and mouth, four legs and tail.
/// </summary>
public static class HorseModelDefinition
{
    public const int HorseEntityTypeId = 67;
    public const int DonkeyEntityTypeId = 37;
    public const int MuleEntityTypeId = 88;
    public const int ZombieHorseEntityTypeId = 155;
    public const int SkeletonHorseEntityTypeId = 119;

    public static readonly EntityModelDefinition Definition = new(
        "horse",
        64,
        64,
        EntityModelLayerHelpers.WithHorseMarkings(
        [
            PlayerModelDefinition.FromVanilla("body", 0f, 11f, 5f, -5f, -8f, -17f, 10f, 10f, 22f, 0, 32, grow: 0.05f),
            PlayerModelDefinition.FromVanilla("head_parts", 0f, 4f, -12f, -2.05f, -6f, -2f, 4f, 12f, 7f, 0, 35,
                rotationX: -MathF.PI / 6f),
            PlayerModelDefinition.FromVanillaChild("head", "head_parts", 0f, 0f, 0f, -3f, -11f, -2f, 6f, 5f, 7f, 0, 13,
                grow: 0.05f),
            PlayerModelDefinition.FromVanillaChild("mane", "head_parts", 0f, 0f, 0f, -1f, -11f, 5.01f, 2f, 16f, 2f, 56, 36,
                grow: 0.05f),
            PlayerModelDefinition.FromVanillaChild("upper_mouth", "head_parts", 0f, 0f, 0f, -2f, -11f, -7f, 4f, 5f, 5f, 0, 25,
                grow: 0.05f),
            PlayerModelDefinition.FromVanillaChild("left_ear", "head", 0f, 0f, 0f, 0.55f, -13f, 4f, 2f, 3f, 1f, 19, 16,
                grow: -0.001f),
            PlayerModelDefinition.FromVanillaChild("right_ear", "head", 0f, 0f, 0f, -2.55f, -13f, 4f, 2f, 3f, 1f, 19, 16,
                grow: -0.001f),
            PlayerModelDefinition.FromVanilla("left_hind_leg", 4f, 14f, 7f, -3f, -1.01f, -1f, 4f, 11f, 4f, 48, 21, mirror: true),
            PlayerModelDefinition.FromVanilla("right_hind_leg", -4f, 14f, 7f, -1f, -1.01f, -1f, 4f, 11f, 4f, 48, 21),
            PlayerModelDefinition.FromVanilla("left_front_leg", 4f, 14f, -10f, -3f, -1.01f, -1.9f, 4f, 11f, 4f, 48, 21, mirror: true),
            PlayerModelDefinition.FromVanilla("right_front_leg", -4f, 14f, -10f, -1f, -1.01f, -1.9f, 4f, 11f, 4f, 48, 21),
            PlayerModelDefinition.FromVanillaChild("tail", "body", 0f, -5f, 2f, -1.5f, 0f, 0f, 3f, 14f, 4f, 42, 36,
                rotationX: MathF.PI / 6f, grow: 0.05f),
        ],
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
}



