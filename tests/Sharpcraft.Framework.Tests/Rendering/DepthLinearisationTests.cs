// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Framework.Tests.Rendering;

/// <summary>
/// Depth linearisation (docs/specs/render/occlusion-culling.md O1.7): the far field must come back as usable distances, and
/// the inverse must agree with the projection so the two cannot drift apart.
/// </summary>
public sealed class DepthLinearisationTests
{
    private const float Near = 0.1f;
    private const float Far = 2000f;

    [Fact]
    public void TheEndsMapToThePlanes()
    {
        Assert.Equal(Near, DepthLinearisation.ToDistance(0f, Near, Far), 4);
        Assert.Equal(Far, DepthLinearisation.ToDistance(1f, Near, Far), 2);
    }

    [Fact]
    public void ATypicalWindowDepthBecomesAUsableDistance()
    {
        // 0.9986 is what the recorded scene reported for terrain tens of blocks away.
        var distance = DepthLinearisation.ToDistance(0.9986f, Near, Far);

        Assert.InRange(distance, 40f, 90f);
    }

    [Fact]
    public void DepthAndDistanceAreInverses()
    {
        foreach (var distance in new[] { 1f, 16f, 64f, 200f })
        {
            var depth = DepthLinearisation.ToDepth(distance, Near, Far);
            Assert.Equal(distance, DepthLinearisation.ToDistance(depth, Near, Far), 1);
        }
    }

    [Fact]
    public void DistanceGrowsWithDepth()
    {
        Assert.True(DepthLinearisation.ToDistance(0.99f, Near, Far) > DepthLinearisation.ToDistance(0.5f, Near, Far));
    }
}
