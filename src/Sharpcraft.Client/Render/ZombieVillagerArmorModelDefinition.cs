// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// Zombie villager armor is the dedicated source <c>ZombieVillagerModel.createArmorMeshSet</c> mesh, not the
/// shared humanoid armor: the head sits at y -10, body and legs use the source <c>g.extend(0.1)</c> deformation,
/// the legs are offset by 2 px instead of 1.9 px, and the hat keeps its source empty <c>hat_rim</c> group.
/// Helmet/chest/boots use the outer (1.0 px) deformation, leggings the inner (0.5 px) one; UVs are unchanged.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.monster.zombie.ZombieVillagerModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/zombie/ZombieVillagerModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "Zombie villager armor slot raw source-frame meshes; material layers separate")]
public static class ZombieVillagerArmorModelDefinition
{
    private const float Outer = 1f;
    private const float Inner = 0.5f;

    public static readonly EntityModelDefinition Helmet = Build("helmet", 5);
    public static readonly EntityModelDefinition Chestplate = Build("chestplate", 4);
    public static readonly EntityModelDefinition Leggings = Build("leggings", 3);
    public static readonly EntityModelDefinition Boots = Build("boots", 2);

    public static EntityModelDefinition ForSlot(int slot) => slot switch
    {
        5 => Helmet,
        4 => Chestplate,
        3 => Leggings,
        2 => Boots,
        _ => throw new ArgumentOutOfRangeException(nameof(slot))
    };

    internal static VanillaMeshDefinition CreateMesh(int slot)
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        if (slot == 5)
        {
            var head = root.AddOrReplaceChild("head", VanillaPartPose.Zero,
                new VanillaCubeDefinition("head", 0, 0, -4f, -10f, -4f, 8f, 8f, 8f, Grow: Outer));
            var hat = head.AddOrReplaceChild("hat", VanillaPartPose.Zero,
                new VanillaCubeDefinition("hat", 32, 0, -4f, -8f, -4f, 8f, 8f, 8f, Grow: Outer + 0.5f));
            hat.AddOrReplaceChild("hat_rim", VanillaPartPose.Zero);
        }
        else if (slot == 4)
        {
            root.AddOrReplaceChild("right_arm", VanillaPartPose.Offset(-5f, 2f, 0f),
                new VanillaCubeDefinition("right_arm", 40, 16, -3f, -2f, -2f, 4f, 12f, 4f, Grow: Outer));
            root.AddOrReplaceChild("left_arm", VanillaPartPose.Offset(5f, 2f, 0f),
                new VanillaCubeDefinition("left_arm", 40, 16, -1f, -2f, -2f, 4f, 12f, 4f, Mirror: true, Grow: Outer));
            root.AddOrReplaceChild("body", VanillaPartPose.Offset(0f, 0f, 0f),
                new VanillaCubeDefinition("body", 16, 16, -4f, 0f, -2f, 8f, 12f, 4f, Grow: Outer + 0.1f));
        }
        else if (slot == 3)
        {
            var grow = Inner + 0.1f;
            root.AddOrReplaceChild("left_leg", VanillaPartPose.Offset(2f, 12f, 0f),
                new VanillaCubeDefinition("left_leg", 0, 16, -2f, 0f, -2f, 4f, 12f, 4f, Mirror: true, Grow: grow));
            root.AddOrReplaceChild("right_leg", VanillaPartPose.Offset(-2f, 12f, 0f),
                new VanillaCubeDefinition("right_leg", 0, 16, -2f, 0f, -2f, 4f, 12f, 4f, Grow: grow));
            root.AddOrReplaceChild("body", VanillaPartPose.Offset(0f, 0f, 0f),
                new VanillaCubeDefinition("body", 16, 16, -4f, 0f, -2f, 8f, 12f, 4f, Grow: grow));
        }
        else
        {
            var grow = Outer + 0.1f;
            root.AddOrReplaceChild("left_leg", VanillaPartPose.Offset(2f, 12f, 0f),
                new VanillaCubeDefinition("left_leg", 0, 16, -2f, 0f, -2f, 4f, 12f, 4f, Mirror: true, Grow: grow));
            root.AddOrReplaceChild("right_leg", VanillaPartPose.Offset(-2f, 12f, 0f),
                new VanillaCubeDefinition("right_leg", 0, 16, -2f, 0f, -2f, 4f, 12f, 4f, Grow: grow));
        }

        return new VanillaMeshDefinition(root);
    }

    private static EntityModelDefinition Build(string name, int slot) =>
        EntityModelDefinition.FromTree(
            "zombie_villager_armor_" + name + "_raw",
            64,
            32,
            CreateMesh(slot),
            faceTransforms: null,
            texturePath: null,
            baselinePx: 0f,
            rawSourceFrame: true,
            sourceFrameBaselinePx: 0f);
}
