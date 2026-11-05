// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// WitherBossModel: shoulders, a ribcage with three rib slabs, a tail, a centre head and two side heads.
/// Bind pose only; head/ribcage animation and the invulnerable armour layer are not ported.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.monster.wither.WitherBossModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/wither/WitherBossModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class WitherBossModelDefinition
{
    public const int WitherEntityTypeId = 149;

    public static readonly EntityModelDefinition Definition = new(
        "wither",
        64,
        64,
        [
            PlayerModelDefinition.FromVanilla("shoulders", 0f, 0f, 0f, -10f, 3.9f, -0.5f, 20f, 3f, 3f, 0, 16, mirrorX: false),
            PlayerModelDefinition.FromVanilla("ribcage", -2f, 6.9f, -0.5f, 0f, 0f, 0f, 3f, 10f, 3f, 0, 22,
                rotationX: -0.20420352f),
            PlayerModelDefinition.FromVanillaChild("rib_1", "ribcage", 0f, 0f, 0f, -4f, 1.5f, 0.5f, 11f, 2f, 2f, 24, 22),
            PlayerModelDefinition.FromVanillaChild("rib_2", "ribcage", 0f, 0f, 0f, -4f, 4f, 0.5f, 11f, 2f, 2f, 24, 22),
            PlayerModelDefinition.FromVanillaChild("rib_3", "ribcage", 0f, 0f, 0f, -4f, 6.5f, 0.5f, 11f, 2f, 2f, 24, 22),
            PlayerModelDefinition.FromVanilla("tail", -2f, 16.692228f, 1.527873f, 0f, 0f, 0f, 3f, 6f, 3f, 12, 22,
                rotationX: -0.83252203f),
            PlayerModelDefinition.FromVanilla("center_head", 0f, 0f, 0f, -4f, -4f, -4f, 8f, 8f, 8f, 0, 0),
            PlayerModelDefinition.FromVanilla("right_head", -8f, 4f, 0f, -4f, -4f, -4f, 6f, 6f, 6f, 32, 0),
            PlayerModelDefinition.FromVanilla("left_head", 10f, 4f, 0f, -4f, -4f, -4f, 6f, 6f, 6f, 32, 0)
        ],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/wither/wither");
}
