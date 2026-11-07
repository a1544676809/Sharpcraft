// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Render;

/// <summary>
/// One cuboid inside a <see cref="EntityModelPartNode"/>. Geometry is stored in y-up model pixels, like the legacy
/// <see cref="EntityModelPart"/>; unlike the legacy record, a node may own several cubes.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.geom.ModelPart.Cube", "ref://mc-26.3-analysis/src/net/minecraft/client/model/geom/ModelPart.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "Baked cuboid data inside the tree runtime model; UV and grow metadata retained per cube")]
public readonly record struct EntityModelCube(
    string Name,
    float CentreX,
    float Bottom,
    float CentreZ,
    float Width,
    float Height,
    float Depth,
    int TextureX = 0,
    int TextureY = 0,
    bool Mirror = false,
    IReadOnlyList<EntityModelLayer>? Overlays = null,
    string? TexturePath = null,
    float Grow = 0f,
    Vector3? GrowXyz = null,
    EntityGeometryLayerKind Kind = EntityGeometryLayerKind.Cutout,
    int UvWidth = 0,
    int UvHeight = 0,
    int UvDepth = 0,
    int TextureWidth = 0,
    int TextureHeight = 0,
    bool IncludeInEmissive = true,
    bool EmissiveOnly = false,
    string? EmissiveTexturePath = null);

/// <summary>
/// One node of the baked, structurally-preserving model tree. Unlike the legacy <see cref="EntityModelPart"/>, a node
/// owns a cube list and real children; zero-cube group nodes remain present.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.geom.ModelPart", "ref://mc-26.3-analysis/src/net/minecraft/client/model/geom/ModelPart.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "Tree node analogue: cube list + children + pose; replaces flattened one-cube EntityModelPart for the new bakery path")]
public sealed class EntityModelPartNode
{
    public EntityModelPartNode(
        string name,
        string? parent,
        float pivotX,
        float pivotY,
        float pivotZ,
        float rotationX,
        float rotationY,
        float rotationZ,
        IReadOnlyList<EntityModelCube> cubes,
        IReadOnlyList<EntityModelPartNode> children,
        Func<Sharpcraft.Client.World.EntityRenderState, bool>? visibleWhen = null,
        Func<Sharpcraft.Client.World.EntityRenderState, Vector3>? rotationSelector = null,
        Func<Sharpcraft.Client.World.EntityRenderState, float>? yOffsetSelector = null,
        string? poseSource = null,
        Func<Sharpcraft.Client.World.EntityRenderState, Vector3>? tintSelector = null)
    {
        Name = name;
        Parent = parent;
        PivotX = pivotX;
        PivotY = pivotY;
        PivotZ = pivotZ;
        RotationX = rotationX;
        RotationY = rotationY;
        RotationZ = rotationZ;
        Cubes = cubes;
        Children = children;
        VisibleWhen = visibleWhen;
        RotationSelector = rotationSelector;
        YOffsetSelector = yOffsetSelector;
        PoseSource = poseSource;
        TintSelector = tintSelector;
    }

    public string Name { get; }
    public string? Parent { get; }
    public float PivotX { get; }
    public float PivotY { get; }
    public float PivotZ { get; }
    public float RotationX { get; }
    public float RotationY { get; }
    public float RotationZ { get; }
    public IReadOnlyList<EntityModelCube> Cubes { get; }
    public IReadOnlyList<EntityModelPartNode> Children { get; }
    public Func<Sharpcraft.Client.World.EntityRenderState, bool>? VisibleWhen { get; }
    public Func<Sharpcraft.Client.World.EntityRenderState, Vector3>? RotationSelector { get; }
    public Func<Sharpcraft.Client.World.EntityRenderState, float>? YOffsetSelector { get; }
    public string? PoseSource { get; }
    public Func<Sharpcraft.Client.World.EntityRenderState, Vector3>? TintSelector { get; }
}

/// <summary>
/// Result of a <see cref="VanillaMeshBakery"/> build: the root group plus every structural part in reference tree order.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.client.model.geom.EntityModelSet", "ref://mc-26.3-analysis/src/net/minecraft/client/model/geom/EntityModelSet.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Adapter, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "Compiled tree model produced by the central bakery; no source tree is stored")]
public sealed class CompiledEntityModel
{
    public CompiledEntityModel(
        EntityModelPartNode root,
        IReadOnlyList<EntityModelPartNode> parts,
        IReadOnlyList<EntityModelCube> cubes)
    {
        Root = root;
        Parts = parts;
        Cubes = cubes;
    }

    public EntityModelPartNode Root { get; }
    public IReadOnlyList<EntityModelPartNode> Parts { get; }
    public IReadOnlyList<EntityModelCube> Cubes { get; }

    public EntityModelPartNode? Find(string name)
    {
        foreach (var part in Parts)
        {
            if (string.Equals(part.Name, name, StringComparison.Ordinal))
            {
                return part;
            }
        }

        return null;
    }
}
