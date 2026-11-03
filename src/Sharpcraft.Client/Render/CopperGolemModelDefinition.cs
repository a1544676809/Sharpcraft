// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// CopperGolemModel: a small humanoid with body, head (face + nose + two antenna boxes), two arms and two legs.
/// Four weathering states swap both the body texture and the emissive eye texture.
/// </summary>
public static class CopperGolemModelDefinition
{
    public const int CopperGolemEntityTypeId = 28;

    private static readonly EntityModelPart[] Parts =
    [
        PlayerModelDefinition.FromVanilla("body", 0f, 19f, 0f, -4f, -6f, -3f, 8f, 6f, 6f, 0, 15),
        PlayerModelDefinition.FromVanillaChild("head", "body", 0f, -6f, 0f, -4f, -5f, -5f, 8f, 5f, 10f, 0, 0, grow: 0.015f),
        PlayerModelDefinition.FromVanillaChild("nose", "head", 0f, 0f, 0f, -1f, -2f, -6f, 2f, 3f, 2f, 56, 0),
        PlayerModelDefinition.FromVanillaChild("antenna", "head", 0f, 0f, 0f, -1f, -9f, -1f, 2f, 4f, 2f, 37, 8, grow: -0.015f),
        PlayerModelDefinition.FromVanillaChild("antenna_tip", "head", 0f, 0f, 0f, -2f, -13f, -2f, 4f, 4f, 4f, 37, 0, grow: -0.015f),
        PlayerModelDefinition.FromVanillaChild("right_arm", "body", -4f, -6f, 0f, -3f, -1f, -2f, 3f, 10f, 4f, 36, 16),
        PlayerModelDefinition.FromVanillaChild("left_arm", "body", 4f, -6f, 0f, 0f, -1f, -2f, 3f, 10f, 4f, 50, 16),
        PlayerModelDefinition.FromVanilla("right_leg", 0f, 19f, 0f, -4f, 0f, -2f, 4f, 5f, 4f, 0, 27),
        PlayerModelDefinition.FromVanilla("left_leg", 0f, 19f, 0f, 0f, 0f, -2f, 4f, 5f, 4f, 16, 27)
    ];

    /// <summary>Weathering states in registry order: UNAFFECTED, EXPOSED, WEATHERED, OXIDIZED.</summary>
    public static readonly IReadOnlyList<string> VariantKeys = ["unaffected", "exposed", "weathered", "oxidized"];

    private static readonly string[] BodyFiles = ["copper_golem", "copper_golem_exposed", "copper_golem_weathered", "copper_golem_oxidized"];
    private static readonly string[] EyeFiles = ["copper_golem_eyes", "copper_golem_eyes_exposed", "copper_golem_eyes_weathered", "copper_golem_eyes_oxidized"];

    public static readonly EntityModelDefinition Definition = Create("copper_golem", 0);

    public static readonly IReadOnlyDictionary<string, EntityModelDefinition> Variants = BuildVariants();

    private static EntityModelDefinition Create(string name, int index) => new(
        name,
        64,
        64,
        Parts,
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/copper_golem/" + BodyFiles[index],
        1f,
        "entity/copper_golem/" + EyeFiles[index],
        additionalTexturePaths: ["entity/copper_golem/" + EyeFiles[index]]);

    private static Dictionary<string, EntityModelDefinition> BuildVariants()
    {
        var variants = new Dictionary<string, EntityModelDefinition>(StringComparer.Ordinal);
        for (var i = 0; i < VariantKeys.Count; i++)
        {
            variants[VariantKeys[i]] = i == 0 ? Definition : Create("copper_golem_" + VariantKeys[i], i);
        }

        return variants;
    }
}
