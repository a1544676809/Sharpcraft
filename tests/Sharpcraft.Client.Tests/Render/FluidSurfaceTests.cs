// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Fluid surface math equivalence (docs/specs/render/fluid-rendering.md 2-3). Expected values are worked out by hand
/// from the vanilla calculateAverageHeight/addWeightedHeight rules.
/// </summary>
public sealed class FluidSurfaceTests
{
    [Fact]
    public void SourceHeightIsEightNinths()
    {
        Assert.Equal(8f / 9f, FluidSurface.OwnHeight(8), 5);
        Assert.Equal(4f / 9f, FluidSurface.OwnHeight(4), 5);
        Assert.Equal(1f / 9f, FluidSurface.OwnHeight(1), 5);
    }

    [Fact]
    public void FullEdgeNeighbourLiftsTheCornerToOne()
    {
        Assert.Equal(1f, FluidSurface.AverageCorner(0.5f, 1f, 0.2f, 0.2f), 5);
    }

    [Fact]
    public void UniformHeightAveragesToItself()
    {
        Assert.Equal(0.5f, FluidSurface.AverageCorner(0.5f, 0.5f, 0.5f, 0.5f), 5);
        Assert.Equal(4f / 9f, FluidSurface.AverageCorner(4f / 9f, 4f / 9f, 4f / 9f, 4f / 9f), 5);
    }

    [Fact]
    public void RaisedSelfIsWeightedTenTimes()
    {
        // corner .2 + self .9*10 + n .2 + e .2 = 9.6 over weights 1+10+1+1 = 13.
        Assert.Equal(9.6f / 13f, FluidSurface.AverageCorner(0.9f, 0.2f, 0.2f, 0.2f), 5);
    }

    [Fact]
    public void SolidNeighboursAreIgnored()
    {
        // Both edges solid (-1): only self contributes.
        Assert.Equal(0.4f, FluidSurface.AverageCorner(0.4f, -1f, -1f, -1f), 5);

        // One edge fluid, the other solid: corner and solid edge are dropped, average of self and the fluid edge.
        Assert.Equal(0.3f, FluidSurface.AverageCorner(0.4f, 0.2f, -1f, -1f), 5);
    }

    [Fact]
    public void FullDiagonalColumnLiftsTheCornerToOne()
    {
        // Reference calculateAverageHeight: inside the "an edge has fluid" branch, a diagonal of height >= 1 returns 1
        // directly instead of being averaged - this is what let a flowing block's corner meet a full diagonal column.
        Assert.Equal(1f, FluidSurface.AverageCorner(0.5f, 0.5f, 0.5f, 1f), 5);
    }
}
