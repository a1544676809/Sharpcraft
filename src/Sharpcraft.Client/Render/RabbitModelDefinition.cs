// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// AdultRabbitModel: body with tail, head with ears, front legs and hind haunches, all through the reference's nested
/// pivots (body → head → ears, body → frontlegs, body → backlegs → hind legs → haunch).
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.animal.rabbit.RabbitModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/rabbit/RabbitModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class RabbitModelDefinition
{
    public const int RabbitEntityTypeId = 111;

    public static readonly EntityModelDefinition Definition = new(
        "rabbit",
        64,
        64,
        [
            PlayerModelDefinition.FromVanilla("body", 0f, 23f, 4f, -4f, -6f, -9f, 8f, 6f, 10f, 0, 0, rotationX: 0.3927f),
            PlayerModelDefinition.FromVanillaChild("tail", "body", 0f, -4.9916f, 0.0125f, -2f, -3.0084f, -1.0125f, 4f, 4f, 4f, 20, 16),
            PlayerModelDefinition.FromVanillaChild("head", "body", 0f, -5.2929f, -8.1213f, -2.5f, -3f, -4f, 5f, 5f, 5f, 0, 16,
                rotationX: 0.3927f),
            PlayerModelDefinition.FromVanillaChild("left_ear", "head", 1.5f, -3.7071f, -0.8787f, -1f, -4.2929f, -0.1213f, 2f, 5f, 1f, 32, 0),
            PlayerModelDefinition.FromVanillaChild("right_ear", "head", -1.5f, -3.7071f, -0.8787f, -1f, -4.2929f, -0.1213f, 2f, 5f, 1f, 26, 0),
            PlayerModelDefinition.FromVanillaChild("frontlegs", "body", 0f, -1.5349f, -6.3108f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0),
            PlayerModelDefinition.FromVanillaChild("right_front_leg", "frontlegs", -2f, 1.9239f, 0.3827f, -0.9f, -1f, -0.9f, 2f, 4f, 2f, 36, 18,
                rotationX: 0.3927f),
            PlayerModelDefinition.FromVanillaChild("left_front_leg", "frontlegs", 2f, 1.9239f, 0.4827f, -1f, -1f, -1f, 2f, 4f, 2f, 44, 18,
                rotationX: 0.3927f),
            PlayerModelDefinition.FromVanilla("backlegs", 0f, 23f, 4f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0),
            PlayerModelDefinition.FromVanillaChild("right_hind_leg", "backlegs", -3f, 0.5f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0),
            PlayerModelDefinition.FromVanillaChild("right_haunch", "right_hind_leg", 0f, -0.5f, 0f, -1f, 0f, -5f, 2f, 1f, 6f, 20, 24,
                rotationY: 0.3927f),
            PlayerModelDefinition.FromVanillaChild("left_hind_leg", "backlegs", 3f, 0.5f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0),
            PlayerModelDefinition.FromVanillaChild("left_haunch", "left_hind_leg", 0f, -0.5f, 0f, -1f, 0f, -5f, 2f, 1f, 6f, 36, 24,
                rotationY: -0.3927f)
        ],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/rabbit/rabbit_brown");

    public static readonly IReadOnlyList<string> VariantKeys = ["brown", "white", "black", "white_splotched", "gold", "salt", "evil"];

    public static readonly IReadOnlyDictionary<string, EntityModelDefinition> Variants = BuildVariants();

    private static Dictionary<string, EntityModelDefinition> BuildVariants()
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["brown"] = "rabbit_brown",
            ["white"] = "rabbit_white",
            ["black"] = "rabbit_black",
            ["white_splotched"] = "rabbit_white_splotched",
            ["gold"] = "rabbit_gold",
            ["salt"] = "rabbit_salt",
            ["evil"] = "rabbit_caerbannog"
        };
        var variants = new Dictionary<string, EntityModelDefinition>(StringComparer.Ordinal)
        {
            ["brown"] = Definition
        };
        foreach (var key in VariantKeys)
        {
            if (key != "brown")
            {
                variants[key] = new EntityModelDefinition("rabbit_" + key, 64, 64, Definition.Parts, PlayerModelDefinition.DefaultFaceTransforms, "entity/rabbit/" + files[key]);
            }
        }

        return variants;
    }
}
