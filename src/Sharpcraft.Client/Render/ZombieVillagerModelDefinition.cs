// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// ZombieVillagerModel extends HumanoidModel: 8x10x8 head with nose, body with a 20px outer cube,
/// Humanoid arms/legs. It is not the VillagerModel geometry.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.monster.zombie.ZombieVillagerModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/zombie/ZombieVillagerModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class ZombieVillagerModelDefinition
{
    public const int ZombieVillagerEntityTypeId = 157;

    public static readonly EntityModelDefinition Definition = new(
        "zombie_villager",
        64,
        64,
        [
            PlayerModelDefinition.FromVanilla("head", 0f, 0f, 0f, -4f, -10f, -4f, 8f, 10f, 8f, 0, 0,
                overlays: [new("hat", 0.5f, 32, 0)]),
            PlayerModelDefinition.FromVanilla("nose", 0f, 0f, 0f, -1f, -3f, -6f, 2f, 4f, 2f, 24, 0),
            PlayerModelDefinition.FromVanilla("body", 0f, 0f, 0f, -4f, 0f, -3f, 8f, 12f, 6f, 16, 20),
            PlayerModelDefinition.FromVanilla("body_outer", 0f, 0f, 0f, -4f, 0f, -3f, 8f, 20f, 6f, 0, 38, grow: 0.05f),
            PlayerModelDefinition.FromVanilla("left_arm", 5f, 2f, 0f, -1f, -2f, -2f, 4f, 12f, 4f, 44, 22, mirror: true),
            PlayerModelDefinition.FromVanilla("right_arm", -5f, 2f, 0f, -3f, -2f, -2f, 4f, 12f, 4f, 44, 22),
            PlayerModelDefinition.FromVanilla("left_leg", 2f, 12f, 0f, -2f, 0f, -2f, 4f, 12f, 4f, 0, 22),
            PlayerModelDefinition.FromVanilla("right_leg", -2f, 12f, 0f, -2f, 0f, -2f, 4f, 12f, 4f, 0, 22)
        ],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/zombie_villager/zombie_villager");

    public static readonly IReadOnlyDictionary<string, EntityModelDefinition> Variants =
        VillagerModelDefinition.VariantMap(Definition.Parts, "entity/zombie_villager/type/", 1f, "zombie_villager_", "entity/zombie_villager/zombie_villager");
}
