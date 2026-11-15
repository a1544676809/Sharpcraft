// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// The scaled bake is deliberately restricted to the static Dragon skull shape (no rotations, no root
/// MirrorX and modelHeight 0). Unsupported inputs must fail loudly instead of producing a misleading
/// generic result.
/// </summary>
public sealed class VanillaMeshScaledBakeTests
{
    [Fact]
    public void DragonShapedScaledSubtreeBakesInSourceCoordinates()
    {
        var parts = VanillaMeshBake.Bake(ScaledMesh(), 64, 64, modelHeight: 0f);

        Assert.Single(parts);
        Assert.Equal("head", parts[0].Name);
        Assert.Equal(0f, parts[0].CentreX, 3);
        Assert.Equal(-11.99f, parts[0].Bottom, 3);
        Assert.Equal(6f, parts[0].Width, 3);
        Assert.Equal(6f, parts[0].Height, 3);
        Assert.Equal(6f, parts[0].Depth, 3);
        Assert.Equal(0f, parts[0].CentreZ, 3);
    }

    [Fact]
    public void RotatedScaledSubtreeIsRejected()
    {
        var mesh = ScaledMesh(rotationX: 0.1f);
        var exception = Assert.Throws<NotSupportedException>(() => VanillaMeshBake.Bake(mesh, 64, 64, modelHeight: 0f));
        Assert.Contains("rotation", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RootMirroredScaledSubtreeIsRejected()
    {
        var mesh = ScaledMesh(mirrorX: true);
        var exception = Assert.Throws<NotSupportedException>(() => VanillaMeshBake.Bake(mesh, 64, 64, modelHeight: 0f));
        Assert.Contains("MirrorX", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NonZeroModelHeightScaledSubtreeIsRejected()
    {
        var exception = Assert.Throws<NotSupportedException>(() => VanillaMeshBake.Bake(ScaledMesh(), 64, 64, modelHeight: 24f));
        Assert.Contains("modelHeight", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ZeroRootScaleIsRejected()
    {
        var exception = Assert.Throws<NotSupportedException>(() => VanillaMeshBake.Bake(ZeroScaleMesh(), 64, 64, modelHeight: 0f));
        Assert.Contains("Zero PartPose scale", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ZeroNestedChildScaleIsRejected()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        var head = root.AddOrReplaceChild(
            "head",
            VanillaPartPose.Zero,
            new VanillaCubeDefinition("head", 0, 0, -4f, -8f, -4f, 8f, 8f, 8f, MirrorX: false));
        head.AddOrReplaceChild(
            "child",
            VanillaPartPose.Zero.Scaled(0f),
            new VanillaCubeDefinition("child", 0, 0, -1f, -1f, -1f, 2f, 2f, 2f, MirrorX: false));

        var exception = Assert.Throws<NotSupportedException>(() => VanillaMeshBake.Bake(new VanillaMeshDefinition(root), 64, 64, modelHeight: 0f));
        Assert.Contains("Zero PartPose scale", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GrowAndOverlayScaledCuboidsAreRejected()
    {
        var grow = Assert.Throws<NotSupportedException>(() => VanillaMeshBake.Bake(ScaledMesh(grow: 0.25f), 64, 64, modelHeight: 0f));
        Assert.Contains("grow/overlay", grow.Message, StringComparison.Ordinal);

        var overlays = new[] { new EntityModelLayer("hat", 0.25f, 0, 0) };
        var overlay = Assert.Throws<NotSupportedException>(() => VanillaMeshBake.Bake(ScaledMesh(overlays: overlays), 64, 64, modelHeight: 0f));
        Assert.Contains("grow/overlay", overlay.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CompiledTreeBakeryRejectsNonUnitScale()
    {
        var exception = Assert.Throws<NotSupportedException>(() => VanillaMeshBakery.Build(ScaledMesh(), 64, 64));
        Assert.Contains("scale 1", exception.Message, StringComparison.Ordinal);
    }

    private static VanillaMeshDefinition ScaledMesh(float rotationX = 0f, bool mirrorX = false, float grow = 0f, IReadOnlyList<EntityModelLayer>? overlays = null)
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        root.AddOrReplaceChild(
            "head",
            VanillaPartPose.OffsetAndRotation(0f, -7.986666f, 0f, rotationX, 0f, 0f).Scaled(0.75f),
            new VanillaCubeDefinition("head", 0, 0, -4f, -8f, -4f, 8f, 8f, 8f, Grow: grow, MirrorX: mirrorX, Overlays: overlays));
        return new VanillaMeshDefinition(root);
    }

    private static VanillaMeshDefinition ZeroScaleMesh()
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        root.AddOrReplaceChild(
            "head",
            VanillaPartPose.Zero.Scaled(0f),
            new VanillaCubeDefinition("head", 0, 0, -4f, -8f, -4f, 8f, 8f, 8f, MirrorX: false));
        return new VanillaMeshDefinition(root);
    }
}
