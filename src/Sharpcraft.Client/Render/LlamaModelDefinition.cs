// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// LlamaModel: head/neck/ears, rotated body, two chest boxes and four 4x14x4 legs.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.animal.llama.LlamaModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/llama/LlamaModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class LlamaModelDefinition
{
    public const int LlamaEntityTypeId = 79;

    public static readonly EntityModelDefinition Definition = new(
        "llama",
        128,
        64,
        [
            PlayerModelDefinition.FromVanilla("head", 0f, 7f, -6f, -2f, -14f, -10f, 4f, 4f, 9f, 0, 0),
            PlayerModelDefinition.FromVanilla("neck", 0f, 7f, -6f, -4f, -16f, -6f, 8f, 18f, 6f, 0, 14),
            PlayerModelDefinition.FromVanilla("left_ear", 0f, 7f, -6f, -4f, -19f, -4f, 3f, 3f, 2f, 17, 0),
            PlayerModelDefinition.FromVanilla("right_ear", 0f, 7f, -6f, 1f, -19f, -4f, 3f, 3f, 2f, 17, 0),
            PlayerModelDefinition.FromVanilla("body", 0f, 5f, 2f, -6f, -10f, -7f, 12f, 18f, 10f, 29, 0, rotationX: -MathF.PI / 2f),
            PlayerModelDefinition.FromVanilla("right_chest", -8.5f, 3f, 3f, -3f, 0f, 0f, 8f, 8f, 3f, 45, 28, rotationY: -MathF.PI / 2f),
            PlayerModelDefinition.FromVanilla("left_chest", 5.5f, 3f, 3f, -3f, 0f, 0f, 8f, 8f, 3f, 45, 41, rotationY: -MathF.PI / 2f),
            PlayerModelDefinition.FromVanilla("right_hind_leg", -3.5f, 10f, 6f, -2f, 0f, -2f, 4f, 14f, 4f, 29, 29),
            PlayerModelDefinition.FromVanilla("left_hind_leg", 3.5f, 10f, 6f, -2f, 0f, -2f, 4f, 14f, 4f, 29, 29),
            PlayerModelDefinition.FromVanilla("right_front_leg", -3.5f, 10f, -5f, -2f, 0f, -2f, 4f, 14f, 4f, 29, 29),
            PlayerModelDefinition.FromVanilla("left_front_leg", 3.5f, 10f, -5f, -2f, 0f, -2f, 4f, 14f, 4f, 29, 29)
        ],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/llama/llama_brown");

    /// <summary>Llama coat ids, in the <c>Llama.Variant</c> enum order used on the wire.</summary>
    public static readonly IReadOnlyList<string> VariantKeys = ["creamy", "white", "brown", "gray"];

    /// <summary>Coat key to definition; the geometry is identical, only the texture changes.</summary>
    public static readonly IReadOnlyDictionary<string, EntityModelDefinition> Variants = BuildVariants();

    private static Dictionary<string, EntityModelDefinition> BuildVariants()
    {
        var variants = new Dictionary<string, EntityModelDefinition>(StringComparer.Ordinal)
        {
            ["brown"] = Definition
        };

        foreach (var key in VariantKeys)
        {
            if (key == "brown")
            {
                continue;
            }

            variants[key] = new EntityModelDefinition(
                "llama_" + key,
                128,
                64,
                Definition.Parts,
                PlayerModelDefinition.DefaultFaceTransforms,
                "entity/llama/llama_" + key);
        }

        return variants;
    }
}
