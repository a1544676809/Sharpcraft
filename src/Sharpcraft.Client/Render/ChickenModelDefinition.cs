// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// AdultChickenModel: head with beak/red_thing, rotated body, two legs and two wings.
/// </summary>
/// <remarks>
/// Variants come from the <c>chicken_variant</c> registry (docs/analysis/render/entity-variants.md 3). "warm" keeps the
/// normal geometry; "cold" uses <c>ColdChickenModel</c> (a zero-width fin on the body and an extra head layer).
/// </remarks>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.animal.chicken.ChickenModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/chicken/ChickenModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class ChickenModelDefinition
{
    public const int ChickenEntityTypeId = 26;

    private static readonly EntityModelPart[] NormalParts =
    [
        PlayerModelDefinition.FromVanilla("head", 0f, 15f, -4f, -2f, -6f, -2f, 4f, 6f, 3f, 0, 0),
        PlayerModelDefinition.FromVanilla("beak", 0f, 15f, -4f, -2f, -4f, -4f, 4f, 2f, 2f, 14, 0),
        PlayerModelDefinition.FromVanilla("red_thing", 0f, 15f, -4f, -1f, -2f, -3f, 2f, 2f, 2f, 14, 4),
        PlayerModelDefinition.FromVanilla("body", 0f, 16f, 0f, -3f, -4f, -3f, 6f, 8f, 6f, 0, 9, rotationX: -MathF.PI / 2f),
        PlayerModelDefinition.FromVanilla("right_leg", -2f, 19f, 1f, -1f, 0f, -3f, 3f, 5f, 3f, 26, 0),
        PlayerModelDefinition.FromVanilla("left_leg", 1f, 19f, 1f, -1f, 0f, -3f, 3f, 5f, 3f, 26, 0),
        PlayerModelDefinition.FromVanilla("right_wing", -4f, 13f, 0f, 0f, 0f, -3f, 1f, 4f, 6f, 24, 13),
        PlayerModelDefinition.FromVanilla("left_wing", 4f, 13f, 0f, -1f, 0f, -3f, 1f, 4f, 6f, 24, 13, mirror: true)
    ];

    private static readonly EntityModelPart[] ColdParts =
    [
        PlayerModelDefinition.FromVanilla("head", 0f, 15f, -4f, -2f, -6f, -2f, 4f, 6f, 3f, 0, 0),
        PlayerModelDefinition.FromVanilla("beak", 0f, 15f, -4f, -2f, -4f, -4f, 4f, 2f, 2f, 14, 0),
        PlayerModelDefinition.FromVanilla("red_thing", 0f, 15f, -4f, -1f, -2f, -3f, 2f, 2f, 2f, 14, 4),
        PlayerModelDefinition.FromVanilla("cold_head_layer", 0f, 15f, -4f, -3f, -7f, -2.015f, 6f, 3f, 4f, 44, 0),
        PlayerModelDefinition.FromVanilla("body", 0f, 16f, 0f, -3f, -4f, -3f, 6f, 8f, 6f, 0, 9, rotationX: -MathF.PI / 2f),
        PlayerModelDefinition.FromVanilla("cold_body_fin", 0f, 16f, 0f, 0f, 3f, -1f, 0f, 3f, 5f, 38, 9, rotationX: -MathF.PI / 2f),
        PlayerModelDefinition.FromVanilla("right_leg", -2f, 19f, 1f, -1f, 0f, -3f, 3f, 5f, 3f, 26, 0),
        PlayerModelDefinition.FromVanilla("left_leg", 1f, 19f, 1f, -1f, 0f, -3f, 3f, 5f, 3f, 26, 0),
        PlayerModelDefinition.FromVanilla("right_wing", -4f, 13f, 0f, 0f, 0f, -3f, 1f, 4f, 6f, 24, 13),
        PlayerModelDefinition.FromVanilla("left_wing", 4f, 13f, 0f, -1f, 0f, -3f, 1f, 4f, 6f, 24, 13, mirror: true)
    ];

    public static readonly EntityModelDefinition Definition = new(
        "chicken",
        64,
        32,
        NormalParts,
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/chicken/chicken_temperate");

    /// <summary>Variant key to definition, keyed by the registry entry name.</summary>
    public static readonly IReadOnlyDictionary<string, EntityModelDefinition> Variants =
        new Dictionary<string, EntityModelDefinition>(StringComparer.Ordinal)
        {
            ["temperate"] = Definition,
            ["warm"] = new("chicken_warm", 64, 32, NormalParts, PlayerModelDefinition.DefaultFaceTransforms, "entity/chicken/chicken_warm"),
            ["cold"] = new("chicken_cold", 64, 32, ColdParts, PlayerModelDefinition.DefaultFaceTransforms, "entity/chicken/chicken_cold")
        };
}
