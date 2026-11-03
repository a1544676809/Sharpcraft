// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// CowModel / QuadrupedModel with legSize 12: head with snout and horns, rotated body with udder, four legs.
/// </summary>
/// <remarks>
/// Variants come from the <c>cow_variant</c> registry (docs/analysis/render/entity-variants.md 3). "warm" replaces the
/// head with <c>WarmCowModel</c> (four extra ear/horn boxes, two mirrored); "cold" uses <c>ColdCowModel</c> (0.5-pixel
/// outer body layer, snout UV 9,33 and two rotated horn children). The head has no rotation, so its children collapse
/// to plain pivot offsets in this flat model.
/// </remarks>
public static class CowModelDefinition
{
    public const int CowEntityTypeId = 30;
    public const int MooshroomEntityTypeId = 87;

    private static readonly EntityModelPart[] Legs =
    [
        PlayerModelDefinition.FromVanilla("right_hind_leg", -4f, 12f, 7f, -2f, 0f, -2f, 4f, 12f, 4f, 0, 16),
        PlayerModelDefinition.FromVanilla("left_hind_leg", 4f, 12f, 7f, -2f, 0f, -2f, 4f, 12f, 4f, 0, 16, mirror: true),
        PlayerModelDefinition.FromVanilla("right_front_leg", -4f, 12f, -5f, -2f, 0f, -2f, 4f, 12f, 4f, 0, 16),
        PlayerModelDefinition.FromVanilla("left_front_leg", 4f, 12f, -5f, -2f, 0f, -2f, 4f, 12f, 4f, 0, 16, mirror: true)
    ];

    private static readonly EntityModelPart[] NormalParts =
    [
        PlayerModelDefinition.FromVanilla("head", 0f, 4f, -8f, -4f, -4f, -6f, 8f, 8f, 6f, 0, 0),
        PlayerModelDefinition.FromVanilla("snout", 0f, 4f, -8f, -3f, 1f, -7f, 6f, 3f, 1f, 1, 33),
        PlayerModelDefinition.FromVanilla("right_horn", 0f, 4f, -8f, -5f, -5f, -5f, 1f, 3f, 1f, 22, 0),
        PlayerModelDefinition.FromVanilla("left_horn", 0f, 4f, -8f, 4f, -5f, -5f, 1f, 3f, 1f, 22, 0),
        PlayerModelDefinition.FromVanilla("body", 0f, 5f, 2f, -6f, -10f, -7f, 12f, 18f, 10f, 18, 4, rotationX: -MathF.PI / 2f),
        PlayerModelDefinition.FromVanilla("udder", 0f, 5f, 2f, -2f, 2f, -8f, 4f, 6f, 1f, 52, 0, rotationX: -MathF.PI / 2f),
        .. Legs
    ];

    private static readonly EntityModelPart[] WarmParts =
    [
        PlayerModelDefinition.FromVanilla("head", 0f, 4f, -8f, -4f, -4f, -6f, 8f, 8f, 6f, 0, 0),
        PlayerModelDefinition.FromVanilla("snout", 0f, 4f, -8f, -3f, 1f, -7f, 6f, 3f, 1f, 1, 33),
        PlayerModelDefinition.FromVanilla("right_ear", 0f, 4f, -8f, -8f, -3f, -5f, 4f, 2f, 2f, 27, 0),
        PlayerModelDefinition.FromVanilla("right_ear_tip", 0f, 4f, -8f, -8f, -5f, -5f, 2f, 2f, 2f, 39, 0),
        PlayerModelDefinition.FromVanilla("left_ear", 0f, 4f, -8f, 4f, -3f, -5f, 4f, 2f, 2f, 27, 0, mirror: true),
        PlayerModelDefinition.FromVanilla("left_ear_tip", 0f, 4f, -8f, 6f, -5f, -5f, 2f, 2f, 2f, 39, 0, mirror: true),
        PlayerModelDefinition.FromVanilla("body", 0f, 5f, 2f, -6f, -10f, -7f, 12f, 18f, 10f, 18, 4, rotationX: -MathF.PI / 2f),
        PlayerModelDefinition.FromVanilla("udder", 0f, 5f, 2f, -2f, 2f, -8f, 4f, 6f, 1f, 52, 0, rotationX: -MathF.PI / 2f),
        .. Legs
    ];

    private static readonly EntityModelPart[] ColdParts =
    [
        PlayerModelDefinition.FromVanilla("head", 0f, 4f, -8f, -4f, -4f, -6f, 8f, 8f, 6f, 0, 0),
        PlayerModelDefinition.FromVanilla("snout", 0f, 4f, -8f, -3f, 1f, -7f, 6f, 3f, 1f, 9, 33),
        PlayerModelDefinition.FromVanilla("right_horn", -4.5f, 1.5f, -11.5f, -1.5f, -4.5f, -0.5f, 2f, 6f, 2f, 0, 40,
            rotationX: 1.5708f),
        PlayerModelDefinition.FromVanilla("left_horn", 5.5f, 1.5f, -13f, -1.5f, -3f, -0.5f, 2f, 6f, 2f, 0, 32,
            rotationX: 1.5708f),
        PlayerModelDefinition.FromVanilla("body", 0f, 5f, 2f, -6f, -10f, -7f, 12f, 18f, 10f, 18, 4,
            overlays: [new("cold_cow_layer", 0.5f, 20, 32)],
            rotationX: -MathF.PI / 2f),
        PlayerModelDefinition.FromVanilla("udder", 0f, 5f, 2f, -2f, 2f, -8f, 4f, 6f, 1f, 52, 0, rotationX: -MathF.PI / 2f),
        .. Legs
    ];

    public static readonly EntityModelDefinition Definition = new(
        "cow",
        64,
        64,
        NormalParts,
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/cow/cow_temperate");

    public static readonly EntityModelDefinition Mooshroom = new(
        "mooshroom",
        64,
        64,
        NormalParts,
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/cow/mooshroom_red");

    /// <summary>Variant key to definition, keyed by the registry entry name.</summary>
    public static readonly IReadOnlyDictionary<string, EntityModelDefinition> Variants =
        new Dictionary<string, EntityModelDefinition>(StringComparer.Ordinal)
        {
            ["temperate"] = Definition,
            ["warm"] = new("cow_warm", 64, 64, WarmParts, PlayerModelDefinition.DefaultFaceTransforms, "entity/cow/cow_warm"),
            ["cold"] = new("cow_cold", 64, 64, ColdParts, PlayerModelDefinition.DefaultFaceTransforms, "entity/cow/cow_cold")
        };
}
