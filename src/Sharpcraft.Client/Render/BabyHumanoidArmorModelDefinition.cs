// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Raw source-frame baby humanoid armor layers. Baby outer = (-0.1, 0.5, 0.3), baby inner =
/// (-0.1, 0.3, 0.3); legs/waist/foot cubes apply the source CubeDeformation.extend(-0.1). The
/// baby armor mesh is shared by humanoid baby families (zombie/villager/...); baby piglin has
/// its own 0.7 deformation and is not covered here.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.HumanoidModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/HumanoidModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "Baby humanoid armor slot raw source-frame meshes; baby piglin/material layers separate")]
public static class BabyHumanoidArmorModelDefinition
{
    public static readonly Vector3 Outer = new(-0.1f, 0.5f, 0.3f);
    public static readonly Vector3 Inner = new(-0.1f, 0.3f, 0.3f);
    private static readonly Vector3 Inset = new(-0.1f);
    private static readonly Vector3 OuterInset = Outer + Inset;
    private static readonly Vector3 InnerInset = Inner + Inset;

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

    private static EntityModelDefinition Build(string name, int slot)
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        if (slot == 5)
        {
            root.AddOrReplaceChild("head", VanillaPartPose.Offset(0f, 15f, 0f),
                new VanillaCubeDefinition("head", 0, 0, -4.5f, -7f, -4.5f, 9f, 8f, 8f, GrowXyz: Outer));
        }
        else if (slot == 4)
        {
            root.AddOrReplaceChild("right_arm", VanillaPartPose.Offset(-3.5f, 15.5f, 0f),
                new VanillaCubeDefinition("right_arm", 30, 25, -1f, 0f, -1.53f, 2f, 5f, 3f, GrowXyz: Outer));
            root.AddOrReplaceChild("left_arm", VanillaPartPose.Offset(3.5f, 15.5f, 0f),
                new VanillaCubeDefinition("left_arm", 30, 17, -1f, 0f, -1.53f, 2f, 5f, 3f, GrowXyz: Outer));
            root.AddOrReplaceChild("body", VanillaPartPose.Offset(0f, 18f, 0f),
                new VanillaCubeDefinition("body", 0, 17, -3f, -3f, -1.5f, 6f, 5f, 3f, GrowXyz: Outer));
        }
        else if (slot == 3)
        {
            root.AddOrReplaceChild("left_leg", VanillaPartPose.Offset(1.5f, 20f, 0.5f),
                new VanillaCubeDefinition("left_leg", 18, 24, -2f, -0.2f, -2f, 3f, 4f, 3f, GrowXyz: InnerInset));
            root.AddOrReplaceChild("right_leg", VanillaPartPose.Offset(-1.5f, 20f, 0.5f),
                new VanillaCubeDefinition("right_leg", 18, 17, -1f, -0.2f, -2f, 3f, 4f, 3f, GrowXyz: InnerInset));
            root.AddOrReplaceChild("waist", VanillaPartPose.Offset(0f, 19f, 0f),
                new VanillaCubeDefinition("waist", 0, 36, -3f, -1.2f, -1.49f, 5.9f, 2f, 2.9f, GrowXyz: InnerInset));
        }
        else
        {
            var leftLeg = root.AddOrReplaceChild("left_leg", VanillaPartPose.Offset(1.5f, 20f, 0.5f));
            leftLeg.AddOrReplaceChild("right_foot", VanillaPartPose.Zero,
                new VanillaCubeDefinition("right_foot", 0, 25, -2f, 2.9f, -2f, 3f, 1f, 3f, GrowXyz: Outer));
            var rightLeg = root.AddOrReplaceChild("right_leg", VanillaPartPose.Offset(-1.5f, 20f, 0.5f));
            rightLeg.AddOrReplaceChild("left_foot", VanillaPartPose.Zero,
                new VanillaCubeDefinition("left_foot", 0, 29, -1f, 2.9f, -2f, 3f, 1f, 3f,
                    Mirror: true, GrowXyz: Outer));
        }

        return EntityModelDefinition.FromTree(
            "baby_humanoid_armor_" + name + "_raw",
            64,
            64,
            new VanillaMeshDefinition(root),
            faceTransforms: null,
            texturePath: null,
            baselinePx: 0f,
            rawSourceFrame: true,
            sourceFrameBaselinePx: 0f);
    }
}
