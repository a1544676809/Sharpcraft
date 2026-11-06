// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Render;

/// <summary>
/// A part's pivot and bind-pose rotations in the reference y-down model frame (the analogue of reference
/// <c>PartPose</c>). Conversion to y-up geometry is always delegated to the centralized <c>VanillaMeshBake</c>.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.geom.PartPose", "ref://mc-26.3-analysis/src/net/minecraft/client/model/geom/PartPose.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "Part pivot/rotation DSL value in the y-down reference frame; converted by VanillaMeshBake")]
public readonly record struct VanillaPartPose(float X, float Y, float Z, float RotationX = 0f, float RotationY = 0f, float RotationZ = 0f)
{
    /// <summary>Identity pose: pivot at the origin, no rotation.</summary>
    public static VanillaPartPose Zero => default;

    /// <summary>A pure translation with no rotation.</summary>
    public static VanillaPartPose Offset(float x, float y, float z) => new(x, y, z);

    /// <summary>A translation combined with the three bind-pose rotations.</summary>
    public static VanillaPartPose OffsetAndRotation(float x, float y, float z, float rx, float ry, float rz) => new(x, y, z, rx, ry, rz);
}

/// <summary>
/// One cuboid of a part in reference coordinates. <c>Name</c> becomes the flattened <c>EntityModelPart.Name</c>; a
/// part whose descendants need a parent link should name its primary cuboid after the part itself. An optional
/// <c>PoseSource</c> names the base part whose pose this cuboid follows (used by top-level overlay layers). The
/// emissive flags and texture dimensions are per-cube overrides; a non-zero <c>TextureWidth</c>/<c>TextureHeight</c>
/// takes precedence over the mesh-level dimensions passed to <c>VanillaMeshBake.Bake</c>.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.geom.builders.CubeDefinition", "ref://mc-26.3-analysis/src/net/minecraft/client/model/geom/builders/CubeDefinition.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "Declarative cuboid DSL value; baked into EntityModelPart by VanillaMeshBake")]
public readonly record struct VanillaCubeDefinition(
    string Name,
    int TextureX,
    int TextureY,
    float MinX,
    float MinY,
    float MinZ,
    float Width,
    float Height,
    float Depth,
    float Grow = 0f,
    bool Mirror = false,
    IReadOnlyList<EntityModelLayer>? Overlays = null,
    string? TexturePath = null,
    EntityGeometryLayerKind Kind = EntityGeometryLayerKind.Cutout,
    string? PoseSource = null,
    bool IncludeInEmissive = true,
    bool EmissiveOnly = false,
    int TextureWidth = 0,
    int TextureHeight = 0);

/// <summary>
/// One node of the declarative model tree. Direct children of <c>VanillaMeshDefinition.Root</c> are root parts;
/// deeper descendants are child parts whose parent link is this node's <see cref="Name"/>.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.geom.builders.PartDefinition", "ref://mc-26.3-analysis/src/net/minecraft/client/model/geom/builders/PartDefinition.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "Declarative part-tree node; flattened by VanillaMeshBake.Bake")]
public sealed class VanillaPartDefinition
{
    private readonly List<VanillaCubeDefinition> _cubes;
    private readonly List<VanillaPartDefinition> _children;

    public VanillaPartDefinition(string name, VanillaPartPose pose, IEnumerable<VanillaCubeDefinition>? cubes = null, IEnumerable<VanillaPartDefinition>? children = null)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("A part name is required.", nameof(name));
        }

        Name = name;
        Pose = pose;
        _cubes = cubes is null ? new List<VanillaCubeDefinition>() : new List<VanillaCubeDefinition>(cubes);
        _children = children is null ? new List<VanillaPartDefinition>() : new List<VanillaPartDefinition>(children);
    }

    public string Name { get; }
    public VanillaPartPose Pose { get; }
    public IReadOnlyList<VanillaCubeDefinition> Cubes => _cubes;
    public IReadOnlyList<VanillaPartDefinition> Children => _children;

    /// <summary>
    /// Adds a child part. Despite the reference method name, replacement is intentionally rejected: in the flattened
    /// model a part name is the parent-link key for its descendants, so a repeated name is malformed input and throws.
    /// </summary>
    public VanillaPartDefinition AddOrReplaceChild(string name, VanillaPartPose pose, params VanillaCubeDefinition[] cubes)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("A child part name is required.", nameof(name));
        }

        if (GetChild(name) is not null)
        {
            throw new InvalidOperationException($"Duplicate child name '{name}' under part '{Name}'; part names are parent-link keys and must be unique.");
        }

        var child = new VanillaPartDefinition(name, pose, cubes);
        _children.Add(child);
        return child;
    }

    /// <summary>Looks up a direct child by name, or null when absent.</summary>
    public VanillaPartDefinition? GetChild(string name)
    {
        foreach (var child in _children)
        {
            if (string.Equals(child.Name, name, StringComparison.Ordinal))
            {
                return child;
            }
        }

        return null;
    }

    /// <summary>Appends a cuboid to this part and returns the same node for fluent chaining.</summary>
    public VanillaPartDefinition AddCube(VanillaCubeDefinition cube)
    {
        _cubes.Add(cube);
        return this;
    }
}

/// <summary>
/// Root of a declarative vanilla-style mesh. Mirrors reference <c>MeshDefinition</c>: <see cref="Root"/> is a group
/// whose children become the model's root parts. Convert to flattened geometry with <c>VanillaMeshBake.Bake</c>.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.geom.builders.MeshDefinition", "ref://mc-26.3-analysis/src/net/minecraft/client/model/geom/builders/MeshDefinition.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "Root of the declarative mesh DSL; baked to an EntityModelPart array via VanillaMeshBake.Bake")]
public sealed class VanillaMeshDefinition
{
    public VanillaMeshDefinition(VanillaPartDefinition? root = null)
    {
        Root = root;
    }

    /// <summary>The root group. A null root is malformed input and is rejected by <c>VanillaMeshBake.Bake</c>.</summary>
    public VanillaPartDefinition? Root { get; }
}

/// <summary>
/// Centralized bake for the declarative DSL (T-670). Declared as a partial of <c>VanillaMeshBake</c> so all conversion
/// lives in one type while the DSL values live in this file.
/// </summary>
static partial class VanillaMeshBake
{
    /// <summary>
    /// Flattens <paramref name="mesh"/> into parts in reference (pre-order) order. Direct children of the root group
    /// are baked via <c>BakeRoot</c>; deeper descendants via <c>BakeChild</c> with their parent's name. The mesh-level
    /// texture path and dimensions are inherited by every part unless a cuboid overrides the path.
    /// </summary>
    public static IReadOnlyList<EntityModelPart> Bake(
        VanillaMeshDefinition mesh,
        int textureWidth,
        int textureHeight,
        float modelHeight = 24f,
        string? texturePath = null)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        var root = mesh.Root
            ?? throw new InvalidOperationException("VanillaMeshDefinition has no root part; declare one before baking.");

        var parts = new List<EntityModelPart>();
        var producedNames = new HashSet<string>(StringComparer.Ordinal);

        // The root group's own cuboids, if any, are root-level boxes.
        EmitCubes(parts, producedNames, root, parentName: null, modelHeight, texturePath, textureWidth, textureHeight);
        foreach (var child in root.Children)
        {
            Walk(parts, producedNames, child, parentName: null, modelHeight, texturePath, textureWidth, textureHeight);
        }

        return parts;
    }

    private static void Walk(
        List<EntityModelPart> parts,
        HashSet<string> producedNames,
        VanillaPartDefinition part,
        string? parentName,
        float modelHeight,
        string? texturePath,
        int textureWidth,
        int textureHeight)
    {
        EmitCubes(parts, producedNames, part, parentName, modelHeight, texturePath, textureWidth, textureHeight);

        var siblingNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var child in part.Children)
        {
            if (!siblingNames.Add(child.Name))
            {
                throw new InvalidOperationException($"Duplicate child name '{child.Name}' under part '{part.Name}'; part names are parent-link keys and must be unique.");
            }

            Walk(parts, producedNames, child, part.Name, modelHeight, texturePath, textureWidth, textureHeight);
        }
    }

    private static void EmitCubes(
        List<EntityModelPart> parts,
        HashSet<string> producedNames,
        VanillaPartDefinition part,
        string? parentName,
        float modelHeight,
        string? texturePath,
        int textureWidth,
        int textureHeight)
    {
        foreach (var cube in part.Cubes)
        {
            if (!producedNames.Add(cube.Name))
            {
                throw new InvalidOperationException($"Duplicate part name '{cube.Name}' while baking a VanillaMeshDefinition; part names must be unique.");
            }

            var effectiveTexturePath = cube.TexturePath ?? texturePath;
            EntityModelPart baked = parentName is null
                ? BakeRoot(
                    cube.Name,
                    part.Pose.X, part.Pose.Y, part.Pose.Z,
                    cube.MinX, cube.MinY, cube.MinZ,
                    cube.Width, cube.Height, cube.Depth,
                    cube.TextureX, cube.TextureY,
                    mirror: cube.Mirror, overlays: cube.Overlays, modelHeight: modelHeight,
                    rotationX: part.Pose.RotationX, rotationY: part.Pose.RotationY, rotationZ: part.Pose.RotationZ,
                    texturePath: effectiveTexturePath, grow: cube.Grow, kind: cube.Kind)
                : BakeChild(
                    cube.Name, parentName,
                    part.Pose.X, part.Pose.Y, part.Pose.Z,
                    cube.MinX, cube.MinY, cube.MinZ,
                    cube.Width, cube.Height, cube.Depth,
                    cube.TextureX, cube.TextureY,
                    mirror: cube.Mirror, overlays: cube.Overlays,
                    rotationX: part.Pose.RotationX, rotationY: part.Pose.RotationY, rotationZ: part.Pose.RotationZ,
                    texturePath: effectiveTexturePath, grow: cube.Grow, kind: cube.Kind);

            if (cube.PoseSource is not null)
            {
                baked = baked with { PoseSource = cube.PoseSource };
            }

            baked = baked with
            {
                IncludeInEmissive = cube.IncludeInEmissive,
                EmissiveOnly = cube.EmissiveOnly
            };

            // A non-zero per-cube texture dimension wins over the mesh-level Bake dimension.
            var effectiveWidth = cube.TextureWidth > 0 ? cube.TextureWidth : textureWidth;
            var effectiveHeight = cube.TextureHeight > 0 ? cube.TextureHeight : textureHeight;
            parts.Add(effectiveWidth == 0 && effectiveHeight == 0
                ? baked
                : baked with { TextureWidth = effectiveWidth, TextureHeight = effectiveHeight });
        }
    }
}
