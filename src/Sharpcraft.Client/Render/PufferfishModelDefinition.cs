// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// Pufferfish has three separate models for its puff state (0 small, 1 medium, 2 large), all sharing one texture.
/// </summary>
public static class PufferfishModelDefinition
{
    public const int PufferfishEntityTypeId = 110;

    private static readonly EntityModelPart[] SmallParts =
    [
        PlayerModelDefinition.FromVanilla("body", 0f, 23f, 0f, -1.5f, -2f, -1.5f, 3f, 2f, 3f, 0, 27),
        PlayerModelDefinition.FromVanilla("right_eye", 0f, 20f, 0f, -1.5f, 0f, -1.5f, 1f, 1f, 1f, 24, 6),
        PlayerModelDefinition.FromVanilla("left_eye", 0f, 20f, 0f, 0.5f, 0f, -1.5f, 1f, 1f, 1f, 28, 6),
        PlayerModelDefinition.FromVanilla("back_fin", 0f, 22f, 1.5f, -1.5f, 0f, 0f, 3f, 0f, 3f, -3, 0),
        PlayerModelDefinition.FromVanilla("right_fin", -1.5f, 22f, -1.5f, -1f, 0f, 0f, 1f, 0f, 2f, 25, 0),
        PlayerModelDefinition.FromVanilla("left_fin", 1.5f, 22f, -1.5f, 0f, 0f, 0f, 1f, 0f, 2f, 25, 0)
    ];

    private static readonly EntityModelPart[] MediumParts =
    [
        PlayerModelDefinition.FromVanilla("body", 0f, 22f, 0f, -2.5f, -5f, -2.5f, 5f, 5f, 5f, 12, 22),
        PlayerModelDefinition.FromVanilla("right_blue_fin", -2.5f, 18f, -1.5f, -2f, 0f, 0f, 2f, 0f, 2f, 24, 0),
        PlayerModelDefinition.FromVanilla("left_blue_fin", 2.5f, 18f, -1.5f, 0f, 0f, 0f, 2f, 0f, 2f, 24, 3),
        PlayerModelDefinition.FromVanilla("top_front_fin", 0f, 17f, -2.5f, -2.5f, -1f, 0f, 5f, 1f, 0f, 19, 17,
            rotationX: -MathF.PI / 4f),
        PlayerModelDefinition.FromVanilla("top_back_fin", 0f, 17f, 2.5f, -2.5f, -1f, 0f, 5f, 1f, 0f, 11, 17,
            rotationX: MathF.PI / 4f),
        PlayerModelDefinition.FromVanilla("right_front_fin", -2.5f, 22f, -2.5f, -1f, -5f, 0f, 1f, 5f, 0f, 5, 17,
            rotationY: MathF.PI / 4f),
        PlayerModelDefinition.FromVanilla("right_back_fin", -2.5f, 22f, 2.5f, -1f, -5f, 0f, 1f, 5f, 0f, 9, 17,
            rotationY: -MathF.PI / 4f),
        PlayerModelDefinition.FromVanilla("left_back_fin", 2.5f, 22f, 2.5f, 0f, -5f, 0f, 1f, 5f, 0f, 1, 17,
            rotationY: MathF.PI / 4f),
        PlayerModelDefinition.FromVanilla("left_front_fin", 2.5f, 22f, -2.5f, 0f, -5f, 0f, 1f, 5f, 0f, 1, 17,
            rotationY: -MathF.PI / 4f),
        PlayerModelDefinition.FromVanilla("bottom_back_fin", -2.5f, 22f, 2.5f, 0f, 0f, 0f, 5f, 1f, 0f, 18, 20,
            rotationX: -MathF.PI / 4f),
        PlayerModelDefinition.FromVanilla("bottom_front_fin", 0f, 22f, -2.5f, -2.5f, 0f, 0f, 5f, 1f, 1f, 17, 19,
            rotationX: MathF.PI / 4f)
    ];

    private static readonly EntityModelPart[] LargeParts =
    [
        PlayerModelDefinition.FromVanilla("body", 0f, 22f, 0f, -4f, -8f, -4f, 8f, 8f, 8f, 0, 0),
        PlayerModelDefinition.FromVanilla("right_blue_fin", -4f, 15f, -2f, -2f, 0f, -1f, 2f, 1f, 2f, 24, 0),
        PlayerModelDefinition.FromVanilla("left_blue_fin", 4f, 15f, -2f, 0f, 0f, -1f, 2f, 1f, 2f, 24, 3),
        PlayerModelDefinition.FromVanilla("top_front_fin", 0f, 14f, -4f, -4f, -1f, 0f, 8f, 1f, 0f, 15, 17,
            rotationX: -MathF.PI / 4f),
        PlayerModelDefinition.FromVanilla("top_middle_fin", 0f, 14f, 0f, -4f, -1f, 0f, 8f, 1f, 1f, 14, 16),
        PlayerModelDefinition.FromVanilla("top_back_fin", 0f, 14f, 4f, -4f, -1f, 0f, 8f, 1f, 0f, 23, 18,
            rotationX: MathF.PI / 4f),
        PlayerModelDefinition.FromVanilla("right_front_fin", -4f, 22f, -4f, -1f, -8f, 0f, 1f, 8f, 0f, 5, 17,
            rotationY: MathF.PI / 4f),
        PlayerModelDefinition.FromVanilla("left_front_fin", 4f, 22f, -4f, 0f, -8f, 0f, 1f, 8f, 0f, 1, 17,
            rotationY: -MathF.PI / 4f),
        PlayerModelDefinition.FromVanilla("bottom_front_fin", 0f, 22f, -4f, -4f, 0f, 0f, 8f, 1f, 0f, 15, 20,
            rotationX: MathF.PI / 4f),
        PlayerModelDefinition.FromVanilla("bottom_middle_fin", 0f, 22f, 0f, -4f, 0f, 0f, 8f, 1f, 0f, 15, 20),
        PlayerModelDefinition.FromVanilla("bottom_back_fin", 0f, 22f, 4f, -4f, 0f, 0f, 8f, 1f, 0f, 15, 20,
            rotationX: -MathF.PI / 4f),
        PlayerModelDefinition.FromVanilla("right_back_fin", -4f, 22f, 4f, -1f, -8f, 0f, 1f, 8f, 0f, 9, 17,
            rotationY: -MathF.PI / 4f),
        PlayerModelDefinition.FromVanilla("left_back_fin", 4f, 22f, 4f, 0f, -8f, 0f, 1f, 8f, 0f, 9, 17,
            rotationY: MathF.PI / 4f)
    ];

    /// <summary>Puff state ids: 0 small, 1 medium, 2 large.</summary>
    public static readonly IReadOnlyList<string> VariantKeys = ["small", "medium", "large"];

    public static readonly EntityModelDefinition Definition = new(
        "pufferfish",
        32,
        32,
        SmallParts,
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/fish/pufferfish");

    public static readonly IReadOnlyDictionary<string, EntityModelDefinition> Variants =
        new Dictionary<string, EntityModelDefinition>(StringComparer.Ordinal)
        {
            ["small"] = Definition,
            ["medium"] = new("pufferfish_medium", 32, 32, MediumParts, PlayerModelDefinition.DefaultFaceTransforms, "entity/fish/pufferfish"),
            ["large"] = new("pufferfish_large", 32, 32, LargeParts, PlayerModelDefinition.DefaultFaceTransforms, "entity/fish/pufferfish")
        };
}
