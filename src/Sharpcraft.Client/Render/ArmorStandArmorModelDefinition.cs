// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// ArmorStandArmorModel.createArmorLayerSet: the humanoid armor set built on ArmorStandArmorModel's base
/// mesh (head pivot y=1, legs y=11, independent hat). The small variant applies HumanoidModel.BABY_TRANSFORMER:
/// root "head" gets translated y=16 then scaled 0.75, every other root child translated y=24 then scaled 0.5.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.object.armorstand.ArmorStandArmorModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/object/armorstand/ArmorStandArmorModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class ArmorStandArmorModelDefinition
{
    public static readonly EntityModelDefinition Helmet = Build(5, small: false);
    public static readonly EntityModelDefinition Chestplate = Build(4, small: false);
    public static readonly EntityModelDefinition Leggings = Build(3, small: false);
    public static readonly EntityModelDefinition Boots = Build(2, small: false);
    public static readonly EntityModelDefinition SmallHelmet = Build(5, small: true);
    public static readonly EntityModelDefinition SmallChestplate = Build(4, small: true);
    public static readonly EntityModelDefinition SmallLeggings = Build(3, small: true);
    public static readonly EntityModelDefinition SmallBoots = Build(2, small: true);

    public static EntityModelDefinition ForSlot(int slot, bool small) => (small, slot) switch
    {
        (false, 5) => Helmet,
        (false, 4) => Chestplate,
        (false, 3) => Leggings,
        (false, 2) => Boots,
        (true, 5) => SmallHelmet,
        (true, 4) => SmallChestplate,
        (true, 3) => SmallLeggings,
        (true, 2) => SmallBoots,
        _ => throw new ArgumentOutOfRangeException(nameof(slot))
    };

    private static EntityModelDefinition Build(int slot, bool small)
    {
        var mesh = PlayerArmorModelDefinition.CreateMesh(slot, headY: 1f, legY: 11f);
        if (small)
        {
            mesh = ApplyBabyTransformer(mesh);
        }

        return EntityModelDefinition.FromTree(
            (small ? "armor_stand_small_armor_" : "armor_stand_armor_") + SlotName(slot) + "_raw",
            64,
            32,
            mesh,
            faceTransforms: null,
            texturePath: null,
            baselinePx: 0f,
            rawSourceFrame: true,
            sourceFrameBaselinePx: 0f);
    }

    private static VanillaMeshDefinition ApplyBabyTransformer(VanillaMeshDefinition mesh)
    {
        var root = mesh.Root!;
        var children = root.Children.Select(child =>
        {
            var isHead = child.Name == "head";
            var scale = isHead ? 0.75f : 0.5f;
            var translateY = isHead ? 16f : 24f;
            var scaled = ScaleSubtree(child, scale);
            var pose = scaled.Pose;
            return new VanillaPartDefinition(
                scaled.Name,
                new VanillaPartPose(pose.X, pose.Y + translateY * scale, pose.Z,
                    pose.RotationX, pose.RotationY, pose.RotationZ),
                scaled.Cubes,
                scaled.Children);
        }).ToArray();
        return new VanillaMeshDefinition(new VanillaPartDefinition(root.Name, root.Pose, root.Cubes, children));
    }

    private static VanillaPartDefinition ScaleSubtree(VanillaPartDefinition source, float scale)
    {
        var pose = source.Pose;
        var cubes = source.Cubes.Select(cube => cube with
        {
            MinX = cube.MinX * scale,
            MinY = cube.MinY * scale,
            MinZ = cube.MinZ * scale,
            Width = cube.Width * scale,
            Height = cube.Height * scale,
            Depth = cube.Depth * scale,
            Grow = cube.Grow * scale,
            GrowXyz = cube.GrowXyz is { } grow ? grow * scale : null,
            UvWidth = cube.UvWidth > 0 ? cube.UvWidth : (int)cube.Width,
            UvHeight = cube.UvHeight > 0 ? cube.UvHeight : (int)cube.Height,
            UvDepth = cube.UvDepth > 0 ? cube.UvDepth : (int)cube.Depth
        }).ToArray();
        return new VanillaPartDefinition(
            source.Name,
            new VanillaPartPose(pose.X * scale, pose.Y * scale, pose.Z * scale,
                pose.RotationX, pose.RotationY, pose.RotationZ),
            cubes,
            source.Children.Select(child => ScaleSubtree(child, scale)).ToArray());
    }

    private static string SlotName(int slot) => slot switch
    {
        5 => "helmet",
        4 => "chestplate",
        3 => "leggings",
        _ => "boots"
    };
}
