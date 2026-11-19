// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Test-only structural view over a production <see cref="CompiledEntityModel"/>: projects each compiled node+cube
/// into a flat <see cref="EntityModelPart"/> descriptor (node pose/parent + cube geometry) so structural bake tests
/// keep their name/parent/visibility/selector assertions after <c>EntityModelDefinition.Parts</c> was removed (#231).
/// Lives only in the test assembly, never in Sharpcraft.Client. NOT source-equivalence evidence — jar keyed samples
/// in <see cref="LivingRawReferenceSampleTests"/> remain the primary evidence.
/// </summary>
internal static class CompiledStructuralView
{
    public static IReadOnlyList<EntityModelPart> Of(CompiledEntityModel compiled)
    {
        var list = new List<EntityModelPart>(compiled.Cubes.Count);
        foreach (var node in compiled.Parts)
        {
            if (node.CubeArray.Length == 0)
            {
                list.Add(new EntityModelPart(
                    Name: node.Name, CentreX: 0f, Bottom: 0f, Width: 0f, Height: 0f, Depth: 0f,
                    PivotX: node.PivotX, PivotY: node.PivotY, PivotZ: node.PivotZ,
                    RotationX: node.RotationX, RotationY: node.RotationY, RotationZ: node.RotationZ,
                    Parent: node.Parent, VisibleWhen: node.VisibleWhen, PoseSource: node.PoseSource,
                    TintSelector: node.TintSelector, RotationSelector: node.RotationSelector,
                    YOffsetSelector: node.YOffsetSelector));
                continue;
            }

            foreach (var cube in node.CubeArray)
            {
                list.Add(new EntityModelPart(
                    Name: cube.Name,
                    CentreX: cube.CentreX, Bottom: cube.Bottom, CentreZ: cube.CentreZ,
                    Width: cube.Width, Height: cube.Height, Depth: cube.Depth,
                    TextureX: cube.TextureX, TextureY: cube.TextureY, Mirror: cube.Mirror,
                    OverlayLayers: cube.Overlays, TexturePath: cube.TexturePath, Grow: cube.Grow,
                    GrowXyz: cube.GrowXyz,
                    PivotX: node.PivotX, PivotY: node.PivotY, PivotZ: node.PivotZ,
                    RotationX: node.RotationX, RotationY: node.RotationY, RotationZ: node.RotationZ,
                    Parent: node.Parent, Kind: cube.Kind, VisibleWhen: node.VisibleWhen,
                    TextureWidth: cube.TextureWidth, TextureHeight: cube.TextureHeight,
                    IncludeInEmissive: cube.IncludeInEmissive, EmissiveOnly: cube.EmissiveOnly,
                    EmissiveTexturePath: cube.EmissiveTexturePath,
                    TintSelector: node.TintSelector, TextureSelector: cube.TextureSelector,
                    PoseSource: node.PoseSource, RotationSelector: node.RotationSelector,
                    YOffsetSelector: node.YOffsetSelector,
                    UvWidth: cube.UvWidth, UvHeight: cube.UvHeight, UvDepth: cube.UvDepth,
                    UsesItemAtlas: cube.UsesItemAtlas, VisibleSides: cube.VisibleSides));
            }
        }

        return list;
    }
}

internal static class CompiledStructuralViewExtensions
{
    public static IReadOnlyList<EntityModelPart> StructuralParts(this EntityModelDefinition definition) =>
        CompiledStructuralView.Of(definition.Compiled ??
            throw new InvalidOperationException($"definition '{definition.Name}' has no compiled tree"));

    public static IReadOnlyList<EntityModelPart> StructuralParts(this CompiledEntityModel compiled) =>
        CompiledStructuralView.Of(compiled);
}
