// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// SlimeModel's inner cube/eyes/mouth plus the separate outer cube (same texture, 8x8x8 at UV 0,0), matching
/// SlimeModel.createInnerBodyLayer and createOuterBodyLayer.
/// </summary>
public static class SlimeModelDefinition
{
    public const int SlimeEntityTypeId = 120;

    public static readonly EntityModelDefinition Definition = new(
        "slime",
        64,
        32,
        [
            new("cube", 0f, 1f, 6f, 6f, 6f, 0, 16),
            new("right_eye", -2.25f, 4f, 2f, 2f, 2f, 32, 0, false, null, -2.5f),
            new("left_eye", 2.25f, 4f, 2f, 2f, 2f, 32, 4, false, null, -2.5f),
            new("mouth", 0.5f, 2f, 1f, 1f, 1f, 32, 8, false, null, -3f),
            new("outer_cube", 0f, 0f, 8f, 8f, 8f, 0, 0)
        ],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/slime/slime");
}
