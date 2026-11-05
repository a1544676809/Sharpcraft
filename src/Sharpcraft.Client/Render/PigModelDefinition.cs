// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// PigModel / QuadrupedModel with legSize 6: rotated body, head with snout, and four legs.
/// </summary>
/// <remarks>
/// Variants come from the <c>pig_variant</c> registry (docs/analysis/render/entity-variants.md 3). "warm" keeps the
/// normal geometry and only changes the texture; "cold" uses <c>ColdPigModel</c>, which adds a 0.5-pixel outer layer to
/// the body (UV 28,32). Baby models are out of scope for now.
/// </remarks>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.animal.pig.PigModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/pig/PigModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class PigModelDefinition
{
    public const int PigEntityTypeId = 101;

    private static readonly EntityModelPart[] NormalParts =
    [
        PlayerModelDefinition.FromVanilla("head", 0f, 12f, -6f, -4f, -4f, -8f, 8f, 8f, 8f, 0, 0),
        PlayerModelDefinition.FromVanilla("snout", 0f, 12f, -6f, -2f, 0f, -9f, 4f, 3f, 1f, 16, 16),
        PlayerModelDefinition.FromVanilla("body", 0f, 11f, 2f, -5f, -10f, -7f, 10f, 16f, 8f, 28, 8,
            rotationX: -MathF.PI / 2f),
        PlayerModelDefinition.FromVanilla("right_hind_leg", -3f, 18f, 7f, -2f, 0f, -2f, 4f, 6f, 4f, 0, 16),
        PlayerModelDefinition.FromVanilla("left_hind_leg", 3f, 18f, 7f, -2f, 0f, -2f, 4f, 6f, 4f, 0, 16, mirror: true),
        PlayerModelDefinition.FromVanilla("right_front_leg", -3f, 18f, -5f, -2f, 0f, -2f, 4f, 6f, 4f, 0, 16),
        PlayerModelDefinition.FromVanilla("left_front_leg", 3f, 18f, -5f, -2f, 0f, -2f, 4f, 6f, 4f, 0, 16, mirror: true)
    ];

    private static readonly EntityModelPart[] ColdParts =
    [
        PlayerModelDefinition.FromVanilla("head", 0f, 12f, -6f, -4f, -4f, -8f, 8f, 8f, 8f, 0, 0),
        PlayerModelDefinition.FromVanilla("snout", 0f, 12f, -6f, -2f, 0f, -9f, 4f, 3f, 1f, 16, 16),
        PlayerModelDefinition.FromVanilla("body", 0f, 11f, 2f, -5f, -10f, -7f, 10f, 16f, 8f, 28, 8,
            overlays: [new("cold_pig_layer", 0.5f, 28, 32)],
            rotationX: -MathF.PI / 2f),
        PlayerModelDefinition.FromVanilla("right_hind_leg", -3f, 18f, 7f, -2f, 0f, -2f, 4f, 6f, 4f, 0, 16),
        PlayerModelDefinition.FromVanilla("left_hind_leg", 3f, 18f, 7f, -2f, 0f, -2f, 4f, 6f, 4f, 0, 16, mirror: true),
        PlayerModelDefinition.FromVanilla("right_front_leg", -3f, 18f, -5f, -2f, 0f, -2f, 4f, 6f, 4f, 0, 16),
        PlayerModelDefinition.FromVanilla("left_front_leg", 3f, 18f, -5f, -2f, 0f, -2f, 4f, 6f, 4f, 0, 16, mirror: true)
    ];

    public static readonly EntityModelDefinition Definition = new(
        "pig",
        64,
        64,
        NormalParts,
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/pig/pig_temperate");

    /// <summary>Variant key to definition, keyed by the registry entry name ("temperate", "warm", "cold").</summary>
    public static readonly IReadOnlyDictionary<string, EntityModelDefinition> Variants =
        new Dictionary<string, EntityModelDefinition>(StringComparer.Ordinal)
        {
            ["temperate"] = Definition,
            ["warm"] = new("pig_warm", 64, 64, NormalParts, PlayerModelDefinition.DefaultFaceTransforms, "entity/pig/pig_warm"),
            ["cold"] = new("pig_cold", 64, 64, ColdParts, PlayerModelDefinition.DefaultFaceTransforms, "entity/pig/pig_cold")
        };
}
