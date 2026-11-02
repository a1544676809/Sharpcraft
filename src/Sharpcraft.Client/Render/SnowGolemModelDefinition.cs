// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// SnowGolemModel: head, upper/lower body and two laterally rotated arms. The reference uses -0.5 cube deformation.
/// </summary>
public static class SnowGolemModelDefinition
{
    public const int SnowGolemEntityTypeId = 124;

    public static readonly EntityModelDefinition Definition = new(
        "snow_golem",
        64,
        64,
        [
            PlayerModelDefinition.FromVanilla("head", 0f, 4f, 0f, -4f, -8f, -4f, 8f, 8f, 8f, 0, 0, grow: -0.5f),
            PlayerModelDefinition.FromVanilla("left_arm", 5f, 6f, 1f, -1f, 0f, -1f, 12f, 2f, 2f, 32, 0,
                grow: -0.5f, rotationZ: -1f),
            PlayerModelDefinition.FromVanilla("right_arm", -5f, 6f, -1f, -1f, 0f, -1f, 12f, 2f, 2f, 32, 0,
                grow: -0.5f, rotationY: MathF.PI, rotationZ: 1f),
            PlayerModelDefinition.FromVanilla("upper_body", 0f, 13f, 0f, -5f, -10f, -5f, 10f, 10f, 10f, 0, 16, grow: -0.5f),
            PlayerModelDefinition.FromVanilla("lower_body", 0f, 24f, 0f, -6f, -12f, -6f, 12f, 12f, 12f, 0, 36, grow: -0.5f)
        ],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/snow_golem/snow_golem");
}
