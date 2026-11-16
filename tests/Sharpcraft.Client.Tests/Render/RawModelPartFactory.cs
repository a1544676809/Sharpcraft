// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Source-derived oracle for raw source-frame models. Unlike <see cref="LegacyModelPartFactory"/>, which
/// converts reference y-down values into the legacy baked y-up frame, this factory keeps the reference
/// y-down pivot/cube values verbatim (the raw bake is an identity mapping of the source frame). The
/// source literals fed to it are taken from the reference model definitions, cross-checked against
/// <c>vanilla-model-facts.json</c> and the ModelProbe samples under
/// <c>ref://mc-26.3-analysis/vectors/</c>. This is a structural source oracle, never fed by the
/// production mesher's output.
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
            rotationX: rotationX,
            rotationY: rotationY,
            rotationZ: rotationZ,
            texturePath: texturePath,
            grow: grow,
            kind: kind,
            rawSourceFrame: true);
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
            kind,
            rawSourceFrame: true);
    }
}
