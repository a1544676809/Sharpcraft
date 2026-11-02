// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// SlimeModel's inner cube/eyes/mouth plus the separate outer cube, expressed directly in vanilla cube coordinates.
/// </summary>
public static class SlimeModelDefinition
{
    public const int SlimeEntityTypeId = 120;

    public static readonly EntityModelDefinition Definition = new(
        "slime",
        64,
        32,
        [
            PlayerModelDefinition.FromVanilla("cube", 0f, 0f, 0f, -3f, 17f, -3f, 6f, 6f, 6f, 0, 16),
            PlayerModelDefinition.FromVanilla("right_eye", 0f, 0f, 0f, -3.25f, 18f, -3.5f, 2f, 2f, 2f, 32, 0),
            PlayerModelDefinition.FromVanilla("left_eye", 0f, 0f, 0f, 1.25f, 18f, -3.5f, 2f, 2f, 2f, 32, 4),
            PlayerModelDefinition.FromVanilla("mouth", 0f, 0f, 0f, 0f, 21f, -3.5f, 1f, 1f, 1f, 32, 8),
            PlayerModelDefinition.FromVanilla("outer_cube", 0f, 0f, 0f, -4f, 16f, -4f, 8f, 8f, 8f, 0, 0)
        ],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/slime/slime");
}
