// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// ShulkerModel: a base, a separate lid and a small head inside. The dye colour swaps the texture (16 colours plus the
/// default), so the shell is baked at bind pose with the lid closed onto the base.
/// </summary>
/// <remarks>
/// Bind pose only. The reference uses <c>entityCutoutZOffset</c>; the depth offset is not replicated. Peek/lid and head
/// animations are not ported.
/// </remarks>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.monster.shulker.ShulkerModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/shulker/ShulkerModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class ShulkerModelDefinition
{
    public const int ShulkerEntityTypeId = 115;

    private static readonly EntityModelPart[] Parts =
    [
        PlayerModelDefinition.FromVanilla("lid", 0f, 24f, 0f, -8f, -16f, -8f, 16f, 12f, 16f, 0, 0),
        PlayerModelDefinition.FromVanilla("base", 0f, 24f, 0f, -8f, -8f, -8f, 16f, 8f, 16f, 0, 28),
        PlayerModelDefinition.FromVanilla("head", 0f, 12f, 0f, -3f, 0f, -3f, 6f, 6f, 6f, 0, 52)
    ];

    /// <summary>Dye colour ids in registry order (0..15); 16 means "no colour" and uses the default texture.</summary>
    public static readonly IReadOnlyList<string> VariantKeys =
    [
        "white", "orange", "magenta", "light_blue", "yellow", "lime", "pink", "gray",
        "light_gray", "cyan", "purple", "blue", "brown", "green", "red", "black"
    ];

    public static readonly EntityModelDefinition Definition = new(
        "shulker",
        64,
        64,
        Parts,
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/shulker/shulker");

    public static readonly IReadOnlyDictionary<string, EntityModelDefinition> Variants = BuildVariants();

    private static Dictionary<string, EntityModelDefinition> BuildVariants()
    {
        var variants = new Dictionary<string, EntityModelDefinition>(StringComparer.Ordinal);
        foreach (var key in VariantKeys)
        {
            variants[key] = new EntityModelDefinition(
                "shulker_" + key,
                64,
                64,
                Parts,
                PlayerModelDefinition.DefaultFaceTransforms,
                "entity/shulker/shulker_" + key);
        }

        return variants;
    }
}
