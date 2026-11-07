// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Central tree-preserving bake. This is the bridge from the declarative <see cref="VanillaMeshDefinition"/> tree to the
/// structural runtime model: one <see cref="EntityModelPartNode"/> per reference part, one or more
/// <see cref="EntityModelCube"/> per node, and zero-cube group nodes retained. The legacy one-cube-per-part
/// <c>VanillaMeshBake.Bake</c> path remains only for existing callers until migration completes.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.geom.EntityModelSet", "ref://mc-26.3-analysis/src/net/minecraft/client/model/geom/EntityModelSet.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Adapter, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "Tree-preserving baker; legacy flat bake remains during migration")]
public static class VanillaMeshBakery
{
    public static CompiledEntityModel Build(
        VanillaMeshDefinition mesh,
        int textureWidth = 0,
        int textureHeight = 0,
        float modelHeight = 24f,
        string? texturePath = null)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        var root = mesh.Root
            ?? throw new InvalidOperationException("VanillaMeshDefinition has no root part; declare one before baking.");

        var rootNode = BuildNode(root, parentName: null, isRootGroup: true, textureWidth, textureHeight, modelHeight, texturePath);
        var parts = new List<EntityModelPartNode>();
        var cubes = new List<EntityModelCube>();
        Collect(rootNode, parts, cubes);
        return new CompiledEntityModel(rootNode, parts, cubes);
    }

    private static EntityModelPartNode BuildNode(
        VanillaPartDefinition part,
        string? parentName,
        bool isRootGroup,
        int textureWidth,
        int textureHeight,
        float modelHeight,
        string? texturePath)
    {
        var cubes = new List<EntityModelCube>(part.Cubes.Count);
        foreach (var cube in part.Cubes)
        {
            if (cube.Width == 0f && cube.Height == 0f && cube.Depth == 0f)
            {
                // The DSL uses a zero-size cube as the marker for a zero-cube group node (for example Breeze
                // "body"/"rods" and Armadillo "head"). It is metadata, not drawable geometry.
                continue;
            }

            var baked = BakeCube(part, cube, parentName, modelHeight);
            cubes.Add(new EntityModelCube(
                cube.Name,
                baked.CentreX,
                baked.Bottom,
                baked.CentreZ,
                baked.Width,
                baked.Height,
                baked.Depth,
                baked.TextureX,
                baked.TextureY,
                baked.Mirror,
                cube.Overlays,
                cube.TexturePath,
                cube.Grow,
                cube.GrowXyz,
                cube.Kind,
                TextureWidth: cube.TextureWidth > 0 ? cube.TextureWidth : textureWidth,
                TextureHeight: cube.TextureHeight > 0 ? cube.TextureHeight : textureHeight,
                IncludeInEmissive: cube.IncludeInEmissive,
                EmissiveOnly: cube.EmissiveOnly));
        }

        var children = new List<EntityModelPartNode>(part.Children.Count);
        foreach (var child in part.Children)
        {
            children.Add(BuildNode(child, isRootGroup ? null : part.Name, isRootGroup: false, textureWidth, textureHeight, modelHeight, texturePath));
        }

        var template = BakeCube(part, ZeroCube, parentName, modelHeight);
        var firstCube = part.Cubes.Count > 0 ? part.Cubes[0] : (VanillaCubeDefinition?)null;
        return new EntityModelPartNode(
            part.Name,
            parentName,
            template.PivotX,
            template.PivotY,
            template.PivotZ,
            template.RotationX,
            template.RotationY,
            template.RotationZ,
            cubes,
            children,
            firstCube?.VisibleWhen,
            poseSource: firstCube?.PoseSource);
    }

    private static EntityModelPart BakeCube(
        VanillaPartDefinition part,
        VanillaCubeDefinition cube,
        string? parentName,
        float modelHeight)
    {
        if (parentName is null)
        {
            return VanillaMeshBake.BakeRoot(
                cube.Name,
                part.Pose.X, part.Pose.Y, part.Pose.Z,
                cube.MinX, cube.MinY, cube.MinZ,
                cube.Width, cube.Height, cube.Depth,
                cube.TextureX, cube.TextureY,
                mirror: cube.Mirror,
                mirrorX: cube.MirrorX,
                overlays: cube.Overlays,
                modelHeight: modelHeight,
                rotationX: part.Pose.RotationX,
                rotationY: part.Pose.RotationY,
                rotationZ: part.Pose.RotationZ,
                texturePath: cube.TexturePath,
                grow: cube.Grow,
                kind: cube.Kind);
        }

        return VanillaMeshBake.BakeChild(
            cube.Name,
            parentName,
            part.Pose.X, part.Pose.Y, part.Pose.Z,
            cube.MinX, cube.MinY, cube.MinZ,
            cube.Width, cube.Height, cube.Depth,
            cube.TextureX, cube.TextureY,
            mirror: cube.Mirror,
            overlays: cube.Overlays,
            rotationX: part.Pose.RotationX,
            rotationY: part.Pose.RotationY,
            rotationZ: part.Pose.RotationZ,
            texturePath: cube.TexturePath,
            grow: cube.Grow,
            kind: cube.Kind);
    }

    private static void Collect(
        EntityModelPartNode node,
        List<EntityModelPartNode> parts,
        List<EntityModelCube> cubes)
    {
        foreach (var child in node.Children)
        {
            parts.Add(child);
            cubes.AddRange(child.Cubes);
            Collect(child, parts, cubes);
        }
    }

    private static readonly VanillaCubeDefinition ZeroCube =
        new("__group", 0, 0, 0f, 0f, 0f, 0f, 0f, 0f);
}
