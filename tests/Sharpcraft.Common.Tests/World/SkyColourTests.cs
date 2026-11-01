// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.World;
using Xunit;

namespace Sharpcraft.Common.Tests.World;

/// <summary>
/// Sky colour from biome temperature matches OverworldBiomes.calculateSkyColor (docs/modules/weather-and-sky-color.md
/// 6.2): t = clamp(temp/3,-1,1), HSV (0.62222224-0.05t, 0.5+0.1t, 1.0), opaque. Expected ARGB is hand-computed.
/// </summary>
public sealed class SkyColourTests
{
    [Fact]
    public void ZeroTemperatureIsTheHandComputedColour()
    {
        // h=0.62222224 -> h*6=3.7333 -> h=3, f=0.73333; sat=0.5; p=0.5, q=0.63333, t=0.86667.
        // case 3: red=p=0.5->127, green=q=0.63333->161, blue=value=1->255 => 0xFF7FA1FF.
        Assert.Equal(unchecked((int)0xFF7FA1FF), SkyColour.FromTemperature(0f));
    }

    [Fact]
    public void ResultIsOpaque()
    {
        Assert.Equal(0xFF, (SkyColour.FromTemperature(0.5f) >> 24) & 0xFF);
    }

    [Fact]
    public void TemperatureIsClampedToTheValidBand()
    {
        Assert.Equal(SkyColour.FromTemperature(3f), SkyColour.FromTemperature(100f));
        Assert.Equal(SkyColour.FromTemperature(-3f), SkyColour.FromTemperature(-100f));
    }
}
