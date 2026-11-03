// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.World;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Tropical fish: two body shapes (small/large) tinted by the base colour, plus a pattern copy of the same geometry
/// tinted by the pattern colour. Bind pose only; tail sway and rotation offsets are not ported.
/// </summary>
public static class TropicalFishModelDefinition
{
    public const int TropicalFishEntityTypeId = 140;

    public static readonly IReadOnlyList<string> VariantKeys =
    [
        "kob", "sunstreak", "snooper", "dasher", "brinely", "spotty",
        "flopper", "stripey", "glitter", "blockfish", "betty", "clayfish"
    ];

    private static readonly EntityModelPart[] SmallParts =
    [
        PlayerModelDefinition.FromVanilla("body", 0f, 22f, 0f, -1f, -1.5f, -3f, 2f, 3f, 6f, 0, 0),
        PlayerModelDefinition.FromVanilla("tail", 0f, 22f, 3f, 0f, -1.5f, 0f, 0f, 3f, 6f, 22, -6),
        PlayerModelDefinition.FromVanilla("right_fin", -1f, 22.5f, 0f, -2f, -1f, 0f, 2f, 2f, 0f, 2, 16, rotationY: -MathF.PI / 4f),
        PlayerModelDefinition.FromVanilla("left_fin", 1f, 22.5f, 0f, 0f, -1f, 0f, 2f, 2f, 0f, 2, 12, rotationY: MathF.PI / 4f),
        PlayerModelDefinition.FromVanilla("top_fin", 0f, 20.5f, -3f, 0f, -3f, 0f, 0f, 3f, 6f, 10, -5)
    ];

    private static readonly EntityModelPart[] LargeParts =
    [
        PlayerModelDefinition.FromVanilla("body", 0f, 19f, 0f, -1f, -3f, -3f, 2f, 6f, 6f, 0, 20),
        PlayerModelDefinition.FromVanilla("tail", 0f, 19f, 3f, 0f, -3f, 0f, 0f, 6f, 5f, 21, 16),
        PlayerModelDefinition.FromVanilla("right_fin", -1f, 20f, 0f, -2f, 0f, 0f, 2f, 2f, 0f, 2, 16, rotationY: -MathF.PI / 4f),
        PlayerModelDefinition.FromVanilla("left_fin", 1f, 20f, 0f, 0f, 0f, 0f, 2f, 2f, 0f, 2, 12, rotationY: MathF.PI / 4f),
        PlayerModelDefinition.FromVanilla("top_fin", 0f, 16f, -3f, 0f, -4f, 0f, 0f, 4f, 6f, 20, 11),
        PlayerModelDefinition.FromVanilla("bottom_fin", 0f, 22f, -3f, 0f, 0f, 0f, 0f, 4f, 6f, 20, 21)
    ];

    public static readonly EntityModelDefinition Definition = Variant("tropical_fish", "tropical_a", "tropical_a_pattern_1", SmallParts);

    public static readonly IReadOnlyDictionary<string, EntityModelDefinition> Variants = BuildVariants();

    private static Dictionary<string, EntityModelDefinition> BuildVariants()
    {
        var variants = new Dictionary<string, EntityModelDefinition>(StringComparer.Ordinal)
        {
            [VariantKeys[0]] = Definition
        };

        for (var index = 1; index < VariantKeys.Count; index++)
        {
            var key = VariantKeys[index];
            var large = index >= 6;
            var fileIndex = (index % 6) + 1;
            var baseName = large ? "tropical_b" : "tropical_a";
            var patternName = baseName + "_pattern_" + fileIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
            variants[key] = Variant("tropical_fish_" + key, baseName, patternName, large ? LargeParts : SmallParts);
        }

        return variants;
    }

    private static EntityModelDefinition Variant(string name, string baseTexture, string patternTexture, EntityModelPart[] bodyParts)
    {
        var parts = new List<EntityModelPart>(bodyParts);
        foreach (var part in bodyParts)
        {
            parts.Add(part with
            {
                Name = "pattern_" + part.Name,
                TexturePath = "entity/fish/" + patternTexture,
                TintSelector = static state => state.PatternTint == default ? Vector3.One : state.PatternTint
            });
        }

        return new EntityModelDefinition(
            name,
            32,
            32,
            [.. parts],
            PlayerModelDefinition.DefaultFaceTransforms,
            "entity/fish/" + baseTexture);
    }
}
