// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>Baby squid/bee/dolphin/axolotl models.</summary>
public static class BabyModelDefinitionsAquatic
{
    public static readonly EntityModelDefinition Squid = new(
        "baby_squid",
        32,
        32,
        BuildSquidParts(),
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/squid/squid_baby");

    public static readonly EntityModelDefinition Bee = new(
        "baby_bee",
        32,
        32,
        [
            PlayerModelDefinition.FromVanilla("bone", 0f, 19.6667f, -1.8567f, 1f, -1.6667f, -2.1633f, 1f, 2f, 2f, 6, 12),
            PlayerModelDefinition.FromVanillaChild("antenna_left", "bone", 0f, 0f, 0f, -2f, -1.6667f, -2.1933f, 1f, 2f, 2f, 0, 12),
            PlayerModelDefinition.FromVanillaChild("body", "bone", 0f, 1.3333f, 2.3567f, -2f, -2f, -2.5f, 4f, 4f, 5f, 0, 0),
            PlayerModelDefinition.FromVanillaChild("stinger", "body", 0f, 0.5f, 2.5f, 0f, -0.5f, 0f, 0f, 1f, 1f, 13, 2),
            PlayerModelDefinition.FromVanillaChild("right_wing", "bone", -1f, -0.6667f, 0.8567f, -3f, 0f, 0f, 3f, 0f, 3f, 3, 9,
                rotationX: 0.2182f, rotationY: 0.3491f),
            PlayerModelDefinition.FromVanillaChild("left_wing", "bone", 1f, -0.6667f, 0.8567f, 0f, 0f, 0f, 3f, 0f, 3f, -3, 9,
                mirror: true, rotationX: 0.2182f, rotationY: -0.3491f),
            PlayerModelDefinition.FromVanillaChild("front_legs", "bone", 0f, 3.3333f, 1.8567f, -1.5f, 0f, 0f, 3f, 1f, 0f, 13, 0),
            PlayerModelDefinition.FromVanillaChild("middle_legs", "bone", 0f, 3.3333f, 2.8567f, -1.5f, 0f, 0f, 3f, 1f, 0f, 13, 1),
            PlayerModelDefinition.FromVanillaChild("back_legs", "bone", 0f, 3.3333f, 3.8567f, -1.5f, 0f, 0f, 3f, 1f, 0f, 13, 2)
        ],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/bee/bee_baby",
        textureSelector: state => state.IsAngry
            ? (state.HasNectar ? "entity/bee/bee_angry_nectar_baby" : "entity/bee/bee_angry_baby")
            : (state.HasNectar ? "entity/bee/bee_nectar_baby" : "entity/bee/bee_baby"),
        additionalTexturePaths:
        [
            "entity/bee/bee_angry_baby", "entity/bee/bee_nectar_baby", "entity/bee/bee_angry_nectar_baby"
        ]);

    public static readonly EntityModelDefinition Dolphin = new(
        "baby_dolphin",
        64,
        64,
        [
            PlayerModelDefinition.FromVanilla("body", 0f, 21.5f, 0f, -3f, -2.5f, -4f, 6f, 5f, 8f, 20, 0),
            PlayerModelDefinition.FromVanillaChild("head", "body", 0f, 1f, -4f, -3f, -3.5f, -4f, 6f, 5f, 4f, 0, 0),
            PlayerModelDefinition.FromVanillaChild("nose", "head", 0f, 0.5f, -4f, -1f, -1f, -2f, 2f, 2f, 2f, 0, 9),
            PlayerModelDefinition.FromVanillaChild("left_fin", "body", 1.8f, 0.85f, -2.6f, -0.5f, -1.5f, -0.5f, 1f, 3f, 6f, 34, 18,
                rotationX: 0.8727f, rotationZ: 1.7017f),
            PlayerModelDefinition.FromVanillaChild("right_fin", "body", -1.8f, 0.85f, -2.6f, -0.5f, -1.5f, -0.5f, 1f, 3f, 6f, 48, 18,
                mirror: true, rotationX: 0.8727f, rotationZ: -1.7017f),
            PlayerModelDefinition.FromVanillaChild("tail", "body", 0f, 1f, 4f, -2f, -1.5f, 0f, 4f, 3f, 7f, 0, 13),
            PlayerModelDefinition.FromVanillaChild("tail_fin", "tail", 0f, 0f, 6f, -4f, -0.5f, -1f, 8f, 1f, 4f, 22, 13),
            PlayerModelDefinition.FromVanillaChild("back_fin", "body", 0f, -1f, -2.7f, -0.5f, -1f, 1f, 1f, 3f, 4f, 42, 0,
                rotationX: 0.8727f)
        ],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/dolphin/dolphin_baby");

    public static readonly EntityModelDefinition Axolotl = new(
        "baby_axolotl",
        32,
        32,
        [
            PlayerModelDefinition.FromVanilla("root", 0f, 24f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0),
            PlayerModelDefinition.FromVanillaChild("body", "root", 0f, -1.25f, 1.75f, -2f, -0.75f, -2.75f, 4f, 2f, 6f, 0, 0),
            PlayerModelDefinition.FromVanillaChild("body_fin", "body", 0f, 0f, 0f, 0f, -1.75f, -2.75f, 0f, 3f, 5f, 0, 12),
            PlayerModelDefinition.FromVanillaChild("right_front_leg", "body", -2f, 0.25f, -1.25f, -3f, 0f, -0.5f, 3f, 0f, 1f, 20, 16),
            PlayerModelDefinition.FromVanillaChild("right_hind_leg", "body", -2f, 0.25f, 1.75f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0,
                rotationY: 1.5708f, rotationZ: 1.5708f),
            PlayerModelDefinition.FromVanillaChild("right_leg_r1", "right_hind_leg", 0f, 0f, 0f, 0f, 0f, -0.5f, 3f, 0f, 1f, 20, 14,
                rotationX: -1.5708f, rotationZ: 1.5708f),
            PlayerModelDefinition.FromVanillaChild("left_front_leg", "body", 2f, 0.25f, -1.25f, 0f, 0f, -0.5f, 3f, 0f, 1f, 20, 13),
            PlayerModelDefinition.FromVanillaChild("left_hind_leg", "body", 2f, 0.25f, 1.75f, 0f, 0f, -0.5f, 3f, 0f, 1f, 20, 14),
            PlayerModelDefinition.FromVanillaChild("tail", "body", 0f, -0.25f, 3.25f, 0f, -1.5f, -1f, 0f, 3f, 8f, 10, 9),
            PlayerModelDefinition.FromVanillaChild("head", "body", 0f, 0.25f, -2.75f, -3f, -2f, -4f, 6f, 3f, 4f, 0, 8),
            PlayerModelDefinition.FromVanillaChild("left_gills", "head", 3f, -0.5f, -2f, 0f, -3.5f, 0f, 3f, 5f, 0f, 20, 8),
            PlayerModelDefinition.FromVanillaChild("right_gills", "head", -3f, -0.5f, -2f, -3f, -3.5f, 0f, 3f, 5f, 0f, 20, 3),
            PlayerModelDefinition.FromVanillaChild("top_gills", "head", 0f, -2f, -2f, -3f, -3f, 0f, 6f, 3f, 0f, 20, 0)
        ],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/axolotl/axolotl_lucy_baby");

    public static readonly IReadOnlyDictionary<int, EntityModelDefinition> Models =
        new Dictionary<int, EntityModelDefinition>
        {
            [130] = Squid,
            [11] = Bee,
            [36] = Dolphin,
            [7] = Axolotl
        };

    public static readonly IReadOnlyDictionary<(int TypeId, string Key), EntityModelDefinition> Variants = BuildVariants();

    private static EntityModelPart[] BuildSquidParts()
    {
        var parts = new List<EntityModelPart>
        {
            PlayerModelDefinition.FromVanilla("body", 0f, 13f, 0f, -4f, -5f, -4f, 8f, 10f, 8f, 0, 0)
        };

        for (var i = 0; i < 8; i++)
        {
            var angle = i * MathF.PI * 2f / 8f;
            var x = MathF.Cos(angle) * 3f;
            var z = MathF.Sin(angle) * 3f;
            var yRot = i * MathF.PI * -2f / 8f + (MathF.PI / 2f);
            parts.Add(PlayerModelDefinition.FromVanilla("tentacle" + i, x, 18.5f, z, -1f, -0.5f, -1f, 2f, 6f, 2f, 0, 18, rotationY: -yRot));
        }

        return [.. parts];
    }

    private static Dictionary<(int, string), EntityModelDefinition> BuildVariants()
    {
        var variants = new Dictionary<(int, string), EntityModelDefinition>
        {
            [(7, "lucy")] = Axolotl
        };
        foreach (var key in AxolotlModelDefinition.VariantKeys)
        {
            if (key != "lucy")
            {
                variants[(7, key)] = new("baby_axolotl_" + key, 32, 32, Axolotl.Parts, PlayerModelDefinition.DefaultFaceTransforms, "entity/axolotl/axolotl_" + key + "_baby");
            }
        }

        return variants;
    }
}
