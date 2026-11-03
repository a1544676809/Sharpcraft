// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// PhantomModel: body with head, a two-segment tail and two two-segment wings. The eyes layer uses phantom_eyes.
/// Bind pose only; wing flap and tail sway are not ported.
/// </summary>
public static class PhantomModelDefinition
{
    public const int PhantomEntityTypeId = 100;

    public static readonly EntityModelDefinition Definition = new(
        "phantom",
        64,
        64,
        [
            PlayerModelDefinition.FromVanilla("body", 0f, 0f, 0f, -3f, -2f, -8f, 5f, 3f, 9f, 0, 8, rotationX: 0.1f),
            PlayerModelDefinition.FromVanillaChild("head", "body", 0f, 1f, -7f, -4f, -2f, -5f, 7f, 3f, 5f, 0, 0, rotationX: 0.2f),
            PlayerModelDefinition.FromVanillaChild("tail_base", "body", 0f, -2f, 1f, -2f, 0f, 0f, 3f, 2f, 6f, 3, 20),
            PlayerModelDefinition.FromVanillaChild("tail_tip", "tail_base", 0f, 0.5f, 6f, -1f, 0f, 0f, 1f, 1f, 6f, 4, 29),
            PlayerModelDefinition.FromVanillaChild("left_wing_base", "body", 2f, -2f, -8f, 0f, 0f, 0f, 6f, 2f, 9f, 23, 12, rotationZ: 0.1f),
            PlayerModelDefinition.FromVanillaChild("left_wing_tip", "left_wing_base", 6f, 0f, 0f, 0f, 0f, 0f, 13f, 1f, 9f, 16, 24, rotationZ: 0.1f),
            PlayerModelDefinition.FromVanillaChild("right_wing_base", "body", -3f, -2f, -8f, -6f, 0f, 0f, 6f, 2f, 9f, 23, 12, mirror: true, rotationZ: -0.1f),
            PlayerModelDefinition.FromVanillaChild("right_wing_tip", "right_wing_base", -6f, 0f, 0f, -13f, 0f, 0f, 13f, 1f, 9f, 16, 24, mirror: true, rotationZ: -0.1f)
        ],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/phantom/phantom",
        1f,
        "entity/phantom/phantom_eyes");
}
