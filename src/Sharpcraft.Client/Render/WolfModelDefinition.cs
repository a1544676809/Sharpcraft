// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// AdultWolfModel: real head with ears/snout, body + upper body, four 2x8x2 legs, and the real tail.
/// </summary>
public static class WolfModelDefinition
{
    public const int WolfEntityTypeId = 152;

    public static readonly EntityModelDefinition Definition = new(
        "wolf",
        64,
        64,
        EntityModelLayerHelpers.WithCollar(
        [
            PlayerModelDefinition.FromVanilla("head", -1f, 13.5f, -7f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0),
            PlayerModelDefinition.FromVanillaChild("real_head", "head", 0f, 0f, 0f, -2f, -3f, -2f, 6f, 6f, 4f, 0, 0),
            PlayerModelDefinition.FromVanillaChild("right_ear", "head", 0f, 0f, 0f, -2f, -5f, 0f, 2f, 2f, 1f, 16, 14),
            PlayerModelDefinition.FromVanillaChild("left_ear", "head", 0f, 0f, 0f, 2f, -5f, 0f, 2f, 2f, 1f, 16, 14),
            PlayerModelDefinition.FromVanillaChild("snout", "head", 0f, 0f, 0f, -0.5f, -0.001f, -5f, 3f, 3f, 4f, 0, 10),
            PlayerModelDefinition.FromVanilla("body", 0f, 14f, 2f, -3f, -2f, -3f, 6f, 9f, 6f, 18, 14, rotationX: -MathF.PI / 2f),
            PlayerModelDefinition.FromVanilla("upper_body", -1f, 14f, -3f, -3f, -3f, -3f, 8f, 6f, 7f, 21, 0, rotationX: -MathF.PI / 2f),
            PlayerModelDefinition.FromVanilla("right_hind_leg", -2.5f, 16f, 7f, 0f, 0f, -1f, 2f, 8f, 2f, 0, 18),
            PlayerModelDefinition.FromVanilla("left_hind_leg", 0.5f, 16f, 7f, 0f, 0f, -1f, 2f, 8f, 2f, 0, 18),
            PlayerModelDefinition.FromVanilla("right_front_leg", -2.5f, 16f, -4f, 0f, 0f, -1f, 2f, 8f, 2f, 0, 18),
            PlayerModelDefinition.FromVanilla("left_front_leg", 0.5f, 16f, -4f, 0f, 0f, -1f, 2f, 8f, 2f, 0, 18),
            PlayerModelDefinition.FromVanilla("tail", -1f, 12f, 8f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0,
                rotationX: -MathF.PI / 5f),
            PlayerModelDefinition.FromVanillaChild("real_tail", "tail", 0f, 0f, 0f, 0f, 0f, -1f, 2f, 8f, 2f, 9, 18)
        ],
        "entity/wolf/wolf_collar"),
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/wolf/wolf",
        textureSelector: state => state.IsTame
            ? "entity/wolf/wolf_tame"
            : state.IsAngry
                ? "entity/wolf/wolf_angry"
                : "entity/wolf/wolf",
        additionalTexturePaths: ["entity/wolf/wolf_tame", "entity/wolf/wolf_angry"]);

    /// <summary>Registry entry names of the wolf variants, in vanilla bootstrap order.</summary>
    public static readonly IReadOnlyList<string> VariantKeys =
    [
        "pale", "spotted", "snowy", "black", "ashen", "rusty", "woods", "chestnut", "striped"
    ];

    private static readonly string[] VariantFiles =
    [
        "wolf", "wolf_spotted", "wolf_snowy", "wolf_black", "wolf_ashen", "wolf_rusty", "wolf_woods", "wolf_chestnut", "wolf_striped"
    ];

    /// <summary>Variant key to definition; texture follows the tame/angry state through the selector.</summary>
    public static readonly IReadOnlyDictionary<string, EntityModelDefinition> Variants = BuildVariants();

    private static Dictionary<string, EntityModelDefinition> BuildVariants()
    {
        var variants = new Dictionary<string, EntityModelDefinition>(StringComparer.Ordinal)
        {
            ["pale"] = Definition
        };

        for (var i = 0; i < VariantKeys.Count; i++)
        {
            var key = VariantKeys[i];
            if (key == "pale")
            {
                continue;
            }

            var wild = "entity/wolf/" + VariantFiles[i];
            var tame = wild + "_tame";
            var angry = wild + "_angry";
            var captured = wild;
            variants[key] = new EntityModelDefinition(
                "wolf_" + key,
                64,
                64,
                Definition.Parts,
                PlayerModelDefinition.DefaultFaceTransforms,
                captured,
                textureSelector: state => state.IsTame ? captured + "_tame" : state.IsAngry ? captured + "_angry" : captured,
                additionalTexturePaths: [tame, angry]);
        }

        return variants;
    }
}




