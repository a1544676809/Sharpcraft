// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// Equine armor layers: HORSE_ARMOR is AbstractEquineModel.createBodyMesh(grow 0.1) then
/// MeshTransformer.scaling(1.1); UNDEAD_HORSE_ARMOR is the same mesh with no living scale. The body box keeps
/// the source hard-coded 0.05 grow and the ears keep -0.001.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.animal.equine.AbstractEquineModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/animal/equine/AbstractEquineModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class HorseArmorModelDefinition
{
    private const float LivingScale = 1.1f;
    private static readonly float LivingYOffset = 24.016f * (1f - LivingScale);

    public static readonly EntityModelDefinition Living = Build(living: true);
    public static readonly EntityModelDefinition Undead = Build(living: false);

    private static EntityModelDefinition Build(bool living)
    {
        var mesh = HorseModelDefinition.BuildBodyMesh(0.1f);
        if (living)
        {
            mesh = Scale(mesh.Root!, depth: 0);
        }

        return EntityModelDefinition.FromTree(
            living ? "horse_armor_raw" : "undead_horse_armor_raw",
            64,
            64,
            Reorder(mesh),
            faceTransforms: null,
            texturePath: null,
            baselinePx: 0f,
            rawSourceFrame: true,
            sourceFrameBaselinePx: 0f);
    }

    private static VanillaMeshDefinition Scale(VanillaPartDefinition root, int depth)
    {
        var scaled = ScalePart(root, depth);
        return new VanillaMeshDefinition(scaled);
    }

    private static VanillaPartDefinition ScalePart(VanillaPartDefinition source, int depth)
    {
        var pose = source.Pose;
        var cubes = source.Cubes.Select(cube => cube with
        {
            MinX = cube.MinX * LivingScale,
            MinY = cube.MinY * LivingScale,
            MinZ = cube.MinZ * LivingScale,
            Width = cube.Width * LivingScale,
            Height = cube.Height * LivingScale,
            Depth = cube.Depth * LivingScale,
            Grow = cube.Grow * LivingScale,
            GrowXyz = cube.GrowXyz is { } grow ? grow * LivingScale : null,
            UvWidth = cube.UvWidth > 0 ? cube.UvWidth : (int)cube.Width,
            UvHeight = cube.UvHeight > 0 ? cube.UvHeight : (int)cube.Height,
            UvDepth = cube.UvDepth > 0 ? cube.UvDepth : (int)cube.Depth
        }).ToArray();
        return new VanillaPartDefinition(
            source.Name,
            new VanillaPartPose(
                pose.X * LivingScale,
                pose.Y * LivingScale + (depth == 1 ? LivingYOffset : 0f),
                pose.Z * LivingScale,
                pose.RotationX, pose.RotationY, pose.RotationZ),
            cubes,
            source.Children.Select(child => ScalePart(child, depth + 1)).ToArray());
    }

    private static VanillaMeshDefinition Reorder(VanillaMeshDefinition mesh)
    {
        var root = mesh.Root!;
        var rootByName = root.Children.ToDictionary(child => child.Name);
        rootByName["head_parts"] = ReorderChildren(
            rootByName["head_parts"], ["head", "mane", "upper_mouth"]);
        var headParts = rootByName["head_parts"];
        var head = headParts.GetChild("head")!;
        rootByName["head_parts"] = new VanillaPartDefinition(
            headParts.Name, headParts.Pose, headParts.Cubes,
            headParts.Children.Select(child => child.Name == "head"
                ? ReorderChildren(child, ["right_ear", "left_ear"])
                : child).ToArray());
        string[] order =
        [
            "right_front_leg", "right_hind_leg", "head_parts", "left_hind_leg",
            "body", "left_front_leg"
        ];
        return new VanillaMeshDefinition(new VanillaPartDefinition(
            root.Name, root.Pose, root.Cubes, order.Select(name => rootByName[name]).ToArray()));
    }

    private static VanillaPartDefinition ReorderChildren(VanillaPartDefinition part, string[] order)
    {
        var byName = part.Children.ToDictionary(child => child.Name);
        return new VanillaPartDefinition(
            part.Name, part.Pose, part.Cubes, order.Select(name => byName[name]).ToArray());
    }
}
