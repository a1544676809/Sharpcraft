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
        // Reversed-Z: depth 1 is the near plane, depth 0 is the far plane.
        Assert.Equal(Near, DepthLinearisation.ToDistance(1f, Near, Far), 4);
        Assert.Equal(Far, DepthLinearisation.ToDistance(0f, Near, Far), 2);
    }

    [Fact]
    public void ATypicalWindowDepthBecomesAUsableDistance()
    {
        // Reversed-Z compresses the far field toward zero; a surface about seventy blocks away sits near 0.0014.
        var distance = DepthLinearisation.ToDistance(0.0013786f, Near, Far);

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
    public void DistanceFallsAsReversedDepthGrows()
    {
        // Reversed-Z: a larger window depth means a nearer surface, so the recovered distance must shrink.
        Assert.True(DepthLinearisation.ToDistance(0.99f, Near, Far) < DepthLinearisation.ToDistance(0.5f, Near, Far));
    }
}
