// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// AdultWolfModel: real head with ears/snout, body + upper body, four 2x8x2 legs, and the real tail.
/// </summary>
public static class WolfModelDefinition
{
    public const int WolfEntityTypeId = 152;

    public static readonly EntityModelDefinition Definition = new(
        "wolf",
        64,
        64,
        [
            PlayerModelDefinition.FromVanilla("head", -1f, 13.5f, -7f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0),
            PlayerModelDefinition.FromVanillaChild("real_head", "head", 0f, 0f, 0f, -2f, -3f, -2f, 6f, 6f, 4f, 0, 0),
            PlayerModelDefinition.FromVanillaChild("right_ear", "head", 0f, 0f, 0f, -2f, -5f, 0f, 2f, 2f, 1f, 16, 14),
            PlayerModelDefinition.FromVanillaChild("left_ear", "head", 0f, 0f, 0f, 2f, -5f, 0f, 2f, 2f, 1f, 16, 14),
            PlayerModelDefinition.FromVanillaChild("snout", "head", 0f, 0f, 0f, -0.5f, -0.001f, -5f, 3f, 3f, 4f, 0, 10),
            PlayerModelDefinition.FromVanilla("body", 0f, 14f, 2f, -3f, -2f, -3f, 6f, 9f, 6f, 18, 14, rotationX: -MathF.PI / 2f),
            PlayerModelDefinition.FromVanilla("upper_body", -1f, 14f, -3f, -3f, -3f, -3f, 8f, 6f, 7f, 21, 0, rotationX: -MathF.PI / 2f),
            PlayerModelDefinition.FromVanilla("right_hind_leg", -2.5f, 16f, 7f, 0f, 0f, -1f, 2f, 8f, 2f, 0, 18),
            PlayerModelDefinition.FromVanilla("left_hind_leg", 0.5f, 16f, 7f, 0f, 0f, -1f, 2f, 8f, 2f, 0, 18),
            PlayerModelDefinition.FromVanilla("right_front_leg", -2.5f, 16f, -4f, 0f, 0f, -1f, 2f, 8f, 2f, 0, 18),
            PlayerModelDefinition.FromVanilla("left_front_leg", 0.5f, 16f, -4f, 0f, 0f, -1f, 2f, 8f, 2f, 0, 18),
            PlayerModelDefinition.FromVanilla("tail", -1f, 12f, 8f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0,
                rotationX: -MathF.PI / 5f),
            PlayerModelDefinition.FromVanillaChild("real_tail", "tail", 0f, 0f, 0f, 0f, 0f, -1f, 2f, 8f, 2f, 9, 18)
        ],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/wolf/wolf");
}
