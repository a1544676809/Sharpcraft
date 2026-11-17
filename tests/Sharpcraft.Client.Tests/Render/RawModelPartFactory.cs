// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Independent source oracle for raw source-frame models. This does NOT call any production bake
/// (<c>VanillaMeshBake</c>/<c>VanillaMeshBakery</c>); it constructs the expected <see cref="EntityModelPart"/>
/// directly from the reference y-down DSL literals using formulas written out here. The formulas mirror the
/// documented raw-frame semantics: a root part's cube centre is the pivot plus the cube min plus half the
/// size; a child part's cube centre is the cube min plus half the size (local to its parent frame); pivots and
/// rotations are the source literals verbatim. A bug in the production raw bake therefore cannot silently
/// change both expected and actual. Values fed here come from the reference model definitions, cross-checked
/// by the ModelProbe reference samples and <c>vanilla-model-facts.json</c>.
/// </summary>
internal static class RawModelPartFactory
{
    public static EntityModelPart FromVanilla(
        string name,
        float pivotX,
        float pivotY,
        float pivotZ,
        float minX,
        float minY,
        float minZ,
        float width,
        float height,
        float depth,
        int textureX,
        int textureY,
        bool mirror = false,
        bool mirrorX = true,
        IReadOnlyList<EntityModelLayer>? overlays = null,
        float rotationX = 0f,
        float rotationY = 0f,
        float rotationZ = 0f,
        string? texturePath = null,
        float grow = 0f,
        EntityGeometryLayerKind kind = EntityGeometryLayerKind.Cutout)
    {
        // Root raw formula: centre = pivot + min + halfSize, bottom = pivotY + minY.
        return new EntityModelPart(
            name,
            pivotX + minX + (width * 0.5f),
            pivotY + minY,
            width,
            height,
            depth,
            textureX,
            textureY,
            mirror,
            overlays,
            CentreZ: pivotZ + minZ + (depth * 0.5f),
            PivotX: pivotX,
            PivotY: pivotY,
            PivotZ: pivotZ,
            RotationX: rotationX,
            RotationY: rotationY,
            RotationZ: rotationZ,
            TexturePath: texturePath,
            Grow: grow,
            Kind: kind);
    }

    public static EntityModelPart FromVanillaChild(
        string name,
        string parent,
        float pivotX,
        float pivotY,
        float pivotZ,
        float minX,
        float minY,
        float minZ,
        float width,
        float height,
        float depth,
        int textureX,
        int textureY,
        bool mirror = false,
        IReadOnlyList<EntityModelLayer>? overlays = null,
        float rotationX = 0f,
        float rotationY = 0f,
        float rotationZ = 0f,
        string? texturePath = null,
        float grow = 0f,
        EntityGeometryLayerKind kind = EntityGeometryLayerKind.Cutout)
    {
        // Child raw formula: centre = min + halfSize (parent-local), bottom = minY. Pivots/rotations literal.
        return new EntityModelPart(
            name,
            minX + (width * 0.5f),
            minY,
            width,
            height,
            depth,
            textureX,
            textureY,
            mirror,
            overlays,
            CentreZ: minZ + (depth * 0.5f),
            PivotX: pivotX,
            PivotY: pivotY,
            PivotZ: pivotZ,
            RotationX: rotationX,
            RotationY: rotationY,
            RotationZ: rotationZ,
            TexturePath: texturePath,
            Grow: grow,
            Parent: parent,
            Kind: kind);
    }
}
