// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Translucent draw order (docs/specs/render/pipeline.md 15, slice S3): farthest first, and stable so the same scene always
/// draws in the same order and pixel comparisons stay meaningful.
/// </summary>
public sealed class TranslucentOrderTests
{
    [Fact]
    public void TheFarthestItemComesFirst()
    {
        var order = TranslucentOrder.ByDistanceDescending([(1L, 4f), (2L, 100f), (3L, 25f)]);

        Assert.Equal([2L, 3L, 1L], order);
    }

    [Fact]
    public void EqualDistancesKeepTheirIncomingOrder()
    {
        var order = TranslucentOrder.ByDistanceDescending([(10L, 9f), (20L, 9f), (30L, 9f)]);

        Assert.Equal([10L, 20L, 30L], order);
    }

    [Fact]
    public void ASingleItemIsReturnedUnchanged()
    {
        Assert.Equal([7L], TranslucentOrder.ByDistanceDescending([(7L, 0f)]));
    }
}
