// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// AdultAxolotlModel: body with a dorsal fin plane, head with top/left/right gills, four flat legs and a flat tail.
/// </summary>
/// <remarks>
/// Bind pose only. Variants (lucy/wild/gold/cyan/blue) are a synced integer, not a registry, and only swap the texture.
/// </remarks>
public static class AxolotlModelDefinition
{
    public const int AxolotlEntityTypeId = 7;

    private static readonly EntityModelPart[] Parts =
    [
        PlayerModelDefinition.FromVanilla("body", 0f, 19.5f, 5f, -4f, -2f, -9f, 8f, 4f, 10f, 0, 11),
        PlayerModelDefinition.FromVanillaChild("body_layer", "body", 0f, 0f, 0f, 0f, -3f, -8f, 0f, 5f, 9f, 2, 17),
        PlayerModelDefinition.FromVanillaChild("head", "body", 0f, 0f, -9f, -4f, -3f, -5f, 8f, 5f, 5f, 0, 1, grow: 0.001f),
        PlayerModelDefinition.FromVanillaChild("top_gills", "head", 0f, -3f, -1f, -4f, -3f, 0f, 8f, 3f, 0f, 3, 37, grow: 0.001f),
        PlayerModelDefinition.FromVanillaChild("left_gills", "head", -4f, 0f, -1f, -3f, -5f, 0f, 3f, 7f, 0f, 0, 40, grow: 0.001f),
        PlayerModelDefinition.FromVanillaChild("right_gills", "head", 4f, 0f, -1f, 0f, -5f, 0f, 3f, 7f, 0f, 11, 40, grow: 0.001f),
        PlayerModelDefinition.FromVanillaChild("right_hind_leg", "body", -3.5f, 1f, -1f, -2f, 0f, 0f, 3f, 5f, 0f, 2, 13, grow: 0.001f),
        PlayerModelDefinition.FromVanillaChild("left_hind_leg", "body", 3.5f, 1f, -1f, -1f, 0f, 0f, 3f, 5f, 0f, 2, 13, grow: 0.001f),
        PlayerModelDefinition.FromVanillaChild("right_front_leg", "body", -3.5f, 1f, -8f, -2f, 0f, 0f, 3f, 5f, 0f, 2, 13, grow: 0.001f),
        PlayerModelDefinition.FromVanillaChild("left_front_leg", "body", 3.5f, 1f, -8f, -1f, 0f, 0f, 3f, 5f, 0f, 2, 13, grow: 0.001f),
        PlayerModelDefinition.FromVanillaChild("tail", "body", 0f, 0f, 1f, 0f, -3f, 0f, 0f, 5f, 12f, 2, 19)
    ];

    /// <summary>Synced variant ids: LUCY, WILD, GOLD, CYAN, BLUE.</summary>
    public static readonly IReadOnlyList<string> VariantKeys = ["lucy", "wild", "gold", "cyan", "blue"];

    public static readonly EntityModelDefinition Definition = new(
        "axolotl",
        64,
        64,
        Parts,
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/axolotl/axolotl_lucy");

    public static readonly IReadOnlyDictionary<string, EntityModelDefinition> Variants = BuildVariants();

    private static Dictionary<string, EntityModelDefinition> BuildVariants()
    {
        var variants = new Dictionary<string, EntityModelDefinition>(StringComparer.Ordinal)
        {
            ["lucy"] = Definition
        };

        foreach (var key in VariantKeys)
        {
            if (key == "lucy")
            {
                continue;
            }

            variants[key] = new EntityModelDefinition(
                "axolotl_" + key,
                64,
                64,
                Parts,
                PlayerModelDefinition.DefaultFaceTransforms,
                "entity/axolotl/axolotl_" + key);
        }

        return variants;
    }
}
