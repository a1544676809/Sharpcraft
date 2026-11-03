// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.World;

namespace Sharpcraft.Client.Render;

/// <summary>Baby strider/husk/snifflet/armadillo models.</summary>
public static class BabyModelDefinitionsOdd
{
    public static readonly EntityModelDefinition Strider = new(
        "baby_strider",
        32,
        32,
        [
            PlayerModelDefinition.FromVanilla("body", 0f, 16.75f, 0f, -3.5f, -3.75f, -4f, 7f, 7f, 8f, 0, 0),
            PlayerModelDefinition.FromVanilla("right_leg", -1.5f, 20f, 0f, -1f, 0f, -1f, 2f, 4f, 2f, 0, 24),
            PlayerModelDefinition.FromVanilla("left_leg", 1.5f, 20f, 0f, -1f, 0f, -1f, 2f, 4f, 2f, 8, 24),
            PlayerModelDefinition.FromVanillaChild("bristle0", "body", 0f, -4.25f, 2f, -3.5f, -2.5f, 0f, 7f, 3f, 0f, 0, 21),
            PlayerModelDefinition.FromVanillaChild("bristle1", "body", 0f, -4.25f, 0f, -3.5f, -2.5f, 0f, 7f, 3f, 0f, 0, 18),
            PlayerModelDefinition.FromVanillaChild("bristle2", "body", 0f, -4.25f, -2f, -3.5f, -2.5f, 0f, 7f, 3f, 0f, 0, 15)
        ],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/strider/strider_baby");

    public static readonly EntityModelDefinition Husk = new(
        "baby_husk",
        64,
        64,
        BabyModelDefinitionsHumanoid.Zombie.Parts,
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/zombie/husk_baby");

    public static readonly EntityModelDefinition Snifflet = new(
        "baby_sniffer",
        128,
        128,
        [
            PlayerModelDefinition.FromVanilla("bone", 0f, 24f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0),
            PlayerModelDefinition.FromVanillaChild("body", "bone", 6f, -3f, -9.5f, -13f, -14f, -0.5f, 14f, 14f, 20f, 0, 35, grow: 0.25f),
            PlayerModelDefinition.FromVanillaChild("body_main", "body", 0f, 0f, 0f, -13f, -14f, -0.5f, 14f, 15f, 20f, 0, 0),
            PlayerModelDefinition.FromVanillaChild("body_flat", "body", 0f, 0f, 0f, -13f, 0f, -0.5f, 14f, 0f, 20f, 68, 0),
            PlayerModelDefinition.FromVanillaChild("head", "body", -6f, -4.75f, 0f, -5f, -4.25f, -7.5f, 10f, 9f, 9f, 68, 20),
            PlayerModelDefinition.FromVanillaChild("head_flat", "head", 0f, 0f, 0f, -5f, 3.75f, -7.5f, 10f, 0f, 9f, 88, 20),
            PlayerModelDefinition.FromVanillaChild("left_ear", "head", 5f, -4.25f, -1.5f, 0f, 0f, -2f, 1f, 11f, 3f, 104, 38),
            PlayerModelDefinition.FromVanillaChild("right_ear", "head", -5f, -4.25f, -1.5f, -1f, 0f, -2f, 1f, 11f, 3f, 96, 38),
            PlayerModelDefinition.FromVanillaChild("nose", "head", 0f, -1.25f, -9.5f, -5f, -3f, -2f, 10f, 3f, 4f, 68, 47),
            PlayerModelDefinition.FromVanillaChild("lower_beak", "head", 0f, 1.25f, -9.5f, -5f, -2.5f, -2f, 10f, 5f, 4f, 68, 38),
            PlayerModelDefinition.FromVanillaChild("right_front_leg", "bone", -4f, -4f, -7f, -2f, -1f, -2f, 4f, 5f, 4f, 0, 69),
            PlayerModelDefinition.FromVanillaChild("right_mid_leg", "bone", -4f, -4f, 0f, -2f, -1f, -2f, 4f, 5f, 4f, 0, 78),
            PlayerModelDefinition.FromVanillaChild("right_hind_leg", "bone", -4f, -4f, 7f, -2f, -1f, -2f, 4f, 5f, 4f, 0, 87),
            PlayerModelDefinition.FromVanillaChild("left_front_leg", "bone", 4f, -4f, -7f, -2f, -1f, -2f, 4f, 5f, 4f, 16, 69),
            PlayerModelDefinition.FromVanillaChild("left_mid_leg", "bone", 4f, -4f, 0f, -2f, -1f, -2f, 4f, 5f, 4f, 16, 78),
            PlayerModelDefinition.FromVanillaChild("left_hind_leg", "bone", 4f, -4f, 7f, -2f, -1f, -2f, 4f, 5f, 4f, 16, 87)
        ],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/sniffer/snifflet");

    public static readonly EntityModelDefinition Armadillo = BuildArmadillo();

    public static readonly IReadOnlyDictionary<int, EntityModelDefinition> Models =
        new Dictionary<int, EntityModelDefinition>
        {
            [132] = Strider,
            [68] = Husk,
            [122] = Snifflet,
            [4] = Armadillo
        };

    private static bool Unrolled(EntityRenderState state) => !state.IsHidingInShell;

    private static bool Rolled(EntityRenderState state) => state.IsHidingInShell;

    private static EntityModelDefinition BuildArmadillo()
    {
        EntityModelPart[] parts =
        [
            PlayerModelDefinition.FromVanilla("body", 0f, 20f, 0.5f, -2.5f, -2f, -3.5f, 5f, 4f, 7f, 0, 0, grow: 0.3f) with { VisibleWhen = Unrolled },
            PlayerModelDefinition.FromVanillaChild("body_inner", "body", 0f, 0f, 0f, -2.5f, -2f, -3f, 5f, 4f, 6f, 0, 11) with { VisibleWhen = Unrolled },
            PlayerModelDefinition.FromVanillaChild("tail", "body", 0f, 0f, 3.4f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0) with { VisibleWhen = Unrolled },
            PlayerModelDefinition.FromVanillaChild("tail_cube", "tail", 0f, 1.5f, 1f, -0.5f, -0.5f, -2f, 1f, 1f, 4f, 22, 11, rotationX: -1.0472f) with { VisibleWhen = Unrolled },
            PlayerModelDefinition.FromVanillaChild("head", "body", 0f, 0f, -3.2f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0) with { VisibleWhen = Unrolled },
            PlayerModelDefinition.FromVanillaChild("head_cube", "head", 0f, 0f, 0f, -1f, -2f, -4f, 2f, 2f, 4f, 20, 17, rotationX: 0.7417649f),
            PlayerModelDefinition.FromVanillaChild("right_ear", "head_cube", -1f, -2f, -0.3f, -1.8f, -2f, 0f, 2f, 3f, 0f, 28, 8,
                mirror: true, rotationX: -0.4363f, rotationY: -0.1134f, rotationZ: 0.0524f),
            PlayerModelDefinition.FromVanillaChild("left_ear", "head_cube", 1f, -2f, -0.3f, -0.2f, -2f, 0f, 2f, 3f, 0f, 28, 8,
                rotationX: -0.4363f, rotationY: 0.1134f, rotationZ: -0.0524f),
            PlayerModelDefinition.FromVanilla("right_hind_leg", -1.5f, 22f, 2.5f, -1f, 0f, -1f, 2f, 2f, 2f, 20, 27) with { VisibleWhen = Unrolled },
            PlayerModelDefinition.FromVanilla("left_hind_leg", 1.5f, 22f, 2.5f, -1f, 0f, -1f, 2f, 2f, 2f, 20, 27) with { VisibleWhen = Unrolled },
            PlayerModelDefinition.FromVanilla("right_front_leg", 1.5f, 22f, -1.5f, -1f, 0f, -1f, 2f, 2f, 2f, 20, 23) with { VisibleWhen = Unrolled },
            PlayerModelDefinition.FromVanilla("left_front_leg", -1.5f, 22f, -1.5f, -1f, 0f, -1f, 2f, 2f, 2f, 24, 0) with { VisibleWhen = Unrolled },
            PlayerModelDefinition.FromVanilla("cube", 0f, 20.7f, 0.5f, -3f, -3f, -3f, 6f, 6f, 6f, 0, 25, grow: 0.3f) with { VisibleWhen = Rolled }
        ];
        return new EntityModelDefinition("baby_armadillo", 64, 64, parts, PlayerModelDefinition.DefaultFaceTransforms, "entity/armadillo/armadillo_baby");
    }
}
