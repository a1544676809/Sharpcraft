// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.World;

namespace Sharpcraft.Client.Render;

/// <summary>
/// AdultArmadilloModel: grown body shell with head/ears/tail, four small legs, and a separate rolled-up cube.
/// The body/legs/tail hide while the armadillo is in its shell and the cube replaces them.
/// </summary>
public static class ArmadilloModelDefinition
{
    public const int ArmadilloEntityTypeId = 4;

    private static bool Unrolled(EntityRenderState state) => !state.IsHidingInShell;

    private static bool Rolled(EntityRenderState state) => state.IsHidingInShell;

    public static readonly EntityModelDefinition Definition = new(
        "armadillo",
        64,
        64,
        [
            PlayerModelDefinition.FromVanilla("body", 0f, 21f, 4f, -4f, -7f, -10f, 8f, 8f, 12f, 0, 40) with { VisibleWhen = Unrolled },
            PlayerModelDefinition.FromVanillaChild("body_outer", "body", 0f, 0f, 0f, -4f, -7f, -10f, 8f, 8f, 12f, 0, 20, grow: 0.3f) with { VisibleWhen = Unrolled },
            PlayerModelDefinition.FromVanillaChild("tail", "body", 0f, -3f, 1f, -0.5f, -0.0865f, 0.0933f, 1f, 6f, 1f, 44, 53, rotationX: 0.5061f) with { VisibleWhen = Unrolled },
            PlayerModelDefinition.FromVanillaChild("head", "body", 0f, -2f, -11f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0) with { VisibleWhen = Unrolled },
            PlayerModelDefinition.FromVanillaChild("head_cube", "head", 0f, 0f, 0f, -1.5f, -1f, -1f, 3f, 5f, 2f, 43, 15, rotationX: -0.3927f),
            PlayerModelDefinition.FromVanillaChild("right_ear", "head", -1f, -1f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0) with { VisibleWhen = Unrolled },
            PlayerModelDefinition.FromVanillaChild("right_ear_cube", "right_ear", -0.5f, 0f, -0.6f, -2f, -3f, 0f, 2f, 5f, 0f, 43, 10,
                rotationX: 0.1886f, rotationY: -0.3864f, rotationZ: -0.0718f),
            PlayerModelDefinition.FromVanillaChild("left_ear", "head", 1f, -2f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0) with { VisibleWhen = Unrolled },
            PlayerModelDefinition.FromVanillaChild("left_ear_cube", "left_ear", 0.5f, 1f, -0.6f, 0f, -3f, 0f, 2f, 5f, 0f, 47, 10,
                rotationX: 0.1886f, rotationY: 0.3864f, rotationZ: 0.0718f),
            PlayerModelDefinition.FromVanilla("right_hind_leg", -2f, 21f, 4f, -1f, 0f, -1f, 2f, 3f, 2f, 51, 31) with { VisibleWhen = Unrolled },
            PlayerModelDefinition.FromVanilla("left_hind_leg", 2f, 21f, 4f, -1f, 0f, -1f, 2f, 3f, 2f, 42, 31) with { VisibleWhen = Unrolled },
            PlayerModelDefinition.FromVanilla("right_front_leg", -2f, 21f, -4f, -1f, 0f, -1f, 2f, 3f, 2f, 51, 43) with { VisibleWhen = Unrolled },
            PlayerModelDefinition.FromVanilla("left_front_leg", 2f, 21f, -4f, -1f, 0f, -1f, 2f, 3f, 2f, 42, 43) with { VisibleWhen = Unrolled },
            PlayerModelDefinition.FromVanilla("cube", 0f, 24f, 0f, -5f, -10f, -6f, 10f, 10f, 10f, 0, 0) with { VisibleWhen = Rolled }
        ],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/armadillo/armadillo");
}
