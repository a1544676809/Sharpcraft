// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Test-only bridge from already-baked flat <see cref="EntityModelPart"/> values to a
/// <see cref="CompiledEntityModel"/> tree consumed by the production compiled mesher. It copies the baked
/// part data verbatim (pivot/rotation/cube/UV/mirror/face mask/selectors) into a root-level node per part,
/// preserving each part's Parent string so Emit's base-pivot subtraction matches the legacy flat path.
/// No meshing maths are reimplemented here: the production compiled Emit path produces the vertices, so a
/// jar source sample remains an independent oracle for the test. Verified byte-identical to the former flat
/// path for root parts and for nested parts (e.g. villager nose->head) and with non-unit layer scale.
/// </summary>
internal static class CompiledPartHelper
{
    public static EntityModelDefinition Definition(
        string name,
        int width,
        int height,
        IReadOnlyList<EntityModelPart> parts,
        string? texturePath,
        float scale = 1f,
        float sourceFrameBaselinePx = 0f,
        bool rawSourceFrame = true,
        float baselinePx = 0f,
        IReadOnlyDictionary<string, FaceUvTransform>? faceTransforms = null)
    {
        var nodes = new List<EntityModelPartNode>(parts.Count);
        var cubes = new List<EntityModelCube>(parts.Count);
        foreach (var part in parts)
        {
            var cube = new EntityModelCube(
                part.Name, part.CentreX, part.Bottom, part.CentreZ, part.Width, part.Height, part.Depth,
                part.TextureX, part.TextureY, part.Mirror, part.OverlayLayers, part.TexturePath, part.Grow,
                part.GrowXyz, part.Kind, part.UvWidth, part.UvHeight, part.UvDepth, part.TextureWidth,
                part.TextureHeight, part.IncludeInEmissive, part.EmissiveOnly, part.EmissiveTexturePath,
                part.TextureSelector, part.UsesItemAtlas, part.VisibleSides);
            // Preserve the baked part's Parent verbatim. Emit subtracts the base pivot only for nodes
            // whose Parent is null; the flat path does the same. All nodes attach directly to the
            // synthetic root so emission order equals the flat partOrder and parent frames are not
            // double-applied (verified byte-identical for root and nested parts).
            var node = new EntityModelPartNode(
                part.Name, part.Parent, part.PivotX, part.PivotY, part.PivotZ, part.RotationX, part.RotationY,
                part.RotationZ, [cube], [], visibleWhen: part.VisibleWhen,
                rotationSelector: part.RotationSelector, yOffsetSelector: part.YOffsetSelector,
                poseSource: part.PoseSource, tintSelector: part.TintSelector);
            nodes.Add(node);
            cubes.Add(cube);
        }

        var root = new EntityModelPartNode("root", null, 0f, 0f, 0f, 0f, 0f, 0f, [], nodes);
        var compiled = new CompiledEntityModel(root, nodes, cubes);
        return new EntityModelDefinition(
            name, width, height, [], faceTransforms: faceTransforms, texturePath, scale: scale,
            compiled: compiled, baselinePx: baselinePx, rawSourceFrame: rawSourceFrame,
            sourceFrameBaselinePx: sourceFrameBaselinePx);
    }

    public static EntityModelDefinition Definition(
        string name, int width, int height, EntityModelPart part, string? texturePath, float scale = 1f) =>
        Definition(name, width, height, [part], texturePath, scale);

    /// <summary>
    /// Like <see cref="Definition"/> but wires real parent-child links so Emit composes ancestor frames
    /// (needed for parts whose parent pivot/rotation is non-identity). Parts are keyed by name; any part
    /// whose Parent is absent from the set is treated as a root child. Used with source-identity vertex
    /// matching, so the compiled Emit order (parent before child) is not assumed.
    /// </summary>
    public static EntityModelDefinition DefinitionNested(
        string name,
        int width,
        int height,
        IReadOnlyList<EntityModelPart> parts,
        string? texturePath,
        float scale = 1f,
        float sourceFrameBaselinePx = 0f)
    {
        var cubes = new List<EntityModelCube>(parts.Count);
        var byName = new Dictionary<string, EntityModelPart>(StringComparer.Ordinal);
        foreach (var part in parts)
        {
            var cube = new EntityModelCube(
                part.Name, part.CentreX, part.Bottom, part.CentreZ, part.Width, part.Height, part.Depth,
                part.TextureX, part.TextureY, part.Mirror, part.OverlayLayers, part.TexturePath, part.Grow,
                part.GrowXyz, part.Kind, part.UvWidth, part.UvHeight, part.UvDepth, part.TextureWidth,
                part.TextureHeight, part.IncludeInEmissive, part.EmissiveOnly, part.EmissiveTexturePath,
                part.TextureSelector, part.UsesItemAtlas, part.VisibleSides);
            cubes.Add(cube);
            byName[part.Name] = part;
        }

        // Build a node and its full descendant subtree recursively, so multi-level chains compose.
        EntityModelPartNode Build(string partName)
        {
            var part = byName[partName];
            var cube = cubes.First(c => c.Name == partName);
            var children = parts.Where(p => p.Parent == partName).Select(p => Build(p.Name)).ToArray();
            return new EntityModelPartNode(
                part.Name, part.Parent, part.PivotX, part.PivotY, part.PivotZ, part.RotationX, part.RotationY,
                part.RotationZ, [cube], children, visibleWhen: part.VisibleWhen,
                rotationSelector: part.RotationSelector, yOffsetSelector: part.YOffsetSelector,
                poseSource: part.PoseSource, tintSelector: part.TintSelector);
        }

        var rootChildren = parts.Where(p => p.Parent is null || !byName.ContainsKey(p.Parent))
            .Select(p => Build(p.Name)).ToArray();
        var root = new EntityModelPartNode("root", null, 0f, 0f, 0f, 0f, 0f, 0f, [], rootChildren);
        var orderedNodes = new List<EntityModelPartNode>();
        void Collect(EntityModelPartNode n) { orderedNodes.Add(n); foreach (var c in n.ChildArray) Collect(c); }
        foreach (var rc in rootChildren) Collect(rc);
        var compiled = new CompiledEntityModel(root, orderedNodes, cubes);
        return new EntityModelDefinition(
            name, width, height, [], faceTransforms: null, texturePath, scale: scale,
            compiled: compiled, baselinePx: 0f, rawSourceFrame: true,
            sourceFrameBaselinePx: sourceFrameBaselinePx);
    }

    /// <summary>Rebuild a legacy (y-up, rawSourceFrame:false) definition with a compiled tree for the now
    /// compiled-only mesher, preserving its baked geometry, baseline and face transforms verbatim.</summary>
    public static EntityModelDefinition Legacy(EntityModelDefinition legacy)
    {
        // Legacy baked parts may carry Parent links (Wither rib_N -> ribcage); wire them as a real tree
        // so the compiled Emit composes the same ancestor frames the legacy flat path did.
        var cubes = new List<EntityModelCube>(legacy.Parts.Count);
        var byName = new Dictionary<string, EntityModelPart>(StringComparer.Ordinal);
        foreach (var part in legacy.Parts)
        {
            cubes.Add(new EntityModelCube(
                part.Name, part.CentreX, part.Bottom, part.CentreZ, part.Width, part.Height, part.Depth,
                part.TextureX, part.TextureY, part.Mirror, part.OverlayLayers, part.TexturePath, part.Grow,
                part.GrowXyz, part.Kind, part.UvWidth, part.UvHeight, part.UvDepth, part.TextureWidth,
                part.TextureHeight, part.IncludeInEmissive, part.EmissiveOnly, part.EmissiveTexturePath,
                part.TextureSelector, part.UsesItemAtlas, part.VisibleSides));
            byName[part.Name] = part;
        }

        EntityModelPartNode Build(string partName)
        {
            var part = byName[partName];
            var cube = cubes.First(c => c.Name == partName);
            var children = legacy.Parts.Where(p => p.Parent == partName).Select(p => Build(p.Name)).ToArray();
            return new EntityModelPartNode(
                part.Name, part.Parent, part.PivotX, part.PivotY, part.PivotZ, part.RotationX, part.RotationY,
                part.RotationZ, [cube], children, visibleWhen: part.VisibleWhen);
        }

        var rootChildren = legacy.Parts.Where(p => p.Parent is null || !byName.ContainsKey(p.Parent))
            .Select(p => Build(p.Name)).ToArray();
        var root = new EntityModelPartNode("root", null, 0f, 0f, 0f, 0f, 0f, 0f, [], rootChildren);
        var ordered = new List<EntityModelPartNode>();
        void Collect(EntityModelPartNode n) { ordered.Add(n); foreach (var c in n.ChildArray) Collect(c); }
        foreach (var rc in rootChildren) Collect(rc);
        var compiled = new CompiledEntityModel(root, ordered, cubes);
        return new EntityModelDefinition(
            legacy.Name, legacy.TextureWidth, legacy.TextureHeight, [], faceTransforms: legacy.FaceTransforms,
            legacy.TexturePath, scale: legacy.Scale, compiled: compiled,
            baselinePx: legacy.BaselinePx, rawSourceFrame: false);
    }
}
