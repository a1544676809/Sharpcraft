// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>Full-cube smooth light (LightCoordsUtil.smoothBlend), docs/analysis/render/lighting-color-space.md #3.</summary>
public sealed class SmoothLightingTests
{
    [Fact]
    public void UniformCellsAverageToThemselves()
    {
        var (sky, block) = SmoothLighting.Blend(10, 5, 10, 5, 10, 5, 10, 5);
        Assert.Equal(10, sky);
        Assert.Equal(5, block);
    }

    [Fact]
    public void BrightCenterReplacesFullyDarkNeighbours()
    {
        // Centre sky 15 > 2, all neighbours have no light at all -> they take the centre.
        var (sky, block) = SmoothLighting.Blend(15, 0, 0, 0, 0, 0, 0, 0);
        Assert.Equal(15, sky);
        Assert.Equal(0, block);
    }

    [Fact]
    public void NeighbourInheritsOnlyTheSkyWhenItHasBlockLight()
    {
        // Centre sky 15; neighbour1 has block light 7 but sky 0 -> its sky takes the centre's, block stays 7 (>>2 = 1).
        var (sky, block) = SmoothLighting.Blend(15, 0, 0, 7, 15, 0, 15, 0);
        Assert.Equal(15, sky);
        Assert.Equal(1, block);
    }

    [Fact]
    public void DimCentreLeavesNeighboursUnchanged()
    {
        // Centre sky 2 (not > 2): no correction, plain average.
        var (sky, block) = SmoothLighting.Blend(2, 0, 0, 0, 0, 0, 0, 0);
        Assert.Equal(0, sky);
        Assert.Equal(0, block);
    }
}
