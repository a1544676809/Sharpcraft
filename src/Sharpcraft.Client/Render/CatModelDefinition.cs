// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// AdultFelineModel used by AdultCatModel: head with nose/ears, rotated body, two tail segments and four legs.
/// AdultCatModel applies CAT_TRANSFORMER scale 0.8.
/// </summary>
/// <remarks>
/// Cat variants only change the texture (docs/analysis/render/entity-variants.md 3); the registry entry name is the
/// suffix of <c>entity/cat/cat_&lt;name&gt;</c>.
/// </remarks>
public static class CatModelDefinition
{
    public const int CatEntityTypeId = 21;
    public const int OcelotEntityTypeId = 92;

    private static readonly EntityModelPart[] Parts =
    [
        PlayerModelDefinition.FromVanilla("head_main", 0f, 15f, -9f, -2.5f, -2f, -3f, 5f, 4f, 5f, 0, 0),
        PlayerModelDefinition.FromVanilla("nose", 0f, 15f, -9f, -1.5f, -0.001f, -4f, 3f, 2f, 2f, 0, 24),
        PlayerModelDefinition.FromVanilla("ear1", 0f, 15f, -9f, -2f, -3f, 0f, 1f, 1f, 2f, 0, 10),
        PlayerModelDefinition.FromVanilla("ear2", 0f, 15f, -9f, 1f, -3f, 0f, 1f, 1f, 2f, 6, 10),
        PlayerModelDefinition.FromVanilla("body", 0f, 12f, -10f, -2f, 3f, -8f, 4f, 16f, 6f, 20, 0, rotationX: -MathF.PI / 2f),
        PlayerModelDefinition.FromVanilla("tail1", 0f, 15f, 8f, -0.5f, 0f, 0f, 1f, 8f, 1f, 0, 15, rotationX: -0.9f),
        PlayerModelDefinition.FromVanilla("tail2", 0f, 20f, 14f, -0.5f, 0f, 0f, 1f, 8f, 1f, 4, 15),
        PlayerModelDefinition.FromVanilla("left_hind_leg", 1.1f, 18f, 5f, -1f, 0f, 1f, 2f, 6f, 2f, 8, 13),
        PlayerModelDefinition.FromVanilla("right_hind_leg", -1.1f, 18f, 5f, -1f, 0f, 1f, 2f, 6f, 2f, 8, 13),
        PlayerModelDefinition.FromVanilla("left_front_leg", 1.2f, 14.1f, -5f, -1f, 0f, 0f, 2f, 10f, 2f, 40, 0),
        PlayerModelDefinition.FromVanilla("right_front_leg", -1.2f, 14.1f, -5f, -1f, 0f, 0f, 2f, 10f, 2f, 40, 0)
    ];

    private static readonly EntityModelPart[] CollarParts = EntityModelLayerHelpers.WithCollar(Parts, "entity/cat/cat_collar");

    public static readonly EntityModelDefinition Definition = new(
        "cat",
        64,
        32,
        CollarParts,
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/cat/cat_tabby",
        0.8f);

    public static readonly EntityModelDefinition Ocelot = new(
        "ocelot",
        64,
        32,
        CollarParts,
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/cat/ocelot");

    /// <summary>Registry entry names, in the vanilla bootstrap order used when no registry data is available.</summary>
    public static readonly IReadOnlyList<string> VariantKeys =
    [
        "tabby", "black", "red", "siamese", "british_shorthair",
        "calico", "persian", "ragdoll", "white", "jellie", "all_black"
    ];

    /// <summary>Variant key to definition, keyed by the registry entry name.</summary>
    public static readonly IReadOnlyDictionary<string, EntityModelDefinition> Variants = BuildVariants();

    private static Dictionary<string, EntityModelDefinition> BuildVariants()
    {
        var variants = new Dictionary<string, EntityModelDefinition>(StringComparer.Ordinal)
        {
            ["tabby"] = Definition
        };

        foreach (var key in VariantKeys)
        {
            if (key == "tabby")
            {
                continue;
            }

            variants[key] = new EntityModelDefinition(
                "cat_" + key,
                64,
                32,
                CollarParts,
                PlayerModelDefinition.DefaultFaceTransforms,
                "entity/cat/cat_" + key,
                0.8f);
        }

        return variants;
    }
}



