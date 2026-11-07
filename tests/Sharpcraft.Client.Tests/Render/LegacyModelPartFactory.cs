// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Test-only legacy oracle: builds flattened <see cref="EntityModelPart"/> values from the reference y-down pivot/cube
/// form using the centralized <see cref="VanillaMeshBake"/> bake. The production model definitions use the DSL;
/// migration tests use this independent factory to prove value equivalence.
/// </summary>
internal static class LegacyModelPartFactory
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
        float modelHeight = 24f,
        float rotationX = 0f,
        float rotationY = 0f,
        float rotationZ = 0f,
        string? texturePath = null,
        float grow = 0f,
        EntityGeometryLayerKind kind = EntityGeometryLayerKind.Cutout)
    {
        return VanillaMeshBake.BakeRoot(
            name,
            pivotX,
            pivotY,
            pivotZ,
            minX,
            minY,
            minZ,
            width,
            height,
            depth,
            textureX,
            textureY,
            mirror,
            mirrorX,
            overlays,
            modelHeight,
            rotationX,
            rotationY,
            rotationZ,
            texturePath,
            grow,
            kind);
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
        return VanillaMeshBake.BakeChild(
            name,
            parent,
            pivotX,
            pivotY,
            pivotZ,
            minX,
            minY,
            minZ,
            width,
            height,
            depth,
            textureX,
            textureY,
            mirror,
            overlays,
            rotationX,
            rotationY,
            rotationZ,
            texturePath,
            grow,
            kind);
    }
}
