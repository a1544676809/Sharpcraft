// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.World;
using Xunit;

namespace Sharpcraft.Common.Tests.World;

/// <summary>
/// Colormap indexing matches the reference ColorMapColorUtil.get (docs/modules/weather-and-sky-color.md 6.1): the
/// index is (y&lt;&lt;8)|x with x=(1-temp)*255, y=(1-temp*downfall)*255. Expected indices are hand-computed.
/// </summary>
public sealed class ColorMapColorUtilTests
{
    private static int[] IdentityMap()
    {
        var pixels = new int[65536];
        for (var i = 0; i < pixels.Length; i++)
        {
            pixels[i] = i;
        }

        return pixels;
    }

    [Fact]
    public void IndexesCornersAndMiddle()
    {
        var pixels = IdentityMap();

        // temp=1 -> x=0, rain=1 -> y=0, index 0.
        Assert.Equal(0, ColorMapColorUtil.Get(1.0, 1.0, pixels, -1));
        // temp=0 -> x=255, rain=0 -> y=255, index 65535.
        Assert.Equal(65535, ColorMapColorUtil.Get(0.0, 0.5, pixels, -1));
        // temp=0.5 -> x=127, rain=0.25 -> y=191, index = 191<<8 | 127 = 49023.
        Assert.Equal(49023, ColorMapColorUtil.Get(0.5, 0.5, pixels, -1));
    }

    [Fact]
    public void DownfallIsScaledByTemperature()
    {
        var pixels = IdentityMap();

        // temp 0 makes rain 0 regardless of downfall, so both index the same cell.
        Assert.Equal(
            ColorMapColorUtil.Get(0.0, 0.0, pixels, -1),
            ColorMapColorUtil.Get(0.0, 0.9, pixels, -1));
    }

    [Fact]
    public void OutOfRangeIndexFallsBackToDefault()
    {
        Assert.Equal(-7, ColorMapColorUtil.Get(0.5, 0.5, new int[10], -7));
    }
}
