// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// CreeperModel's static cubes: head/body plus four 4x6x4 legs offset in Z. All values are raw PartPose/cube
/// values and go through the shared vanilla factory.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.monster.creeper.CreeperModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/creeper/CreeperModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class CreeperModelDefinition
{
    public const int CreeperEntityTypeId = 32;

    public static readonly EntityModelDefinition Definition = new(
        "creeper",
        64,
        32,
        [
            PlayerModelDefinition.FromVanilla("head", 0f, 6f, 0f, -4f, -8f, -4f, 8f, 8f, 8f, 0, 0),
            PlayerModelDefinition.FromVanilla("body", 0f, 6f, 0f, -4f, 0f, -2f, 8f, 12f, 4f, 16, 16),
            PlayerModelDefinition.FromVanilla("left_hind_leg", 2f, 18f, 4f, -2f, 0f, -2f, 4f, 6f, 4f, 0, 16),
            PlayerModelDefinition.FromVanilla("right_hind_leg", -2f, 18f, 4f, -2f, 0f, -2f, 4f, 6f, 4f, 0, 16),
            PlayerModelDefinition.FromVanilla("left_front_leg", 2f, 18f, -4f, -2f, 0f, -2f, 4f, 6f, 4f, 0, 16),
            PlayerModelDefinition.FromVanilla("right_front_leg", -2f, 18f, -4f, -2f, 0f, -2f, 4f, 6f, 4f, 0, 16)
        ],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/creeper/creeper");
}
