// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// Humanoid mob definitions. Every part is written in the reference's y-down pivot/cube form and converted by
/// <see cref="PlayerModelDefinition.FromVanilla"/>; no per-model Bottom/CentreX arithmetic remains here.
/// </summary>
public static class HumanoidMobModelDefinition
{
    public const int SkeletonEntityTypeId = 118;
    public const int StrayEntityTypeId = 131;
    public const int BoggedEntityTypeId = 16;
    public const int WitherSkeletonEntityTypeId = 150;
    public const int HuskEntityTypeId = 68;
    public const int DrownedEntityTypeId = 39;
    public const int EndermanEntityTypeId = 42;
    public const int ZombieEntityTypeId = 154;

    public static readonly EntityModelDefinition Enderman = new(
        "enderman",
        64,
        32,
        [
            PlayerModelDefinition.FromVanilla("head", 0f, -13f, 0f, -4f, -8f, -4f, 8f, 8f, 8f, 0, 0, overlays: [new("hat", -0.5f, 0, 16)], modelHeight: 25f),
            PlayerModelDefinition.FromVanilla("body", 0f, -14f, 0f, -4f, 0f, -2f, 8f, 12f, 4f, 32, 16, modelHeight: 25f),
            PlayerModelDefinition.FromVanilla("left_arm", 5f, -12f, 0f, -1f, -2f, -1f, 2f, 30f, 2f, 56, 0, mirror: true, modelHeight: 25f),
            PlayerModelDefinition.FromVanilla("right_arm", -5f, -12f, 0f, -1f, -2f, -1f, 2f, 30f, 2f, 56, 0, modelHeight: 25f),
            PlayerModelDefinition.FromVanilla("left_leg", 2f, -5f, 0f, -1f, 0f, -1f, 2f, 30f, 2f, 56, 0, mirror: true, modelHeight: 25f),
            PlayerModelDefinition.FromVanilla("right_leg", -2f, -5f, 0f, -1f, 0f, -1f, 2f, 30f, 2f, 56, 0, modelHeight: 25f)
        ],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/enderman/enderman");

    public static readonly EntityModelDefinition Drowned = new(
        "drowned",
        64,
        64,
        [
            PlayerModelDefinition.FromVanilla("head", 0f, 0f, 0f, -4f, -8f, -4f, 8f, 8f, 8f, 0, 0, overlays: [new("hat", 0.5f, 32, 0), DrownedOuter(0, 0)]),
            PlayerModelDefinition.FromVanilla("body", 0f, 0f, 0f, -4f, 0f, -2f, 8f, 12f, 4f, 16, 16, overlays: [DrownedOuter(16, 16)]),
            PlayerModelDefinition.FromVanilla("left_arm", 5f, 2f, 0f, -1f, -2f, -2f, 4f, 12f, 4f, 32, 48, overlays: [DrownedOuter(32, 48)]),
            PlayerModelDefinition.FromVanilla("right_arm", -5f, 2f, 0f, -3f, -2f, -2f, 4f, 12f, 4f, 40, 16, overlays: [DrownedOuter(40, 16)]),
            PlayerModelDefinition.FromVanilla("left_leg", 1.9f, 12f, 0f, -2f, 0f, -2f, 4f, 12f, 4f, 16, 48, overlays: [DrownedOuter(16, 48)]),
            PlayerModelDefinition.FromVanilla("right_leg", -1.9f, 12f, 0f, -2f, 0f, -2f, 4f, 12f, 4f, 0, 16, overlays: [DrownedOuter(0, 16)])
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
            PlayerModelDefinition.FromVanilla("head", 0f, 0f, 0f, -4f, -8f, -4f, 8f, 8f, 8f, 0, 0, overlays: [new("hat", 0.5f, 32, 0)]),
            PlayerModelDefinition.FromVanilla("body", 0f, 0f, 0f, -4f, 0f, -2f, 8f, 12f, 4f, 16, 16),
            PlayerModelDefinition.FromVanilla("left_arm", 5f, 2f, 0f, -1f, -2f, -2f, 4f, 12f, 4f, 40, 16, mirror: true),
            PlayerModelDefinition.FromVanilla("right_arm", -5f, 2f, 0f, -3f, -2f, -2f, 4f, 12f, 4f, 40, 16),
            PlayerModelDefinition.FromVanilla("left_leg", 1.9f, 12f, 0f, -2f, 0f, -2f, 4f, 12f, 4f, 0, 16, mirror: true),
            PlayerModelDefinition.FromVanilla("right_leg", -1.9f, 12f, 0f, -2f, 0f, -2f, 4f, 12f, 4f, 0, 16)
        ],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/zombie/husk",
        1.0625f);

    public static readonly EntityModelDefinition Skeleton = new(
        "skeleton",
        64,
        32,
        [
            PlayerModelDefinition.FromVanilla("head", 0f, 0f, 0f, -4f, -8f, -4f, 8f, 8f, 8f, 0, 0, overlays: [new("hat", 0.5f, 32, 0)]),
            PlayerModelDefinition.FromVanilla("body", 0f, 0f, 0f, -4f, 0f, -2f, 8f, 12f, 4f, 16, 16),
            PlayerModelDefinition.FromVanilla("left_arm", 5f, 2f, 0f, -1f, -2f, -1f, 2f, 12f, 2f, 40, 16, mirror: true),
            PlayerModelDefinition.FromVanilla("right_arm", -5f, 2f, 0f, -1f, -2f, -1f, 2f, 12f, 2f, 40, 16),
            PlayerModelDefinition.FromVanilla("left_leg", 2f, 12f, 0f, -1f, 0f, -1f, 2f, 12f, 2f, 0, 16, mirror: true),
            PlayerModelDefinition.FromVanilla("right_leg", -2f, 12f, 0f, -1f, 0f, -1f, 2f, 12f, 2f, 0, 16)
        ],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/skeleton/skeleton");

    public static readonly EntityModelDefinition Stray = WithClothing("stray", "entity/skeleton/stray", "entity/skeleton/stray_overlay", 0.25f);

    public static readonly EntityModelDefinition Bogged = WithClothing("bogged", "entity/skeleton/bogged", "entity/skeleton/bogged_overlay", 0.2f);

    private static EntityModelDefinition WithClothing(string name, string texturePath, string overlayPath, float grow)
    {
        var parts = new List<EntityModelPart>(Skeleton.Parts)
        {
            PlayerModelDefinition.FromVanilla("outer_head", 0f, 0f, 0f, -4f, -8f, -4f, 8f, 8f, 8f, 0, 0,
                grow: grow, texturePath: overlayPath),
            PlayerModelDefinition.FromVanilla("outer_body", 0f, 0f, 0f, -4f, 0f, -2f, 8f, 12f, 4f, 16, 16,
                grow: grow, texturePath: overlayPath),
            PlayerModelDefinition.FromVanilla("outer_left_arm", 5f, 2f, 0f, -1f, -2f, -2f, 4f, 12f, 4f, 40, 16,
                mirror: true, grow: grow, texturePath: overlayPath),
            PlayerModelDefinition.FromVanilla("outer_right_arm", -5f, 2f, 0f, -3f, -2f, -2f, 4f, 12f, 4f, 40, 16,
                grow: grow, texturePath: overlayPath),
            PlayerModelDefinition.FromVanilla("outer_left_leg", 1.9f, 12f, 0f, -2f, 0f, -2f, 4f, 12f, 4f, 0, 16,
                mirror: true, grow: grow, texturePath: overlayPath),
            PlayerModelDefinition.FromVanilla("outer_right_leg", -1.9f, 12f, 0f, -2f, 0f, -2f, 4f, 12f, 4f, 0, 16,
                grow: grow, texturePath: overlayPath)
        };
        return new EntityModelDefinition(name, 64, 32, parts, PlayerModelDefinition.DefaultFaceTransforms, texturePath);
    }

    public static readonly EntityModelDefinition WitherSkeleton = new(
        "wither_skeleton", 64, 32, Skeleton.Parts, PlayerModelDefinition.DefaultFaceTransforms,
        "entity/skeleton/wither_skeleton");

    public static readonly EntityModelDefinition Zombie = new(
        "zombie",
        64,
        64,
        [
            PlayerModelDefinition.FromVanilla("head", 0f, 0f, 0f, -4f, -8f, -4f, 8f, 8f, 8f, 0, 0, overlays: [new("hat", 0.5f, 32, 0)]),
            PlayerModelDefinition.FromVanilla("body", 0f, 0f, 0f, -4f, 0f, -2f, 8f, 12f, 4f, 16, 16),
            PlayerModelDefinition.FromVanilla("left_arm", 5f, 2f, 0f, -1f, -2f, -2f, 4f, 12f, 4f, 40, 16, mirror: true),
            PlayerModelDefinition.FromVanilla("right_arm", -5f, 2f, 0f, -3f, -2f, -2f, 4f, 12f, 4f, 40, 16),
            PlayerModelDefinition.FromVanilla("left_leg", 1.9f, 12f, 0f, -2f, 0f, -2f, 4f, 12f, 4f, 0, 16, mirror: true),
            PlayerModelDefinition.FromVanilla("right_leg", -1.9f, 12f, 0f, -2f, 0f, -2f, 4f, 12f, 4f, 0, 16)
        ],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/zombie/zombie");
}
