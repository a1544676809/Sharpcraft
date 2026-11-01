// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>Adaptive cloud range policy (docs/specs/render/optimization-plan.md stage 2).</summary>
public sealed class CloudRangePolicyTests
{
    [Fact]
    public void ReducesRangeAboveTheFrameBudget()
    {
        Assert.Equal(120, CloudRangePolicy.Next(128, 50.0));
        Assert.Equal(32, CloudRangePolicy.Next(36, 50.0));
    }

    [Fact]
    public void GrowsRangeWithHeadroom()
    {
        Assert.Equal(128, CloudRangePolicy.Next(124, 10.0));
        Assert.Equal(36, CloudRangePolicy.Next(32, 10.0));
    }

    [Fact]
    public void StaysStableInsideTheDeadBand()
    {
        Assert.Equal(96, CloudRangePolicy.Next(96, 25.0));
    }
}
