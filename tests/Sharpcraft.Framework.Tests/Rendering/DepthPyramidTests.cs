// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Framework.Tests.Rendering;

/// <summary>
/// Depth pyramid (docs/specs/render/occlusion-culling.md, stage O2): levels halve, the farthest child wins so the occlusion
/// test stays conservative, and a rectangle is answered from the finest level whose single texel still covers it.
/// </summary>
public sealed class DepthPyramidTests
{
    [Fact]
    public void EachLevelHalvesAndStopsAtOnePixel()
    {
        var pyramid = DepthPyramid.Build(new float[8 * 4], 8, 4);

        Assert.Equal(8, pyramid.WidthOf(0));
        Assert.Equal(4, pyramid.WidthOf(1));
        Assert.Equal(1, pyramid.WidthOf(3));
        Assert.Equal(1, pyramid.HeightOf(3));
        Assert.Equal(4, pyramid.LevelCount);
    }

    [Fact]
    public void TheFarthestChildWins()
    {
        // A 2x2 base with one far corner: level one must report the farthest, so a box behind it stays culled only if the
        // whole region is that far.
        var depth = new float[] { 0.1f, 0.2f, 0.3f, 0.9f };
        var pyramid = DepthPyramid.Build(depth, 2, 2);

        Assert.Equal(0.9f, pyramid[1][0]);
    }

    [Fact]
    public void ACoveredRectangleIsAnsweredFromOneTexel()
    {
        var depth = new float[4 * 4];
        Array.Fill(depth, 0.25f);
        depth[(2 * 4) + 2] = 0.75f;
        var pyramid = DepthPyramid.Build(depth, 4, 4);

        Assert.True(pyramid.TryCoveringDepth(0.5f, 0.5f, 1.0f, 1.0f, out var farthest));
        Assert.Equal(0.75f, farthest);
    }

    [Fact]
    public void ADegenerateRectangleIsRejected()
    {
        var pyramid = DepthPyramid.Build(new float[4], 2, 2);

        Assert.False(pyramid.TryCoveringDepth(0.5f, 0.5f, 0.5f, 0.5f, out _));
    }
}
