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
    public void CompiledTreeBakeryRejectsNonUnitScale()
    {
        var exception = Assert.Throws<NotSupportedException>(() => VanillaMeshBakery.Build(ScaledMesh(), 64, 64));
        Assert.Contains("scale 1", exception.Message, StringComparison.Ordinal);
    }

    private static VanillaMeshDefinition ScaledMesh(float rotationX = 0f, bool mirrorX = false)
    {
        var root = new VanillaPartDefinition("root", VanillaPartPose.Zero);
        root.AddOrReplaceChild(
            "head",
            VanillaPartPose.OffsetAndRotation(0f, -7.986666f, 0f, rotationX, 0f, 0f).Scaled(0.75f),
            new VanillaCubeDefinition("head", 0, 0, -4f, -8f, -4f, 8f, 8f, 8f, MirrorX: mirrorX));
        return new VanillaMeshDefinition(root);
    }
}
