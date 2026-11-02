// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// Humanoid mob definitions. Zombie uses HumanoidModel's 4x12x4 limbs; Skeleton's own layer replaces them with
/// 2x12x2 limbs and a 64x32 texture.
/// </summary>
public static class HumanoidMobModelDefinition
{
    public const int SkeletonEntityTypeId = 118;
    public const int HuskEntityTypeId = 68;
    public const int DrownedEntityTypeId = 39;
    public const int ZombieEntityTypeId = 154;

    public static readonly EntityModelDefinition Drowned = new(
        "drowned",
        64,
        64,
        [
            new("head", 0f, 24f, 8f, 8f, 8f, 0, 0, false,
                [new("hat", 0.5f, 32, 0), DrownedOuter(0, 0)]),
            new("body", 0f, 12f, 8f, 12f, 4f, 16, 16, false, [DrownedOuter(16, 16)]),
            new("left_arm", -6f, 12f, 4f, 12f, 4f, 32, 48, false, [DrownedOuter(32, 48)]),
            new("right_arm", 6f, 12f, 4f, 12f, 4f, 40, 16, false, [DrownedOuter(40, 16)]),
            new("left_leg", -1.9f, 0f, 4f, 12f, 4f, 16, 48, false, [DrownedOuter(16, 48)]),
            new("right_leg", 1.9f, 0f, 4f, 12f, 4f, 0, 16, false, [DrownedOuter(0, 16)])
        ],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/zombie/drowned");

    private static EntityModelLayer DrownedOuter(int textureX, int textureY) =>
        new("drowned_outer", 0.25f, textureX, textureY, false, "entity/zombie/drowned_outer_layer");

    public static readonly EntityModelDefinition Husk = new(
        "husk",
        64,
        64,
        [
            new("head", 0f, 24f, 8f, 8f, 8f, 0, 0, false, [new("hat", 0.5f, 32, 0)]),
            new("body", 0f, 12f, 8f, 12f, 4f, 16, 16),
            new("left_arm", -6f, 12f, 4f, 12f, 4f, 40, 16, true),
            new("right_arm", 6f, 12f, 4f, 12f, 4f, 40, 16),
            new("left_leg", -1.9f, 0f, 4f, 12f, 4f, 0, 16, true),
            new("right_leg", 1.9f, 0f, 4f, 12f, 4f, 0, 16)
        ],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/zombie/husk",
        1.0625f);

    public static readonly EntityModelDefinition Skeleton = new(
        "skeleton",
        64,
        32,
        [
            new("head", 0f, 24f, 8f, 8f, 8f, 0, 0, false, [new("hat", 0.5f, 32, 0)]),
            new("body", 0f, 12f, 8f, 12f, 4f, 16, 16),
            new("left_arm", -5f, 12f, 2f, 12f, 2f, 40, 16, true),
            new("right_arm", 5f, 12f, 2f, 12f, 2f, 40, 16),
            new("left_leg", -2f, 0f, 2f, 12f, 2f, 0, 16, true),
            new("right_leg", 2f, 0f, 2f, 12f, 2f, 0, 16)
        ],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/skeleton/skeleton");

    public static readonly EntityModelDefinition Zombie = new(
        "zombie",
        64,
        64,
        [
            new("head", 0f, 24f, 8f, 8f, 8f, 0, 0, false, [new("hat", 0.5f, 32, 0)]),
            new("body", 0f, 12f, 8f, 12f, 4f, 16, 16),
            new("left_arm", -6f, 12f, 4f, 12f, 4f, 40, 16, true),
            new("right_arm", 6f, 12f, 4f, 12f, 4f, 40, 16),
            new("left_leg", -1.9f, 0f, 4f, 12f, 4f, 0, 16, true),
            new("right_leg", 1.9f, 0f, 4f, 12f, 4f, 0, 16)
        ],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/zombie/zombie");
}
