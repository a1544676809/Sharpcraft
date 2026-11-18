// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Raw source-frame Piglin armor layers. Adult: PlayerModel armor mesh with outer deformation 1.02
/// and empty ear groups (nose is not part of the armor mesh). Baby: PlayerModel baby armor mesh with
/// uniform 0.7 deformation, armOffset (0.5, -0.5, 0), and empty ear groups.
/// Piglin/ZombifiedPiglin share these meshes; PIGLIN_BRUTE_ARMOR uses the same source set.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.monster.piglin.PiglinModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/monster/piglin/PiglinModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "Piglin adult/baby armor raw source-frame meshes; removeEars empty groups retained, nose not in armor mesh")]
public static class PiglinArmorModelDefinition
{
    public static readonly EntityModelDefinition AdultHelmet = BuildAdult(5);
    public static readonly EntityModelDefinition AdultChestplate = BuildAdult(4);
    public static readonly EntityModelDefinition AdultLeggings = BuildAdult(3);
    public static readonly EntityModelDefinition AdultBoots = BuildAdult(2);
    public static readonly EntityModelDefinition BabyHelmet = BuildBaby(5);
    public static readonly EntityModelDefinition BabyChestplate = BuildBaby(4);
    public static readonly EntityModelDefinition BabyLeggings = BuildBaby(3);
    public static readonly EntityModelDefinition BabyBoots = BuildBaby(2);

    public static EntityModelDefinition ForSlot(int slot, bool baby) => (baby, slot) switch
    {
        (true, 5) => BabyHelmet,
        (true, 4) => BabyChestplate,
        (true, 3) => BabyLeggings,
        (true, 2) => BabyBoots,
        (false, 5) => AdultHelmet,
        (false, 4) => AdultChestplate,
        (false, 3) => AdultLeggings,
        (false, 2) => AdultBoots,
        _ => throw new ArgumentOutOfRangeException(nameof(slot))
    };

    private static EntityModelDefinition BuildAdult(int slot)
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        if (slot == 5)
        {
            var head = root.AddOrReplaceChild("head", VanillaPartPose.Zero,
                new VanillaCubeDefinition("head", 0, 0, -4f, -8f, -4f, 8f, 8f, 8f, Grow: 1.02f));
            head.AddOrReplaceChild("hat", VanillaPartPose.Zero,
                new VanillaCubeDefinition("hat", 32, 0, -4f, -8f, -4f, 8f, 8f, 8f, Grow: 1.52f));
            head.AddOrReplaceChild("left_ear", VanillaPartPose.Zero);
            head.AddOrReplaceChild("right_ear", VanillaPartPose.Zero);
        }
        else if (slot == 4)
        {
            root.AddOrReplaceChild("right_arm", VanillaPartPose.Offset(-5f, 2f, 0f),
                new VanillaCubeDefinition("right_arm", 40, 16, -3f, -2f, -2f, 4f, 12f, 4f, Grow: 1.02f));
            root.AddOrReplaceChild("left_arm", VanillaPartPose.Offset(5f, 2f, 0f),
                new VanillaCubeDefinition("left_arm", 40, 16, -1f, -2f, -2f, 4f, 12f, 4f, Mirror: true, Grow: 1.02f));
            root.AddOrReplaceChild("body", VanillaPartPose.Zero,
                new VanillaCubeDefinition("body", 16, 16, -4f, 0f, -2f, 8f, 12f, 4f, Grow: 1.02f));
        }
        else
        {
            var grow = (slot == 3 ? 0.5f : 1.02f) - 0.1f;
            root.AddOrReplaceChild("left_leg", VanillaPartPose.Offset(1.9f, 12f, 0f),
                new VanillaCubeDefinition("left_leg", 0, 16, -2f, 0f, -2f, 4f, 12f, 4f, Mirror: true, Grow: grow));
            root.AddOrReplaceChild("right_leg", VanillaPartPose.Offset(-1.9f, 12f, 0f),
                new VanillaCubeDefinition("right_leg", 0, 16, -2f, 0f, -2f, 4f, 12f, 4f, Grow: grow));
            if (slot == 3)
            {
                root.AddOrReplaceChild("body", VanillaPartPose.Zero,
                    new VanillaCubeDefinition("body", 16, 16, -4f, 0f, -2f, 8f, 12f, 4f, Grow: 0.5f));
            }
        }

        return EntityModelDefinition.FromTree("piglin_armor_" + SlotName(slot) + "_raw", 64, 32,
            new VanillaMeshDefinition(root), faceTransforms: null, texturePath: null,
            baselinePx: 0f, rawSourceFrame: true, sourceFrameBaselinePx: 0f);
    }

    private static EntityModelDefinition BuildBaby(int slot)
    {
        const float deformation = 0.7f;
        const float inset = 0.6f; // CubeDeformation(0.7).extend(-0.1)
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        if (slot == 5)
        {
            var head = root.AddOrReplaceChild("head", VanillaPartPose.Offset(0f, 15f, 0f),
                new VanillaCubeDefinition("head", 0, 0, -4.5f, -7f, -4.5f, 9f, 8f, 8f, Grow: deformation));
            head.AddOrReplaceChild("left_ear", VanillaPartPose.Zero);
            head.AddOrReplaceChild("right_ear", VanillaPartPose.Zero);
        }
        else if (slot == 4)
        {
            root.AddOrReplaceChild("right_arm", VanillaPartPose.Offset(-4f, 15f, 0f),
                new VanillaCubeDefinition("right_arm", 30, 25, -1f, 0f, -1.53f, 2f, 5f, 3f, Grow: deformation));
            root.AddOrReplaceChild("left_arm", VanillaPartPose.Offset(4f, 15f, 0f),
                new VanillaCubeDefinition("left_arm", 30, 17, -1f, 0f, -1.53f, 2f, 5f, 3f, Grow: deformation));
            root.AddOrReplaceChild("body", VanillaPartPose.Offset(0f, 18f, 0f),
                new VanillaCubeDefinition("body", 0, 17, -3f, -3f, -1.5f, 6f, 5f, 3f, Grow: deformation));
        }
        else if (slot == 3)
        {
            root.AddOrReplaceChild("left_leg", VanillaPartPose.Offset(1.5f, 20f, 0.5f),
                new VanillaCubeDefinition("left_leg", 18, 24, -2f, -0.2f, -2f, 3f, 4f, 3f, Grow: inset));
            root.AddOrReplaceChild("right_leg", VanillaPartPose.Offset(-1.5f, 20f, 0.5f),
                new VanillaCubeDefinition("right_leg", 18, 17, -1f, -0.2f, -2f, 3f, 4f, 3f, Grow: inset));
            root.AddOrReplaceChild("waist", VanillaPartPose.Offset(0f, 19f, 0f),
                new VanillaCubeDefinition("waist", 0, 36, -3f, -1.2f, -1.49f, 5.9f, 2f, 2.9f, Grow: inset));
        }
        else
        {
            var leftLeg = root.AddOrReplaceChild("left_leg", VanillaPartPose.Offset(1.5f, 20f, 0.5f));
            leftLeg.AddOrReplaceChild("right_foot", VanillaPartPose.Zero,
                new VanillaCubeDefinition("right_foot", 0, 25, -2f, 2.9f, -2f, 3f, 1f, 3f, Grow: deformation));
            var rightLeg = root.AddOrReplaceChild("right_leg", VanillaPartPose.Offset(-1.5f, 20f, 0.5f));
            rightLeg.AddOrReplaceChild("left_foot", VanillaPartPose.Zero,
                new VanillaCubeDefinition("left_foot", 0, 29, -1f, 2.9f, -2f, 3f, 1f, 3f,
                    Mirror: true, Grow: deformation));
        }

        return EntityModelDefinition.FromTree("piglin_baby_armor_" + SlotName(slot) + "_raw", 64, 64,
            new VanillaMeshDefinition(root), faceTransforms: null, texturePath: null,
            baselinePx: 0f, rawSourceFrame: true, sourceFrameBaselinePx: 0f);
    }

    private static string SlotName(int slot) => slot switch
    {
        5 => "helmet",
        4 => "chestplate",
        3 => "leggings",
        _ => "boots"
    };
}
