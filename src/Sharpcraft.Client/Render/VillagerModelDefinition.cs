// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// VillagerModel: head/hat, body/jacket, nose, crossed-arm group (three cubes under one rotated pivot) and legs.
/// All values are raw PartPose/cube values; villagerLikeScale 0.9375 is applied at the definition level.
/// </summary>
public static class VillagerModelDefinition
{
    public const int VillagerEntityTypeId = 143;
    public const int WanderingTraderEntityTypeId = 145;

    public static readonly EntityModelDefinition Definition = new(
        "villager",
        64,
        64,
        [
            PlayerModelDefinition.FromVanilla("head", 0f, 0f, 0f, -4f, -10f, -4f, 8f, 10f, 8f, 0, 0,
                overlays: [new("hat", 0.51f, 32, 0)], mirrorX: false),
            PlayerModelDefinition.FromVanilla("body", 0f, 0f, 0f, -4f, 0f, -3f, 8f, 12f, 6f, 16, 20, mirrorX: false),
            PlayerModelDefinition.FromVanilla("jacket", 0f, 0f, 0f, -4f, 0f, -3f, 8f, 20f, 6f, 0, 38, mirrorX: false),
            PlayerModelDefinition.FromVanilla("nose", 0f, -2f, 0f, -1f, -1f, -6f, 2f, 4f, 2f, 24, 0, mirrorX: false),
            PlayerModelDefinition.FromVanilla("left_arm", 0f, 3f, -1f, -8f, -2f, -2f, 4f, 8f, 4f, 44, 22,
                mirror: true, mirrorX: false, rotationX: 0.75f),
            PlayerModelDefinition.FromVanilla("right_arm", 0f, 3f, -1f, 4f, -2f, -2f, 4f, 8f, 4f, 44, 22,
                mirrorX: false, rotationX: 0.75f),
            PlayerModelDefinition.FromVanilla("crossed_arms", 0f, 3f, -1f, -4f, 2f, -2f, 8f, 4f, 4f, 40, 38,
                mirrorX: false, rotationX: 0.75f),
            PlayerModelDefinition.FromVanilla("right_leg", -2f, 12f, 0f, -2f, 0f, -2f, 4f, 12f, 4f, 0, 22),
            PlayerModelDefinition.FromVanilla("left_leg", 2f, 12f, 0f, -2f, 0f, -2f, 4f, 12f, 4f, 0, 22, mirror: true)
        ],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/villager/villager",
        0.9375f);

    public static readonly EntityModelDefinition WanderingTrader = new(
        "wandering_trader",
        64,
        64,
        Definition.Parts,
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/wandering_trader/wandering_trader",
        0.9375f);
}
