// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// ShulkerModel: a base, a separate lid and a small head inside. The dye colour swaps the texture (16 colours plus the
/// default), so the shell is baked at bind pose with the lid closed onto the base. Parts are declared with the
/// <c>VanillaMeshDefinition</c> DSL and baked via <c>VanillaMeshBake</c> (T-688).
/// </summary>
/// <remarks>
/// Bind pose only. The reference uses <c>entityCutoutZOffset</c>; the depth offset is not replicated. Peek/lid and head
/// animations are not ported.
/// </remarks>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.monster.shulker.ShulkerModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/shulker/ShulkerModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class ShulkerModelDefinition
{
    public const int ShulkerEntityTypeId = 115;

    private static readonly VanillaMeshDefinition ShulkerMesh = BuildShulkerMesh();

    private static readonly EntityModelPart[] Parts = [.. VanillaMeshBake.Bake(ShulkerMesh, 0, 0)];

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

    private static VanillaMeshDefinition BuildShulkerMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        root.AddOrReplaceChild("lid", VanillaPartPose.Offset(0f, 24f, 0f),
            new VanillaCubeDefinition("lid", 0, 0, -8f, -16f, -8f, 16f, 12f, 16f));
        root.AddOrReplaceChild("base", VanillaPartPose.Offset(0f, 24f, 0f),
            new VanillaCubeDefinition("base", 0, 28, -8f, -8f, -8f, 16f, 8f, 16f));
        root.AddOrReplaceChild("head", VanillaPartPose.Offset(0f, 12f, 0f),
            new VanillaCubeDefinition("head", 0, 52, -3f, 0f, -3f, 6f, 6f, 6f));
        return new VanillaMeshDefinition(root);
    }
}
