// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Framework.Tests.Rendering;

/// <summary>
/// Frame comparison (docs/analysis/render/offscreen-ui-pass.md): an opaque fill must match exactly, a blend may round by
/// one, and a mismatch has to be reported with a location rather than as a bare count.
/// </summary>
public sealed class PixelComparisonTests
{
    [Fact]
    public void IdenticalFramesHaveNoDifference()
    {
        var pixels = new byte[] { 1, 2, 3, 255, 4, 5, 6, 255 };

        var difference = PixelComparison.Compare(pixels, pixels, 2, 1);

        Assert.Equal(0, difference.Mismatches);
        Assert.Equal(-1, difference.FirstX);
    }

    [Fact]
    public void ABlendRoundingByOnePassesWithToleranceOne()
    {
        var expected = new byte[] { 10, 20, 30, 255, 40, 50, 60, 255 };
        var actual = new byte[] { 11, 20, 30, 255, 40, 50, 59, 255 };

        Assert.Equal(0, PixelComparison.Compare(expected, actual, 2, 1, tolerance: 1).Mismatches);
        Assert.Equal(2, PixelComparison.Compare(expected, actual, 2, 1).Mismatches);
    }

    [Fact]
    public void TheFirstMismatchLocationIsReported()
    {
        var expected = new byte[] { 0, 0, 0, 255, 0, 0, 0, 255, 9, 9, 9, 255, 9, 9, 9, 255 };
        var actual = new byte[] { 0, 0, 0, 255, 0, 0, 0, 255, 9, 9, 9, 255, 1, 9, 9, 255 };

        var difference = PixelComparison.Compare(expected, actual, 2, 2);

        Assert.Equal(1, difference.Mismatches);
        Assert.Equal(1, difference.FirstX);
        Assert.Equal(1, difference.FirstY);
        Assert.Equal(8, difference.MaxChannelDelta);
    }

    [Fact]
    public void ArgbPackingMatchesTheUiConvention()
    {
        var pixels = new byte[] { 0x33, 0x66, 0xCC, 0xFF };

        Assert.Equal(0xFF3366CCu, PixelComparison.ArgbAt(pixels, 1, 0, 0));
    }

    [Fact]
    public void MismatchedBufferSizesAreRejected()
    {
        Assert.Throws<ArgumentException>(() => PixelComparison.Compare(new byte[4], new byte[8], 2, 1));
    }
}
