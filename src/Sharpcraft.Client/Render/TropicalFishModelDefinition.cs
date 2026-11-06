// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.World;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Tropical fish: two body shapes (small/large) tinted by the base colour, plus a pattern copy of the same geometry
/// tinted by the pattern colour. Bind pose only; tail sway and rotation offsets are not ported. Parts are declared with
/// the <c>VanillaMeshDefinition</c> DSL and baked via <c>VanillaMeshBake.Bake</c> (T-680); the pattern-copy logic is
/// unchanged.
/// </summary>
public static class TropicalFishModelDefinition
{
    public const int TropicalFishEntityTypeId = 140;

    public static readonly IReadOnlyList<string> VariantKeys =
    [
        "kob", "sunstreak", "snooper", "dasher", "brinely", "spotty",
        "flopper", "stripey", "glitter", "blockfish", "betty", "clayfish"
    ];

    /// <summary>Declarative DSL trees for the two tropical fish body shapes (T-680).</summary>
    private static readonly VanillaMeshDefinition SmallMesh = BuildSmallMesh();

    private static readonly EntityModelPart[] SmallParts = [.. VanillaMeshBake.Bake(SmallMesh, 0, 0)];

    private static readonly VanillaMeshDefinition LargeMesh = BuildLargeMesh();

    private static readonly EntityModelPart[] LargeParts = [.. VanillaMeshBake.Bake(LargeMesh, 0, 0)];

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

    private static VanillaMeshDefinition BuildSmallMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        root.AddOrReplaceChild("body", VanillaPartPose.Offset(0f, 22f, 0f),
            new VanillaCubeDefinition("body", 0, 0, -1f, -1.5f, -3f, 2f, 3f, 6f));
        root.AddOrReplaceChild("tail", VanillaPartPose.Offset(0f, 22f, 3f),
            new VanillaCubeDefinition("tail", 22, -6, 0f, -1.5f, 0f, 0f, 3f, 6f));
        root.AddOrReplaceChild("right_fin", VanillaPartPose.OffsetAndRotation(-1f, 22.5f, 0f, 0f, -MathF.PI / 4f, 0f),
            new VanillaCubeDefinition("right_fin", 2, 16, -2f, -1f, 0f, 2f, 2f, 0f));
        root.AddOrReplaceChild("left_fin", VanillaPartPose.OffsetAndRotation(1f, 22.5f, 0f, 0f, MathF.PI / 4f, 0f),
            new VanillaCubeDefinition("left_fin", 2, 12, 0f, -1f, 0f, 2f, 2f, 0f));
        root.AddOrReplaceChild("top_fin", VanillaPartPose.Offset(0f, 20.5f, -3f),
            new VanillaCubeDefinition("top_fin", 10, -5, 0f, -3f, 0f, 0f, 3f, 6f));
        return new VanillaMeshDefinition(root);
    }

    private static VanillaMeshDefinition BuildLargeMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        root.AddOrReplaceChild("body", VanillaPartPose.Offset(0f, 19f, 0f),
            new VanillaCubeDefinition("body", 0, 20, -1f, -3f, -3f, 2f, 6f, 6f));
        root.AddOrReplaceChild("tail", VanillaPartPose.Offset(0f, 19f, 3f),
            new VanillaCubeDefinition("tail", 21, 16, 0f, -3f, 0f, 0f, 6f, 5f));
        root.AddOrReplaceChild("right_fin", VanillaPartPose.OffsetAndRotation(-1f, 20f, 0f, 0f, -MathF.PI / 4f, 0f),
            new VanillaCubeDefinition("right_fin", 2, 16, -2f, 0f, 0f, 2f, 2f, 0f));
        root.AddOrReplaceChild("left_fin", VanillaPartPose.OffsetAndRotation(1f, 20f, 0f, 0f, MathF.PI / 4f, 0f),
            new VanillaCubeDefinition("left_fin", 2, 12, 0f, 0f, 0f, 2f, 2f, 0f));
        root.AddOrReplaceChild("top_fin", VanillaPartPose.Offset(0f, 16f, -3f),
            new VanillaCubeDefinition("top_fin", 20, 11, 0f, -4f, 0f, 0f, 4f, 6f));
        root.AddOrReplaceChild("bottom_fin", VanillaPartPose.Offset(0f, 22f, -3f),
            new VanillaCubeDefinition("bottom_fin", 20, 21, 0f, 0f, 0f, 0f, 4f, 6f));
        return new VanillaMeshDefinition(root);
    }
}
