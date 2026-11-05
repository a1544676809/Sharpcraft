// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// PandaModel: head with nose/ears, one large rotated body cube, and four 6x9x6 legs.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.animal.panda.PandaModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/panda/PandaModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class PandaModelDefinition
{
    public const int PandaEntityTypeId = 97;

    public static readonly EntityModelDefinition Definition = new(
        "panda",
        64,
        64,
        [
            PlayerModelDefinition.FromVanilla("head", 0f, 11.5f, -17f, -6.5f, -5f, -4f, 13f, 10f, 9f, 0, 6),
            PlayerModelDefinition.FromVanilla("nose", 0f, 11.5f, -17f, -3.5f, 0f, -6f, 7f, 5f, 2f, 45, 16),
            PlayerModelDefinition.FromVanilla("left_ear", 0f, 11.5f, -17f, 3.5f, -8f, -1f, 5f, 4f, 1f, 52, 25),
            PlayerModelDefinition.FromVanilla("right_ear", 0f, 11.5f, -17f, -8.5f, -8f, -1f, 5f, 4f, 1f, 52, 25),
            PlayerModelDefinition.FromVanilla("body", 0f, 10f, 0f, -9.5f, -13f, -6.5f, 19f, 26f, 13f, 0, 25, rotationX: -MathF.PI / 2f),
            PlayerModelDefinition.FromVanilla("right_hind_leg", -5.5f, 15f, 9f, -3f, 0f, -3f, 6f, 9f, 6f, 40, 0),
            PlayerModelDefinition.FromVanilla("left_hind_leg", 5.5f, 15f, 9f, -3f, 0f, -3f, 6f, 9f, 6f, 40, 0),
            PlayerModelDefinition.FromVanilla("right_front_leg", -5.5f, 15f, -9f, -3f, 0f, -3f, 6f, 9f, 6f, 40, 0),
            PlayerModelDefinition.FromVanilla("left_front_leg", 5.5f, 15f, -9f, -3f, 0f, -3f, 6f, 9f, 6f, 40, 0)
        ],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/panda/panda");

    public static readonly IReadOnlyList<string> VariantKeys = ["normal", "lazy", "worried", "playful", "brown", "weak", "aggressive"];

    public static readonly IReadOnlyDictionary<string, EntityModelDefinition> Variants = BuildVariants();

    private static Dictionary<string, EntityModelDefinition> BuildVariants()
    {
        var variants = new Dictionary<string, EntityModelDefinition>(StringComparer.Ordinal)
        {
            ["normal"] = Definition
        };
        foreach (var key in VariantKeys)
        {
            if (key != "normal")
            {
                variants[key] = new EntityModelDefinition(
                    "panda_" + key,
                    64,
                    64,
                    Definition.Parts,
                    PlayerModelDefinition.DefaultFaceTransforms,
                    "entity/panda/panda_" + key);
            }
        }

        return variants;
    }
}
