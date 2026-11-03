// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// ParrotModel: body, tail, two wings, a head with crest/beak parts, and two small legs. Poses/animations are not ported.
/// </summary>
public static class ParrotModelDefinition
{
    public const int ParrotEntityTypeId = 99;

    private static readonly EntityModelPart[] Parts =
    [
        PlayerModelDefinition.FromVanilla("body", 0f, 16.5f, -3f, -1.5f, 0f, -1.5f, 3f, 6f, 3f, 2, 8, rotationX: -0.4937f),
        PlayerModelDefinition.FromVanilla("tail", 0f, 21.07f, 1.16f, -1.5f, -1f, -1f, 3f, 4f, 1f, 22, 1, rotationX: -1.015f),
        PlayerModelDefinition.FromVanilla("left_wing", 1.5f, 16.94f, -2.76f, -0.5f, 0f, -1.5f, 1f, 5f, 3f, 19, 8,
            rotationX: 0.6981f, rotationY: MathF.PI),
        PlayerModelDefinition.FromVanilla("right_wing", -1.5f, 16.94f, -2.76f, -0.5f, 0f, -1.5f, 1f, 5f, 3f, 19, 8,
            rotationX: 0.6981f, rotationY: MathF.PI),
        PlayerModelDefinition.FromVanilla("head", 0f, 15.69f, -2.76f, -1f, -1.5f, -1f, 2f, 3f, 2f, 2, 2),
        PlayerModelDefinition.FromVanillaChild("head2", "head", 0f, -2f, -1f, -1f, -0.5f, -2f, 2f, 1f, 4f, 10, 0),
        PlayerModelDefinition.FromVanillaChild("beak1", "head", 0f, -0.5f, -1.5f, -0.5f, -1f, -0.5f, 1f, 2f, 1f, 11, 7),
        PlayerModelDefinition.FromVanillaChild("beak2", "head", 0f, -1.75f, -2.45f, -0.5f, 0f, -0.5f, 1f, 2f, 1f, 16, 7),
        PlayerModelDefinition.FromVanillaChild("feather", "head", 0f, -2.15f, 0.15f, 0f, -4f, -2f, 0f, 5f, 4f, 2, 18,
            rotationX: -0.2214f),
        PlayerModelDefinition.FromVanilla("left_leg", 1f, 22f, -1.05f, -0.5f, 0f, -0.5f, 1f, 2f, 1f, 14, 18,
            rotationX: 0.0299f),
        PlayerModelDefinition.FromVanilla("right_leg", -1f, 22f, -1.05f, -0.5f, 0f, -0.5f, 1f, 2f, 1f, 14, 18,
            rotationX: 0.0299f)
    ];

    /// <summary>Synced variant ids in enum order: RED_BLUE, BLUE, GREEN, YELLOW_BLUE, GRAY.</summary>
    public static readonly IReadOnlyList<string> VariantKeys = ["red_blue", "blue", "green", "yellow_blue", "gray"];

    private static readonly string[] VariantFiles = ["parrot_red_blue", "parrot_blue", "parrot_green", "parrot_yellow_blue", "parrot_grey"];

    public static readonly EntityModelDefinition Definition = new(
        "parrot",
        32,
        32,
        Parts,
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/parrot/parrot_red_blue");

    public static readonly IReadOnlyDictionary<string, EntityModelDefinition> Variants = BuildVariants();

    private static Dictionary<string, EntityModelDefinition> BuildVariants()
    {
        var variants = new Dictionary<string, EntityModelDefinition>(StringComparer.Ordinal)
        {
            ["red_blue"] = Definition
        };

        for (var i = 0; i < VariantKeys.Count; i++)
        {
            if (VariantKeys[i] == "red_blue")
            {
                continue;
            }

            variants[VariantKeys[i]] = new EntityModelDefinition(
                "parrot_" + VariantKeys[i],
                32,
                32,
                Parts,
                PlayerModelDefinition.DefaultFaceTransforms,
                "entity/parrot/" + VariantFiles[i]);
        }

        return variants;
    }
}
