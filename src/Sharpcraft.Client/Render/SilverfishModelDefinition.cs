// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// SilverfishModel: seven root body segments plus three root layer cubes.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.monster.silverfish.SilverfishModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/silverfish/SilverfishModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class SilverfishModelDefinition
{
    public const int SilverfishEntityTypeId = 117;

    public static readonly EntityModelDefinition Definition = new(
        "silverfish",
        64,
        32,
        [
            PlayerModelDefinition.FromVanilla("segment0", 0f, 22f, -3.5f, -1.5f, 0f, -1f, 3f, 2f, 2f, 0, 0),
            PlayerModelDefinition.FromVanilla("segment1", 0f, 21f, -1.5f, -2f, 0f, -1f, 4f, 3f, 2f, 0, 4),
            PlayerModelDefinition.FromVanilla("segment2", 0f, 20f, 1f, -3f, 0f, -1.5f, 6f, 4f, 3f, 0, 9),
            PlayerModelDefinition.FromVanilla("segment3", 0f, 21f, 4f, -1.5f, 0f, -1.5f, 3f, 3f, 3f, 0, 16),
            PlayerModelDefinition.FromVanilla("segment4", 0f, 22f, 6.5f, -1f, 0f, -1.5f, 2f, 2f, 3f, 0, 22),
            PlayerModelDefinition.FromVanilla("segment5", 0f, 23f, 8.5f, -1f, 0f, -1f, 2f, 1f, 2f, 11, 0),
            PlayerModelDefinition.FromVanilla("segment6", 0f, 23f, 10.5f, -0.5f, 0f, -1f, 1f, 1f, 2f, 13, 4),
            PlayerModelDefinition.FromVanilla("layer0", 0f, 16f, 1f, -5f, 0f, -1.5f, 10f, 8f, 3f, 20, 0),
            PlayerModelDefinition.FromVanilla("layer1", 0f, 20f, 6.5f, -3f, 0f, -1.5f, 6f, 4f, 3f, 20, 11),
            PlayerModelDefinition.FromVanilla("layer2", 0f, 19f, -1.5f, -3f, 0f, -1.5f, 6f, 5f, 2f, 20, 18)
        ],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/silverfish/silverfish");
}
