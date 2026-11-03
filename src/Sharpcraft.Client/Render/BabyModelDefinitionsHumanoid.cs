// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Render;

/// <summary>Baby zombie/drowned/zombie villager/villager models.</summary>
public static class BabyModelDefinitionsHumanoid
{
    private static readonly EntityModelPart[] ZombieParts =
    [
        PlayerModelDefinition.FromVanilla("body", 0f, 17.5f, 0f, -2f, -2.5f, -1f, 4f, 5f, 2f, 16, 16),
        PlayerModelDefinition.FromVanilla("head", 0f, 15.25f, 0f, -3f, -6.25f, -3f, 6f, 6f, 6f, 3, 3),
        PlayerModelDefinition.FromVanillaChild("head_overlay", "head", 0f, 0f, 0f, -3f, -6.15f, -3f, 6f, 6f, 6f, 35, 3, grow: 0.25f),
        PlayerModelDefinition.FromVanillaChild("hat", "head", 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0),
        PlayerModelDefinition.FromVanilla("right_arm", -3f, 15.5f, 0f, -1f, -0.5f, -1f, 2f, 5f, 2f, 36, 16),
        PlayerModelDefinition.FromVanilla("left_arm", 3f, 15.5f, 0f, -1f, -0.5f, -1f, 2f, 5f, 2f, 28, 16),
        PlayerModelDefinition.FromVanilla("right_leg", -1f, 20f, 0f, -1f, 0f, -1f, 2f, 4f, 2f, 8, 16),
        PlayerModelDefinition.FromVanilla("left_leg", 1f, 20f, 0f, -1f, 0f, -1f, 2f, 4f, 2f, 0, 16)
    ];

    public static readonly EntityModelDefinition Zombie = new(
        "baby_zombie",
        64,
        64,
        ZombieParts,
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/zombie/zombie_baby");

    public static readonly EntityModelDefinition Drowned = new(
        "baby_drowned",
        64,
        64,
        BuildDrownedParts(),
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/zombie/drowned_baby");

    public static readonly EntityModelDefinition ZombieVillager = new(
        "baby_zombie_villager",
        64,
        64,
        [
            PlayerModelDefinition.FromVanilla("body", 0f, 18.75f, 0f, -2f, -2.75f, -1.5f, 4f, 5f, 3f, 0, 15),
            PlayerModelDefinition.FromVanillaChild("body_overlay", "body", 0f, 0f, 0f, -2f, -2.75f, -1.5f, 4f, 6f, 3f, 16, 22, grow: 0.1f),
            PlayerModelDefinition.FromVanilla("head", 0f, 16f, 0f, -4f, -8f, -3.5f, 8f, 8f, 7f, 0, 0),
            PlayerModelDefinition.FromVanillaChild("hat", "head", 0f, -4f, 0f, -4f, -4f, -3.5f, 8f, 8f, 7f, 0, 31, grow: 0.3f),
            PlayerModelDefinition.FromVanillaChild("hat_rim", "head", 0f, -4.5f, 0f, -7f, -0.5f, -6f, 14f, 1f, 12f, 0, 46),
            PlayerModelDefinition.FromVanillaChild("nose", "head", 0f, -1f, -4f, -1f, -1f, -0.5f, 2f, 2f, 1f, 23, 0),
            PlayerModelDefinition.FromVanilla("right_arm", -3f, 15.5f, 0f, -1f, -0.5f, -1f, 2f, 5f, 2f, 24, 15),
            PlayerModelDefinition.FromVanilla("left_arm", 3f, 15.5f, 0f, -1f, -0.5f, -1f, 2f, 5f, 2f, 16, 15),
            PlayerModelDefinition.FromVanilla("right_leg", -1f, 21.5f, 0f, -1f, -0.5f, -1f, 2f, 3f, 2f, 8, 23),
            PlayerModelDefinition.FromVanilla("left_leg", 1f, 21.5f, 0f, -1f, -0.5f, -1f, 2f, 3f, 2f, 0, 23)
        ],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/zombie_villager/zombie_villager_baby");

    public static readonly EntityModelDefinition Villager = new(
        "baby_villager",
        64,
        64,
        [
            PlayerModelDefinition.FromVanilla("arms", 0f, 17.5f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0, 0),
            PlayerModelDefinition.FromVanillaChild("right_hand", "arms", -3f, 1.4025f, -0.9599f, -1f, -2.4925f, -1.8401f, 2f, 4f, 2f, 36, 15, rotationX: -1.0472f),
            PlayerModelDefinition.FromVanillaChild("right_hand_2", "right_hand", 0f, 0f, 0f, 5f, -2.4925f, -1.8401f, 2f, 4f, 2f, 16, 15),
            PlayerModelDefinition.FromVanillaChild("middlearm_r1", "arms", 0f, 0.9024f, -1.8175f, -2f, -0.9924f, -0.9825f, 4f, 2f, 2f, 24, 17, rotationX: -1.0472f),
            PlayerModelDefinition.FromVanilla("right_leg", -1f, 21.5f, 0f, -1f, -0.5f, -1f, 2f, 3f, 2f, 8, 23),
            PlayerModelDefinition.FromVanilla("left_leg", 1f, 21.5f, 0f, -1f, -0.5f, -1f, 2f, 3f, 2f, 0, 23),
            PlayerModelDefinition.FromVanilla("head", 0f, 16f, 0f, -4f, -8f, -3.5f, 8f, 8f, 7f, 0, 0),
            PlayerModelDefinition.FromVanillaChild("hat", "head", 0f, -4f, 0f, -4f, -4f, -3.5f, 8f, 8f, 7f, 0, 30, grow: 0.3f),
            PlayerModelDefinition.FromVanillaChild("hat_rim", "head", 0f, -4.5f, 0f, -7f, -0.5f, -6f, 14f, 1f, 12f, 0, 45),
            PlayerModelDefinition.FromVanillaChild("nose", "head", 0f, -2f, -4f, -1f, 0f, -0.5f, 2f, 2f, 1f, 23, 0),
            PlayerModelDefinition.FromVanilla("body", 0f, 18.75f, 0f, -2f, -2.75f, -1.5f, 4f, 5f, 3f, 0, 15),
            PlayerModelDefinition.FromVanilla("bb_main", 0.5f, 24f, 0f, -2.5f, -8f, -1.5f, 4f, 6f, 3f, 16, 21, grow: 0.2f)
        ],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/villager/villager_baby");

    public static readonly IReadOnlyDictionary<int, EntityModelDefinition> Models =
        new Dictionary<int, EntityModelDefinition>
        {
            [154] = Zombie,
            [39] = Drowned,
            [157] = ZombieVillager,
            [143] = Villager
        };

    public static readonly IReadOnlyDictionary<(int TypeId, string Key), EntityModelDefinition> Variants = BuildVariants();

    private static Dictionary<(int, string), EntityModelDefinition> BuildVariants()
    {
        var variants = new Dictionary<(int, string), EntityModelDefinition>();
        foreach (var key in VillagerModelDefinition.VariantKeys)
        {
            variants[(143, key)] = new EntityModelDefinition(
                "baby_villager_" + key,
                64,
                64,
                EntityModelLayerHelpers.WithCopy(Villager.Parts, "entity/villager/baby/" + key, static _ => true, 0.05f),
                PlayerModelDefinition.DefaultFaceTransforms,
                "entity/villager/villager_baby");
            variants[(157, key)] = new EntityModelDefinition(
                "baby_zombie_villager_" + key,
                64,
                64,
                EntityModelLayerHelpers.WithCopy(ZombieVillager.Parts, "entity/zombie_villager/baby/" + key, static _ => true, 0.05f),
                PlayerModelDefinition.DefaultFaceTransforms,
                "entity/zombie_villager/zombie_villager_baby");
        }

        return variants;
    }

    private static EntityModelPart[] BuildDrownedParts()
    {
        var parts = new List<EntityModelPart>(ZombieParts);
        foreach (var source in ZombieParts)
        {
            if (source.Name == "hat")
            {
                continue;
            }

            parts.Add(source with
            {
                Name = "outer_" + source.Name,
                TexturePath = "entity/zombie/drowned_outer_layer_baby",
                Kind = EntityGeometryLayerKind.Translucent
            });
        }

        return [.. parts];
    }
}
