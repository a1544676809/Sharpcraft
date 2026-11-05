// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// FrogModel: empty "root" pivot carrying the body, head with eyes, mouth parts and both arms, plus the two hind legs.
/// </summary>
/// <remarks>
/// Bind pose only. Frog variants are registry entries that pick a texture (temperate/warm/cold); the croaking body is a
/// static part here because the croak animation is not ported.
/// </remarks>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.animal.frog.FrogModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/frog/FrogModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class FrogModelDefinition
{
    public const int FrogEntityTypeId = 56;

    private static readonly EntityModelPart[] Parts =
    [
        PlayerModelDefinition.FromVanilla("root", 0f, 24f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0),
        PlayerModelDefinition.FromVanillaChild("body", "root", 0f, -2f, 4f, -3.5f, -2f, -8f, 7f, 3f, 9f, 3, 1),
        PlayerModelDefinition.FromVanillaChild("body_layer", "body", 0f, 0f, 0f, -3.5f, -1f, -8f, 7f, 0f, 9f, 23, 22),
        PlayerModelDefinition.FromVanillaChild("head", "body", 0f, -2f, -1f, -3.5f, -2f, -7f, 7f, 3f, 9f, 0, 13),
        PlayerModelDefinition.FromVanillaChild("head_layer", "head", 0f, 0f, 0f, -3.5f, -1f, -7f, 7f, 0f, 9f, 23, 13),
        PlayerModelDefinition.FromVanillaChild("eyes", "head", -0.5f, 0f, 2f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0),
        PlayerModelDefinition.FromVanillaChild("right_eye", "eyes", -1.5f, -3f, -6.5f, -1.5f, -1f, -1.5f, 3f, 2f, 3f, 0, 0),
        PlayerModelDefinition.FromVanillaChild("left_eye", "eyes", 2.5f, -3f, -6.5f, -1.5f, -1f, -1.5f, 3f, 2f, 3f, 0, 5),
        PlayerModelDefinition.FromVanillaChild("croaking_body", "body", 0f, -1f, -5f, -3.5f, -0.1f, -2.9f, 7f, 2f, 3f, 26, 5,
            grow: -0.1f),
        PlayerModelDefinition.FromVanillaChild("tongue", "body", 0f, -1.01f, 1f, -2f, 0f, -7.1f, 4f, 0f, 7f, 17, 13),
        PlayerModelDefinition.FromVanillaChild("left_arm", "body", 4f, -1f, -6.5f, -1f, 0f, -1f, 2f, 3f, 3f, 0, 32),
        PlayerModelDefinition.FromVanillaChild("left_hand", "left_arm", 0f, 3f, -1f, -4f, 0.01f, -4f, 8f, 0f, 8f, 18, 40),
        PlayerModelDefinition.FromVanillaChild("right_arm", "body", -4f, -1f, -6.5f, -1f, 0f, -1f, 2f, 3f, 3f, 0, 38),
        PlayerModelDefinition.FromVanillaChild("right_hand", "right_arm", 0f, 3f, 0f, -4f, 0.01f, -5f, 8f, 0f, 8f, 2, 40),
        PlayerModelDefinition.FromVanillaChild("left_leg", "root", 3.5f, -3f, 4f, -1f, 0f, -2f, 3f, 3f, 4f, 14, 25),
        PlayerModelDefinition.FromVanillaChild("left_foot", "left_leg", 2f, 3f, 0f, -4f, 0.01f, -4f, 8f, 0f, 8f, 2, 32),
        PlayerModelDefinition.FromVanillaChild("right_leg", "root", -3.5f, -3f, 4f, -2f, 0f, -2f, 3f, 3f, 4f, 0, 25),
        PlayerModelDefinition.FromVanillaChild("right_foot", "right_leg", -2f, 3f, 0f, -4f, 0.01f, -4f, 8f, 0f, 8f, 18, 32)
    ];

    public static readonly EntityModelDefinition Definition = new(
        "frog",
        48,
        48,
        Parts,
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/frog/frog_temperate");

    /// <summary>Registry entry names of the frog variants, in vanilla bootstrap order.</summary>
    public static readonly IReadOnlyList<string> VariantKeys = ["temperate", "warm", "cold"];

    public static readonly IReadOnlyDictionary<string, EntityModelDefinition> Variants = BuildVariants();

    private static Dictionary<string, EntityModelDefinition> BuildVariants()
    {
        var variants = new Dictionary<string, EntityModelDefinition>(StringComparer.Ordinal)
        {
            ["temperate"] = Definition
        };

        foreach (var key in VariantKeys)
        {
            if (key == "temperate")
            {
                continue;
            }

            variants[key] = new EntityModelDefinition(
                "frog_" + key,
                48,
                48,
                Parts,
                PlayerModelDefinition.DefaultFaceTransforms,
                "entity/frog/frog_" + key);
        }

        return variants;
    }
}
