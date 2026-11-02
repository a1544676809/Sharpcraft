// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// MagmaCubeModel: eight stacked segment cubes plus the inside cube; all are flat parts with fixed texture offsets.
/// </summary>
public static class MagmaCubeModelDefinition
{
    public const int MagmaCubeEntityTypeId = 81;

    public static readonly EntityModelDefinition Definition = new(
        "magma_cube",
        64,
        64,
        [
            new("cube0", 0f, 7f, 8f, 1f, 8f, 0, 0),
            new("cube1", 0f, 6f, 8f, 1f, 8f, 0, 9),
            new("cube2", 0f, 5f, 8f, 1f, 8f, 0, 18),
            new("cube3", 0f, 4f, 8f, 1f, 8f, 0, 27),
            new("cube4", 0f, 3f, 8f, 1f, 8f, 32, 0),
            new("cube5", 0f, 2f, 8f, 1f, 8f, 32, 9),
            new("cube6", 0f, 1f, 8f, 1f, 8f, 32, 18),
            new("cube7", 0f, 0f, 8f, 1f, 8f, 32, 27),
            new("inside_cube", 0f, 2f, 4f, 4f, 4f, 24, 40)
        ],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/slime/magmacube");
}
