// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.World;
using Xunit;

namespace Sharpcraft.Common.Tests.World;

/// <summary>
/// Biome block-tint averaging matches ClientLevel.calculateBlockTint (docs/modules/weather-and-sky-color.md 6.1/§9).
/// </summary>
public sealed class BlockTintTests
{
    [Fact]
    public void ZeroRadiusReturnsTheSingleCell()
    {
        Assert.Equal(ColourBlend.Rgb(1, 2, 3), BlockTint.Calculate(5, 7, 0, (x, z) => ColourBlend.Rgb(1, 2, 3)));
    }

    [Fact]
    public void AConstantColourIsUnchangedByAveraging()
    {
        var colour = ColourBlend.Rgb(16, 32, 48);
        Assert.Equal(colour, BlockTint.Calculate(0, 0, 2, (x, z) => colour));
    }

    [Fact]
    public void ThreeByThreeAverageIsHandComputed()
    {
        // r = (x+1)*10, g = (z+1)*10, b = 100. Over x,z in -1..1: r cells {0,10,20} each counted 3 times -> 90/9 = 10;
        // g likewise 10; b = 100.
        static int Resolve(int x, int z) => ColourBlend.Rgb((x + 1) * 10, (z + 1) * 10, 100);

        Assert.Equal(ColourBlend.Rgb(10, 10, 100), BlockTint.Calculate(0, 0, 1, Resolve));
    }
}
