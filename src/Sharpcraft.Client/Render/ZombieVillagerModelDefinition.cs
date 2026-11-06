// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// ZombieVillagerModel extends HumanoidModel: 8x10x8 head with nose, body with a 20px outer cube,
/// Humanoid arms/legs. It is not the VillagerModel geometry. Parts are declared with the
/// <c>VanillaMeshDefinition</c> DSL and baked via <c>VanillaMeshBake.Bake</c> (T-683); the VariantMap call is unchanged.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.monster.zombie.ZombieVillagerModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/zombie/ZombieVillagerModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class ZombieVillagerModelDefinition
{
    public const int ZombieVillagerEntityTypeId = 157;

    /// <summary>Declarative DSL tree for the eight zombie-villager parts (T-683).</summary>
    private static readonly VanillaMeshDefinition ZombieVillagerMesh = BuildZombieVillagerMesh();

    public static readonly EntityModelDefinition Definition = new(
        "zombie_villager",
        64,
        64,
        [.. VanillaMeshBake.Bake(ZombieVillagerMesh, 0, 0)],
        PlayerModelDefinition.DefaultFaceTransforms,
        "entity/zombie_villager/zombie_villager");

    public static readonly IReadOnlyDictionary<string, EntityModelDefinition> Variants =
        VillagerModelDefinition.VariantMap(Definition.Parts, "entity/zombie_villager/type/", 1f, "zombie_villager_", "entity/zombie_villager/zombie_villager");

    private static VanillaMeshDefinition BuildZombieVillagerMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        root.AddOrReplaceChild("head", VanillaPartPose.Zero,
            new VanillaCubeDefinition("head", 0, 0, -4f, -10f, -4f, 8f, 10f, 8f, Overlays: [new("hat", 0.5f, 32, 0)]));
        root.AddOrReplaceChild("nose", VanillaPartPose.Zero,
            new VanillaCubeDefinition("nose", 24, 0, -1f, -3f, -6f, 2f, 4f, 2f));
        root.AddOrReplaceChild("body", VanillaPartPose.Zero,
            new VanillaCubeDefinition("body", 16, 20, -4f, 0f, -3f, 8f, 12f, 6f));
        root.AddOrReplaceChild("body_outer", VanillaPartPose.Zero,
            new VanillaCubeDefinition("body_outer", 0, 38, -4f, 0f, -3f, 8f, 20f, 6f, Grow: 0.05f));
        root.AddOrReplaceChild("left_arm", VanillaPartPose.Offset(5f, 2f, 0f),
            new VanillaCubeDefinition("left_arm", 44, 22, -1f, -2f, -2f, 4f, 12f, 4f, Mirror: true));
        root.AddOrReplaceChild("right_arm", VanillaPartPose.Offset(-5f, 2f, 0f),
            new VanillaCubeDefinition("right_arm", 44, 22, -3f, -2f, -2f, 4f, 12f, 4f));
        root.AddOrReplaceChild("left_leg", VanillaPartPose.Offset(2f, 12f, 0f),
            new VanillaCubeDefinition("left_leg", 0, 22, -2f, 0f, -2f, 4f, 12f, 4f));
        root.AddOrReplaceChild("right_leg", VanillaPartPose.Offset(-2f, 12f, 0f),
            new VanillaCubeDefinition("right_leg", 0, 22, -2f, 0f, -2f, 4f, 12f, 4f));
        return new VanillaMeshDefinition(root);
    }
}
