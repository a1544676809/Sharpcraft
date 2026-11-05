// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// MagmaCubeModel: eight stacked segment cubes plus the inside cube, all in raw model coordinates.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.monster.slime.MagmaCubeModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/slime/MagmaCubeModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class MagmaCubeModelDefinition
{
    public const int MagmaCubeEntityTypeId = 81;

    public static readonly EntityModelDefinition Definition = new(
        "magma_cube",
        64,
        64,
        [
            PlayerModelDefinition.FromVanilla("cube0", 0f, 0f, 0f, -4f, 16f, -4f, 8f, 1f, 8f, 0, 0),
            PlayerModelDefinition.FromVanilla("cube1", 0f, 0f, 0f, -4f, 17f, -4f, 8f, 1f, 8f, 0, 9),
            PlayerModelDefinition.FromVanilla("cube2", 0f, 0f, 0f, -4f, 18f, -4f, 8f, 1f, 8f, 0, 18),
            PlayerModelDefinition.FromVanilla("cube3", 0f, 0f, 0f, -4f, 19f, -4f, 8f, 1f, 8f, 0, 27),
            PlayerModelDefinition.FromVanilla("cube4", 0f, 0f, 0f, -4f, 20f, -4f, 8f, 1f, 8f, 32, 0),
            PlayerModelDefinition.FromVanilla("cube5", 0f, 0f, 0f, -4f, 21f, -4f, 8f, 1f, 8f, 32, 9),
            PlayerModelDefinition.FromVanilla("cube6", 0f, 0f, 0f, -4f, 22f, -4f, 8f, 1f, 8f, 32, 18),
            PlayerModelDefinition.FromVanilla("cube7", 0f, 0f, 0f, -4f, 23f, -4f, 8f, 1f, 8f, 32, 27),
            PlayerModelDefinition.FromVanilla("inside_cube", 0f, 0f, 0f, -2f, 18f, -2f, 4f, 4f, 4f, 24, 40)
        ],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/slime/magmacube");
}
