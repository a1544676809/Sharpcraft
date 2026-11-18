// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// ArmorStandModel.createBodyLayer raw body: head/body/limbs, two body sticks, shoulder stick and base plate.
/// The hat cube inherited from HumanoidModel is hidden by the source constructor; arms/base plate visibility
/// follows DATA_CLIENT_FLAGS. The small layer is the same mesh with HumanoidModel.BABY_TRANSFORMER.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.object.armorstand.ArmorStandModel", "ref://mc-26.3-analysis/src/net/minecraft/client/model/object/armorstand/ArmorStandModel.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public static class ArmorStandModelDefinition
{
    /// <summary>BuiltInRegistries.ENTITY_TYPE armor_stand id (161-entry 26.3 registry).</summary>
    public const int ArmorStandEntityTypeId = 5;

    public static readonly EntityModelDefinition Body = EntityModelDefinition.FromTree(
        "armor_stand",
        64,
        64,
        BuildBodyMesh(),
        faceTransforms: null,
        texturePath: "entity/armorstand/armorstand",
        baselinePx: 0f,
        rawSourceFrame: true,
        sourceFrameBaselinePx: 0f);

    public static readonly EntityModelDefinition SmallBody = EntityModelDefinition.FromTree(
        "armor_stand_small",
        64,
        64,
        ApplyBabyTransformer(BuildBodyMesh()),
        faceTransforms: null,
        texturePath: "entity/armorstand/armorstand",
        baselinePx: 0f,
        rawSourceFrame: true,
        sourceFrameBaselinePx: 0f);

    private static VanillaMeshDefinition BuildBodyMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        var head = root.AddOrReplaceChild("head", VanillaPartPose.Offset(0f, 1f, 0f),
            new VanillaCubeDefinition("head", 0, 0, -1f, -7f, -1f, 2f, 7f, 2f));
        head.AddOrReplaceChild("hat", VanillaPartPose.Zero,
            new VanillaCubeDefinition("hat", 32, 0, -4f, -8f, -4f, 8f, 8f, 8f, Grow: 0.5f,
                VisibleWhen: static state => state.ShowHat));
        root.AddOrReplaceChild("left_body_stick", VanillaPartPose.Zero,
            new VanillaCubeDefinition("left_body_stick", 48, 16, 1f, 3f, -1f, 2f, 7f, 2f));
        root.AddOrReplaceChild("right_arm", VanillaPartPose.Offset(-5f, 2f, 0f),
            new VanillaCubeDefinition("right_arm", 24, 0, -2f, -2f, -1f, 2f, 12f, 2f,
                VisibleWhen: static state => state.ShowArms));
        root.AddOrReplaceChild("left_leg", VanillaPartPose.Offset(1.9f, 12f, 0f),
            new VanillaCubeDefinition("left_leg", 40, 16, -1f, 0f, -1f, 2f, 11f, 2f, Mirror: true));
        root.AddOrReplaceChild("left_arm", VanillaPartPose.Offset(5f, 2f, 0f),
            new VanillaCubeDefinition("left_arm", 32, 16, 0f, -2f, -1f, 2f, 12f, 2f, Mirror: true,
                VisibleWhen: static state => state.ShowArms));
        root.AddOrReplaceChild("right_leg", VanillaPartPose.Offset(-1.9f, 12f, 0f),
            new VanillaCubeDefinition("right_leg", 8, 0, -1f, 0f, -1f, 2f, 11f, 2f));
        root.AddOrReplaceChild("shoulder_stick", VanillaPartPose.Zero,
            new VanillaCubeDefinition("shoulder_stick", 0, 48, -4f, 10f, -1f, 8f, 2f, 2f));
        root.AddOrReplaceChild("right_body_stick", VanillaPartPose.Zero,
            new VanillaCubeDefinition("right_body_stick", 16, 0, -3f, 3f, -1f, 2f, 7f, 2f));
        root.AddOrReplaceChild("base_plate", VanillaPartPose.Offset(0f, 12f, 0f),
            new VanillaCubeDefinition("base_plate", 0, 32, -6f, 11f, -6f, 12f, 1f, 12f,
                VisibleWhen: static state => state.ShowBasePlate));
        root.AddOrReplaceChild("body", VanillaPartPose.Zero,
            new VanillaCubeDefinition("body", 0, 26, -6f, 0f, -1.5f, 12f, 3f, 3f));
        return new VanillaMeshDefinition(root);
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
}
