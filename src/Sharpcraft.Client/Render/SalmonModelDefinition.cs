// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// SalmonModel: two body segments with fins and a head. Three synced size variants scale the same geometry 0.5/1.0/1.5.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.animal.fish.SalmonModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/fish/SalmonModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class SalmonModelDefinition
{
    public const int SalmonEntityTypeId = 113;

    private static readonly EntityModelPart[] Parts =
    [
        PlayerModelDefinition.FromVanilla("body_front", 0f, 20f, -7.2f, -1.5f, -2.5f, 0f, 3f, 5f, 8f, 0, 0),
        PlayerModelDefinition.FromVanilla("body_back", 0f, 20f, 0.8f, -1.5f, -2.5f, 0f, 3f, 5f, 8f, 0, 13),
        PlayerModelDefinition.FromVanilla("head", 0f, 20f, -7.2f, -1f, -2f, -3f, 2f, 4f, 3f, 22, 0),
        PlayerModelDefinition.FromVanillaChild("back_fin", "body_back", 0f, 0f, 8f, 0f, -2.5f, 0f, 0f, 5f, 6f, 20, 10),
        PlayerModelDefinition.FromVanillaChild("top_front_fin", "body_front", 0f, -4.5f, 5f, 0f, 0f, 0f, 0f, 2f, 3f, 2, 1),
        PlayerModelDefinition.FromVanillaChild("top_back_fin", "body_back", 0f, -4.5f, -1f, 0f, 0f, 0f, 0f, 2f, 4f, 0, 2),
        PlayerModelDefinition.FromVanilla("right_fin", -1.5f, 21.5f, -7.2f, -2f, 0f, 0f, 2f, 0f, 2f, 28, 0,
            rotationZ: MathF.PI / 4f),
        PlayerModelDefinition.FromVanilla("left_fin", 1.5f, 21.5f, -7.2f, 0f, 0f, 0f, 2f, 0f, 2f, 0, 0,
            rotationZ: -MathF.PI / 4f)
    ];

    private static readonly float[] VariantScale = [0.5f, 1f, 1.5f];

    /// <summary>Synced variant ids: SMALL, MEDIUM, LARGE.</summary>
    public static readonly IReadOnlyList<string> VariantKeys = ["small", "medium", "large"];

    public static readonly EntityModelDefinition Definition = new(
        "salmon",
        32,
        32,
        Parts,
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/fish/salmon");

    public static readonly IReadOnlyDictionary<string, EntityModelDefinition> Variants = BuildVariants();

    private static Dictionary<string, EntityModelDefinition> BuildVariants()
    {
        var variants = new Dictionary<string, EntityModelDefinition>(StringComparer.Ordinal);
        for (var i = 0; i < VariantKeys.Count; i++)
        {
            var key = VariantKeys[i];
            variants[key] = key == "medium"
                ? Definition
                : new EntityModelDefinition(
                    "salmon_" + key,
                    32,
                    32,
                    Parts,
                    PlayerModelDefinition.DefaultFaceTransforms,
                    "entity/fish/salmon",
                    VariantScale[i]);
        }

        return variants;
    }
}
