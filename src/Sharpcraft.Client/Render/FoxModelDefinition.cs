// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// AdultFoxModel: head with ears/nose, rotated body, four legs and a tail.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.animal.fox.FoxModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/fox/FoxModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class FoxModelDefinition
{
    public const int FoxEntityTypeId = 55;

    public static readonly EntityModelDefinition Definition = new(
        "fox",
        48,
        32,
        [
            PlayerModelDefinition.FromVanilla("head", -1f, 16.5f, -3f, -3f, -2f, -5f, 8f, 6f, 6f, 1, 5),
            PlayerModelDefinition.FromVanillaChild("right_ear", "head", 0f, 0f, 0f, -3f, -4f, -4f, 2f, 2f, 1f, 8, 1),
            PlayerModelDefinition.FromVanillaChild("left_ear", "head", 0f, 0f, 0f, 3f, -4f, -4f, 2f, 2f, 1f, 15, 1),
            PlayerModelDefinition.FromVanillaChild("nose", "head", 0f, 0f, 0f, -1f, 2.01f, -8f, 4f, 2f, 3f, 6, 18),
            PlayerModelDefinition.FromVanilla("body", 0f, 16f, -6f, -3f, 3.999f, -3.5f, 6f, 11f, 6f, 24, 15, rotationX: -MathF.PI / 2f),
            PlayerModelDefinition.FromVanilla("right_hind_leg", -5f, 17.5f, 7f, 2f, 0.5f, -1f, 2f, 6f, 2f, 13, 24),
            PlayerModelDefinition.FromVanilla("left_hind_leg", -1f, 17.5f, 7f, 2f, 0.5f, -1f, 2f, 6f, 2f, 4, 24),
            PlayerModelDefinition.FromVanilla("right_front_leg", -5f, 17.5f, 0f, 2f, 0.5f, -1f, 2f, 6f, 2f, 13, 24),
            PlayerModelDefinition.FromVanilla("left_front_leg", -1f, 17.5f, 0f, 2f, 0.5f, -1f, 2f, 6f, 2f, 4, 24),
            PlayerModelDefinition.FromVanillaChild("tail", "body", -4f, 15f, -1f, 2f, 0f, -1f, 4f, 9f, 5f, 30, 0,
                rotationX: -0.05235988f)
        ],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/fox/fox",
        textureSelector: state => state.IsSleeping ? "entity/fox/fox_sleep" : "entity/fox/fox",
        additionalTexturePaths: ["entity/fox/fox_sleep"]);

    /// <summary>Fox type names: RED and SNOW, matching the synced integer type id order.</summary>
    public static readonly IReadOnlyList<string> VariantKeys = ["red", "snow"];

    public static readonly IReadOnlyDictionary<string, EntityModelDefinition> Variants =
        new Dictionary<string, EntityModelDefinition>(StringComparer.Ordinal)
        {
            ["red"] = Definition,
            ["snow"] = new(
                "fox_snow",
                48,
                32,
                Definition.Parts,
                PlayerModelDefinition.DefaultFaceTransforms,
                "entity/fox/fox_snow",
                textureSelector: state => state.IsSleeping ? "entity/fox/fox_snow_sleep" : "entity/fox/fox_snow",
                additionalTexturePaths: ["entity/fox/fox_snow_sleep"])
        };
}
