// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// Giant armor is the shared humanoid armor MeshTransformer.scaling(6.0): pivot/cube uniform scale
/// 6 + source yOffset 24.016*(1-6). UV dimensions stay at the source cube dimensions.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.HumanoidModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/HumanoidModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "Giant armor = humanoid armor MeshTransformer.scaling(6.0), baked statically")]
public static class GiantArmorModelDefinition
{
    private const float Factor = 6f;
    private static readonly float YOffset = 24.016f * (1f - Factor);

    public static readonly EntityModelDefinition Helmet = Build(5);
    public static readonly EntityModelDefinition Chestplate = Build(4);
    public static readonly EntityModelDefinition Leggings = Build(3);
    public static readonly EntityModelDefinition Boots = Build(2);

    public static EntityModelDefinition ForSlot(int slot) => slot switch
    {
        5 => Helmet,
        4 => Chestplate,
        3 => Leggings,
        2 => Boots,
        _ => throw new ArgumentOutOfRangeException(nameof(slot))
    };

    private static EntityModelDefinition Build(int slot)
    {
        var scaled = ScalePart(PlayerArmorModelDefinition.CreateMesh(slot).Root!, depth: 0);
        var order = slot switch
        {
            4 => new[] { "right_arm", "body", "left_arm" },
            3 => new[] { "left_leg", "body", "right_leg" },
            _ => scaled.Children.Select(child => child.Name).ToArray()
        };
        var byName = scaled.Children.ToDictionary(child => child.Name);
        var root = new VanillaPartDefinition(
            scaled.Name, VanillaPartPose.Zero, scaled.Cubes,
            order.Select(name => byName[name]).ToArray());
        return EntityModelDefinition.FromTree(
            "giant_armor_" + SlotName(slot) + "_raw", 64, 32,
            new VanillaMeshDefinition(root),
            faceTransforms: null, texturePath: null,
            baselinePx: 0f, rawSourceFrame: true, sourceFrameBaselinePx: 0f);
    }

    private static VanillaPartDefinition ScalePart(VanillaPartDefinition source, int depth)
    {
        var pose = source.Pose;
        var cubes = source.Cubes.Select(cube => cube with
        {
            MinX = cube.MinX * Factor,
            MinY = cube.MinY * Factor,
            MinZ = cube.MinZ * Factor,
            Width = cube.Width * Factor,
            Height = cube.Height * Factor,
            Depth = cube.Depth * Factor,
            Grow = cube.Grow * Factor,
            GrowXyz = cube.GrowXyz is { } grow ? grow * Factor : null,
            UvWidth = cube.UvWidth > 0 ? cube.UvWidth : (int)cube.Width,
            UvHeight = cube.UvHeight > 0 ? cube.UvHeight : (int)cube.Height,
            UvDepth = cube.UvDepth > 0 ? cube.UvDepth : (int)cube.Depth
        }).ToArray();
        return new VanillaPartDefinition(
            source.Name,
            new VanillaPartPose(
                pose.X * Factor,
                pose.Y * Factor + (depth == 1 ? YOffset : 0f),
                pose.Z * Factor,
                pose.RotationX,
                pose.RotationY,
                pose.RotationZ),
            cubes,
            source.Children.Select(child => ScalePart(child, depth + 1)).ToArray());
    }

    private static string SlotName(int slot) => slot switch
    {
        5 => "helmet",
        4 => "chestplate",
        3 => "leggings",
        _ => "boots"
    };
}
