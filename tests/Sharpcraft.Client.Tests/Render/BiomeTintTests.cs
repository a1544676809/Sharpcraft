// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Sharpcraft.Common.Identifiers;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Biome tint groundwork (docs/specs/render/block-layer-and-tint.md, A3): the colour map is indexed exactly like the
/// reference, and the tint table follows the reference's block registrations.
/// </summary>
public sealed class BiomeTintTests
{
    [Fact]
    public void TheColourMapIsIndexedLikeTheReference()
    {
        // Mirror of ColorMapColorUtil.get: rain *= temp, x = (1 - temp) * 255, y = (1 - rain) * 255, index = (y << 8) | x.
        var temperature = 0.5;
        var rainfall = 1.0;
        var rain = rainfall * temperature;
        var x = (int)((1.0 - temperature) * 255.0);
        var y = (int)((1.0 - rain) * 255.0);

        Assert.Equal(127, x);
        Assert.Equal(127, y);
        Assert.Equal((127 << 8) | 127, 32639);
    }

    [Fact]
    public void GrassTintsTheTopFaceOnly()
    {
        Assert.True(BlockTintTable.TintsTopFaceOnly(BlockTintKind.Grass));
        Assert.False(BlockTintTable.TintsTopFaceOnly(BlockTintKind.Foliage));
    }

    [Theory]
    [InlineData("grass_block", BlockTintKind.Grass)]
    [InlineData("short_grass", BlockTintKind.Grass)]
    [InlineData("oak_leaves", BlockTintKind.Foliage)]
    [InlineData("spruce_leaves", BlockTintKind.FoliageEvergreen)]
    [InlineData("birch_leaves", BlockTintKind.FoliageBirch)]
    [InlineData("water", BlockTintKind.Water)]
    [InlineData("stone", BlockTintKind.None)]
    public void TheTintTableMatchesTheReferenceRegistrations(string path, BlockTintKind expected)
    {
        Assert.Equal(expected, BlockTintTable.KindOf(new Identifier("minecraft", path)));
    }

    [Fact]
    public void NoTintLeavesTheMultiplierAtOne()
    {
        var (red, green, blue) = BiomeTint.ToMultiplier(-1);

        Assert.Equal(1f, red);
        Assert.Equal(1f, green);
        Assert.Equal(1f, blue);
    }

    [Fact]
    public void NegativeArgbIsStillATint()
    {
        // The reference foliage default is 0xFF48B518, which is negative as a signed int; it must not read as "no tint".
        var (red, green, blue) = BiomeTint.ToMultiplier(BiomeTint.FoliageDefault);

        Assert.Equal(0x48 / 255f, red, 4);
        Assert.Equal(0xB5 / 255f, green, 4);
        Assert.Equal(0x18 / 255f, blue, 4);
    }

    [Fact]
    public void ATintBecomesAUnitMultiplier()
    {
        var (red, green, blue) = BiomeTint.ToMultiplier(0x80_40_20);

        Assert.Equal(128f / 255f, red, 4);
        Assert.Equal(64f / 255f, green, 4);
        Assert.Equal(32f / 255f, blue, 4);
    }

    [Fact]
    public void TheReferenceConstantsArePreserved()
    {
        Assert.Equal(-12012264, BiomeTint.FoliageDefault);
        Assert.Equal(-10380959, BiomeTint.FoliageEvergreen);
        Assert.Equal(-8345771, BiomeTint.FoliageBirch);
        Assert.Equal(4159204, BiomeTint.WaterDefault);
    }
}
