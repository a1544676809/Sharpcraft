// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Centralization step 2 (T-669): pins that <c>VanillaMeshBake.BakeRoot</c> / <c>VanillaMeshBake.BakeChild</c>
/// reproduce the exact y-down → y-up conversion that <c>PlayerModelDefinition.FromVanilla</c> /
/// <c>FromVanillaChild</c> now delegate to. The delegating helpers must stay byte-equivalent for every existing
/// caller, so both the full record equality and the individual converted fields are asserted here.
/// </summary>
public sealed class VanillaMeshBakeConversionTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BakeRootMatchesFromVanillaForMirroredAndNonMirrored(bool mirrorX)
    {
        var fromDefinition = PlayerModelDefinition.FromVanilla(
            "root_sample", 3f, 5f, 2f, -2f, -3f, -1f, 4f, 6f, 8f, 10, 20,
            mirror: false, mirrorX: mirrorX, overlays: null, modelHeight: 24f,
            rotationX: 0.1f, rotationY: 0.2f, rotationZ: 0.3f,
            texturePath: "entity/sample", grow: 0.5f, kind: EntityGeometryLayerKind.Cutout);

        var baked = VanillaMeshBake.BakeRoot(
            "root_sample", 3f, 5f, 2f, -2f, -3f, -1f, 4f, 6f, 8f, 10, 20,
            mirror: false, mirrorX: mirrorX, overlays: null, modelHeight: 24f,
            rotationX: 0.1f, rotationY: 0.2f, rotationZ: 0.3f,
            texturePath: "entity/sample", grow: 0.5f, kind: EntityGeometryLayerKind.Cutout);

        // Full record equality covers every field (geometry, pivots, rotations, overlays, selectors, metadata).
        Assert.Equal(fromDefinition, baked);

        // Independently computed expectations (modelHeight 24): sourceCentreX = 3 + (-2) + 2 = 3.
        Assert.Equal(mirrorX ? -3f : 3f, baked.CentreX);
        Assert.Equal(16f, baked.Bottom);             // 24 - (5 + (-3) + 6)
        Assert.Equal(5f, baked.CentreZ);             // 2 + (-1) + 4
        Assert.Equal(mirrorX ? -3f : 3f, baked.PivotX);
        Assert.Equal(19f, baked.PivotY);             // 24 - 5 (not rounded)
        Assert.Equal(2f, baked.PivotZ);
        // Root rotations are passed through unchanged (no sign flip).
        Assert.Equal(0.1f, baked.RotationX);
        Assert.Equal(0.2f, baked.RotationY);
        Assert.Equal(0.3f, baked.RotationZ);
        Assert.Equal(4f, baked.Width);
        Assert.Equal(6f, baked.Height);
        Assert.Equal(8f, baked.Depth);
        Assert.Equal(10, baked.TextureX);
        Assert.Equal(20, baked.TextureY);
        Assert.Equal("entity/sample", baked.TexturePath);
        Assert.Equal(0.5f, baked.Grow);
    }

    [Fact]
    public void BakeChildMatchesFromVanillaChildForRotatedSample()
    {
        const string parent = "body";
        var fromDefinition = PlayerModelDefinition.FromVanillaChild(
            "child_sample", parent, 2f, 4f, 1f, -2f, -5f, -1f, 6f, 4f, 4f, 8, 16,
            mirror: false, overlays: null,
            rotationX: 0.3f, rotationY: -0.4f, rotationZ: 0.5f,
            texturePath: "entity/child", grow: 0.25f, kind: EntityGeometryLayerKind.Cutout);

        var baked = VanillaMeshBake.BakeChild(
            "child_sample", parent, 2f, 4f, 1f, -2f, -5f, -1f, 6f, 4f, 4f, 8, 16,
            mirror: false, overlays: null,
            rotationX: 0.3f, rotationY: -0.4f, rotationZ: 0.5f,
            texturePath: "entity/child", grow: 0.25f, kind: EntityGeometryLayerKind.Cutout);

        // Full record equality covers every field, including the parent name.
        Assert.Equal(fromDefinition, baked);

        // Independently computed expectations (child keeps cubes local to its parent; no model-height offset).
        Assert.Equal(-1f, baked.CentreX);            // -(-2 + 6/2)
        Assert.Equal(1f, baked.Bottom);              // -(-5 + 4)
        Assert.Equal(1f, baked.CentreZ);             // -1 + 4/2
        Assert.Equal(-2f, baked.PivotX);             // -pivotX
        Assert.Equal(-4f, baked.PivotY);             // -pivotY
        Assert.Equal(1f, baked.PivotZ);              // pivotZ (no rounding)
        // Child X/Y rotations negate, Z is unchanged.
        Assert.Equal(-0.3f, baked.RotationX);
        Assert.Equal(0.4f, baked.RotationY);         // -(-0.4)
        Assert.Equal(0.5f, baked.RotationZ);
        Assert.Equal(6f, baked.Width);
        Assert.Equal(4f, baked.Height);
        Assert.Equal(4f, baked.Depth);
        Assert.Equal(8, baked.TextureX);
        Assert.Equal(16, baked.TextureY);
        Assert.Equal(parent, baked.Parent);
        Assert.Equal("entity/child", baked.TexturePath);
        Assert.Equal(0.25f, baked.Grow);
    }

    /// <summary>
    /// Optional registry-wide sweep (T-669, item 3). Baked registry parts no longer retain their raw vanilla inputs,
    /// so a direct re-bake comparison is impossible for arbitrary parts; instead every part produced by the now-single
    /// conversion path is checked for finite converted coordinates/rotations and a non-empty name. This validates the
    /// centralized bake's arithmetic across all of <c>EntityModelResolver.Definitions</c> as the breadth complement to
    /// the two byte-equivalence samples above.
    /// </summary>
    [Fact]
    public void AllRegistryPartsAreFiniteAfterCentralizedBake()
    {
        var definitions = EntityModelResolver.Definitions;
        Assert.NotEmpty(definitions);

        var partCount = 0;
        foreach (var definition in definitions)
        {
            foreach (var part in definition.Parts)
            {
                partCount++;
                Assert.False(string.IsNullOrEmpty(part.Name));
                Assert.True(float.IsFinite(part.CentreX));
                Assert.True(float.IsFinite(part.Bottom));
                Assert.True(float.IsFinite(part.CentreZ));
                Assert.True(float.IsFinite(part.PivotX));
                Assert.True(float.IsFinite(part.PivotY));
                Assert.True(float.IsFinite(part.PivotZ));
                Assert.True(float.IsFinite(part.RotationX));
                Assert.True(float.IsFinite(part.RotationY));
                Assert.True(float.IsFinite(part.RotationZ));
            }
        }

        Assert.True(partCount > 0);
    }
}
