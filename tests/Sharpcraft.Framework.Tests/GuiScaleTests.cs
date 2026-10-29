// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Framework.Platform;
using Xunit;

namespace Sharpcraft.Framework.Tests;

/// <summary>
/// The interface scale (docs/modules/ui.md 9.1). Getting this wrong makes every number in the interface wrong at
/// once, because the layout is written in interface pixels and this decides how many device pixels one of those is.
/// </summary>
public sealed class GuiScaleTests
{
    [Theory]
    [InlineData(960, 540, 2)]
    [InlineData(1920, 1080, 4)]
    [InlineData(1280, 720, 3)]
    [InlineData(3840, 2160, 9)]
    [InlineData(320, 240, 1)]
    [InlineData(200, 150, 1)]
    [InlineData(854, 480, 2)]
    public void TheScaleIsTheLargestWholeNumberThatStillFitsTheMinimumCanvas(
        int width,
        int height,
        int expected) =>
        Assert.Equal(expected, GuiScale.Calculate(width, height));

    [Fact]
    public void TheCanvasIsTheFramebufferDividedByTheScaleAndRoundedUp()
    {
        var scale = GuiScale.Calculate(960, 540);
        var (width, height) = GuiScale.ScaledSize(960, 540, scale);

        Assert.Equal(480, width);
        Assert.Equal(270, height);

        // 1280 / 3 does not divide evenly, and the reference rounds up so no part of the canvas is lost.
        var odd = GuiScale.Calculate(1280, 720);
        var (oddWidth, oddHeight) = GuiScale.ScaledSize(1280, 720, odd);

        Assert.Equal(427, oddWidth);
        Assert.Equal(240, oddHeight);
    }

    [Fact]
    public void ARequestedMaximumCapsTheScale()
    {
        Assert.Equal(2, GuiScale.Calculate(1920, 1080, maxScale: 2));
        Assert.Equal(1, GuiScale.Calculate(1920, 1080, maxScale: 1));

        // A maximum larger than what fits is ignored: the canvas minimum still decides.
        Assert.Equal(4, GuiScale.Calculate(1920, 1080, maxScale: 100));
    }

    [Fact]
    public void AnEvenScaleIsForcedWhenTheFontRequiresIt()
    {
        // Three would be chosen normally; the unicode font needs even numbers.
        Assert.Equal(4, GuiScale.Calculate(1280, 720, enforceUnicode: true));

        var (width, height) = GuiScale.ScaledSize(1280, 720, 4);
        Assert.Equal(320, width);
        Assert.Equal(180, height);
    }

    [Fact]
    public void TheMinimumCanvasIsNeverViolated()
    {
        // Two hundred pixels across cannot be halved and still leave 320 interface pixels.
        Assert.Equal(1, GuiScale.Calculate(200, 150));

        // The smallest window that still allows a scale of two.
        Assert.Equal(2, GuiScale.Calculate(640, 480));
    }
}
