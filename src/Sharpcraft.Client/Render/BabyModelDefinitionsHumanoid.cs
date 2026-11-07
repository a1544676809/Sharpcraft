// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Baby zombie/drowned/zombie villager/villager models. Zombie, zombie villager and villager parts are declared with the
/// <c>VanillaMeshDefinition</c> DSL (including nested child parts) and baked via <c>VanillaMeshBake.Bake</c> (T-674);
/// the drowned outer copies and the variant copy layers are still built from those baked parts.
/// </summary>
public static class BabyModelDefinitionsHumanoid
{
    /// <summary>Declarative DSL tree for the baby zombie parts (T-674).</summary>
    private static readonly VanillaMeshDefinition ZombieMesh = BuildZombieMesh();

    private static readonly EntityModelPart[] ZombieParts = [.. VanillaMeshBake.Bake(ZombieMesh, 0, 0)];

    private static readonly CompiledEntityModel ZombieCompiled =
        VanillaMeshBakery.Build(ZombieMesh, 64, 64);

    public static readonly EntityModelDefinition Zombie = new(
        "baby_zombie",
        64,
        64,
        ZombieParts,
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/zombie/zombie_baby",
        compiled: ZombieCompiled);

    private static readonly CompiledEntityModel DrownedCompiled =
        EntityModelLayerHelpers.WithModelLayer(
            ZombieCompiled,
            ZombieCompiled,
            "outer_",
            _ => "entity/zombie/drowned_outer_layer_baby",
            fallbackTexture: "entity/zombie/drowned_outer_layer_baby");

    public static readonly EntityModelDefinition Drowned = new(
        "baby_drowned",
        64,
        64,
        BuildDrownedParts(),
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/zombie/drowned_baby",
        compiled: DrownedCompiled);

    /// <summary>Declarative DSL tree for the baby zombie villager parts (T-674).</summary>
    private static readonly VanillaMeshDefinition ZombieVillagerMesh = BuildZombieVillagerMesh();

    private static readonly CompiledEntityModel ZombieVillagerCompiled =
        VanillaMeshBakery.Build(ZombieVillagerMesh, 64, 64);

    public static readonly EntityModelDefinition ZombieVillager = new(
        "baby_zombie_villager",
        64,
        64,
        [.. VanillaMeshBake.Bake(ZombieVillagerMesh, 0, 0)],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/zombie_villager/zombie_villager_baby",
        compiled: ZombieVillagerCompiled);

    /// <summary>Declarative DSL tree for the baby villager parts (T-674).</summary>
    private static readonly VanillaMeshDefinition VillagerMesh = BuildVillagerMesh();

    private static readonly CompiledEntityModel VillagerCompiled =
        VanillaMeshBakery.Build(VillagerMesh, 64, 64);

    public static readonly EntityModelDefinition Villager = new(
        "baby_villager",
        64,
        64,
        [.. VanillaMeshBake.Bake(VillagerMesh, 0, 0)],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/villager/villager_baby",
        compiled: VillagerCompiled);

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
            var villagerPath = "entity/villager/baby/" + key;
            variants[(143, key)] = new EntityModelDefinition(
                "baby_villager_" + key,
                64,
                64,
                EntityModelLayerHelpers.WithCopy(Villager.Parts, villagerPath, static _ => true, 0.05f),
                PlayerModelDefinition.DefaultFaceTransforms,
                "entity/villager/villager_baby",
                compiled: EntityModelLayerHelpers.WithModelLayer(
                    VillagerCompiled,
                    VillagerCompiled,
                    "copy_",
                    _ => villagerPath,
                    fallbackTexture: villagerPath,
                    inflate: 0.05f));
            var zombieVillagerPath = "entity/zombie_villager/baby/" + key;
            variants[(157, key)] = new EntityModelDefinition(
                "baby_zombie_villager_" + key,
                64,
                64,
                EntityModelLayerHelpers.WithCopy(ZombieVillager.Parts, zombieVillagerPath, static _ => true, 0.05f),
                PlayerModelDefinition.DefaultFaceTransforms,
                "entity/zombie_villager/zombie_villager_baby",
                compiled: EntityModelLayerHelpers.WithModelLayer(
                    ZombieVillagerCompiled,
                    ZombieVillagerCompiled,
                    "copy_",
                    _ => zombieVillagerPath,
                    fallbackTexture: zombieVillagerPath,
                    inflate: 0.05f));
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

            parts.Add(source.With(
                Name: "outer_" + source.Name,
                TexturePath: "entity/zombie/drowned_outer_layer_baby",
                Kind: EntityGeometryLayerKind.Translucent));
        }

        return [.. parts];
    }

    private static VanillaMeshDefinition BuildZombieMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        root.AddOrReplaceChild("body", VanillaPartPose.Offset(0f, 17.5f, 0f),
            new VanillaCubeDefinition("body", 16, 16, -2f, -2.5f, -1f, 4f, 5f, 2f));
        var head = root.AddOrReplaceChild("head", VanillaPartPose.Offset(0f, 15.25f, 0f),
            new VanillaCubeDefinition("head", 3, 3, -3f, -6.25f, -3f, 6f, 6f, 6f));
        head.AddOrReplaceChild("head_overlay", VanillaPartPose.Zero,
            new VanillaCubeDefinition("head_overlay", 35, 3, -3f, -6.15f, -3f, 6f, 6f, 6f, Grow: 0.25f));
        head.AddOrReplaceChild("hat", VanillaPartPose.Zero,
            new VanillaCubeDefinition("hat", 0, 0, 0f, 0f, 0f, 0f, 0f, 0f));
        root.AddOrReplaceChild("right_arm", VanillaPartPose.Offset(-3f, 15.5f, 0f),
            new VanillaCubeDefinition("right_arm", 36, 16, -1f, -0.5f, -1f, 2f, 5f, 2f));
        root.AddOrReplaceChild("left_arm", VanillaPartPose.Offset(3f, 15.5f, 0f),
            new VanillaCubeDefinition("left_arm", 28, 16, -1f, -0.5f, -1f, 2f, 5f, 2f));
        root.AddOrReplaceChild("right_leg", VanillaPartPose.Offset(-1f, 20f, 0f),
            new VanillaCubeDefinition("right_leg", 8, 16, -1f, 0f, -1f, 2f, 4f, 2f));
        root.AddOrReplaceChild("left_leg", VanillaPartPose.Offset(1f, 20f, 0f),
            new VanillaCubeDefinition("left_leg", 0, 16, -1f, 0f, -1f, 2f, 4f, 2f));
        return new VanillaMeshDefinition(root);
    }

    private static VanillaMeshDefinition BuildZombieVillagerMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        var body = root.AddOrReplaceChild("body", VanillaPartPose.Offset(0f, 18.75f, 0f),
            new VanillaCubeDefinition("body", 0, 15, -2f, -2.75f, -1.5f, 4f, 5f, 3f));
        body.AddOrReplaceChild("body_overlay", VanillaPartPose.Zero,
            new VanillaCubeDefinition("body_overlay", 16, 22, -2f, -2.75f, -1.5f, 4f, 6f, 3f, Grow: 0.1f));
        var head = root.AddOrReplaceChild("head", VanillaPartPose.Offset(0f, 16f, 0f),
            new VanillaCubeDefinition("head", 0, 0, -4f, -8f, -3.5f, 8f, 8f, 7f));
        head.AddOrReplaceChild("hat", VanillaPartPose.Offset(0f, -4f, 0f),
            new VanillaCubeDefinition("hat", 0, 31, -4f, -4f, -3.5f, 8f, 8f, 7f, Grow: 0.3f));
        head.AddOrReplaceChild("hat_rim", VanillaPartPose.Offset(0f, -4.5f, 0f),
            new VanillaCubeDefinition("hat_rim", 0, 46, -7f, -0.5f, -6f, 14f, 1f, 12f));
        head.AddOrReplaceChild("nose", VanillaPartPose.Offset(0f, -1f, -4f),
            new VanillaCubeDefinition("nose", 23, 0, -1f, -1f, -0.5f, 2f, 2f, 1f));
        root.AddOrReplaceChild("right_arm", VanillaPartPose.Offset(-3f, 15.5f, 0f),
            new VanillaCubeDefinition("right_arm", 24, 15, -1f, -0.5f, -1f, 2f, 5f, 2f));
        root.AddOrReplaceChild("left_arm", VanillaPartPose.Offset(3f, 15.5f, 0f),
            new VanillaCubeDefinition("left_arm", 16, 15, -1f, -0.5f, -1f, 2f, 5f, 2f));
        root.AddOrReplaceChild("right_leg", VanillaPartPose.Offset(-1f, 21.5f, 0f),
            new VanillaCubeDefinition("right_leg", 8, 23, -1f, -0.5f, -1f, 2f, 3f, 2f));
        root.AddOrReplaceChild("left_leg", VanillaPartPose.Offset(1f, 21.5f, 0f),
            new VanillaCubeDefinition("left_leg", 0, 23, -1f, -0.5f, -1f, 2f, 3f, 2f));
        return new VanillaMeshDefinition(root);
    }

    private static VanillaMeshDefinition BuildVillagerMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        var arms = root.AddOrReplaceChild("arms", VanillaPartPose.Offset(0f, 17.5f, 0f),
            new VanillaCubeDefinition("arms", 0, 0, 0f, 0f, 0f, 0f, 0f, 0f));
        var rightHand = arms.AddOrReplaceChild("right_hand",
            VanillaPartPose.OffsetAndRotation(-3f, 1.4025f, -0.9599f, -1.0472f, 0f, 0f),
            new VanillaCubeDefinition("right_hand", 36, 15, -1f, -2.4925f, -1.8401f, 2f, 4f, 2f));
        rightHand.AddOrReplaceChild("right_hand_2", VanillaPartPose.Zero,
            new VanillaCubeDefinition("right_hand_2", 16, 15, 5f, -2.4925f, -1.8401f, 2f, 4f, 2f));
        arms.AddOrReplaceChild("middlearm_r1",
            VanillaPartPose.OffsetAndRotation(0f, 0.9024f, -1.8175f, -1.0472f, 0f, 0f),
            new VanillaCubeDefinition("middlearm_r1", 24, 17, -2f, -0.9924f, -0.9825f, 4f, 2f, 2f));
        root.AddOrReplaceChild("right_leg", VanillaPartPose.Offset(-1f, 21.5f, 0f),
            new VanillaCubeDefinition("right_leg", 8, 23, -1f, -0.5f, -1f, 2f, 3f, 2f));
        root.AddOrReplaceChild("left_leg", VanillaPartPose.Offset(1f, 21.5f, 0f),
            new VanillaCubeDefinition("left_leg", 0, 23, -1f, -0.5f, -1f, 2f, 3f, 2f));
        var head = root.AddOrReplaceChild("head", VanillaPartPose.Offset(0f, 16f, 0f),
            new VanillaCubeDefinition("head", 0, 0, -4f, -8f, -3.5f, 8f, 8f, 7f));
        head.AddOrReplaceChild("hat", VanillaPartPose.Offset(0f, -4f, 0f),
            new VanillaCubeDefinition("hat", 0, 30, -4f, -4f, -3.5f, 8f, 8f, 7f, Grow: 0.3f));
        head.AddOrReplaceChild("hat_rim", VanillaPartPose.Offset(0f, -4.5f, 0f),
            new VanillaCubeDefinition("hat_rim", 0, 45, -7f, -0.5f, -6f, 14f, 1f, 12f));
        head.AddOrReplaceChild("nose", VanillaPartPose.Offset(0f, -2f, -4f),
            new VanillaCubeDefinition("nose", 23, 0, -1f, 0f, -0.5f, 2f, 2f, 1f));
        root.AddOrReplaceChild("body", VanillaPartPose.Offset(0f, 18.75f, 0f),
            new VanillaCubeDefinition("body", 0, 15, -2f, -2.75f, -1.5f, 4f, 5f, 3f));
        root.AddOrReplaceChild("bb_main", VanillaPartPose.Offset(0.5f, 24f, 0f),
            new VanillaCubeDefinition("bb_main", 16, 21, -2.5f, -8f, -1.5f, 4f, 6f, 3f, Grow: 0.2f));
        return new VanillaMeshDefinition(root);
    }
}
