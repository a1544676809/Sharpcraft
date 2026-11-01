// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Render;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Equivalence of the lightmap multiplier with the vanilla formula (docs/specs/render/lighting.md 3 and 8). Expected
/// values are worked out by hand from the documented formula, not read back from the implementation.
/// </summary>
public sealed class VanillaLightmapTests
{
    [Fact]
    public void FullSkyIsWhite()
    {
        var colour = VanillaLightmap.Colour(15, 0);

        Assert.Equal(1f, colour.X, 5);
        Assert.Equal(1f, colour.Y, 5);
        Assert.Equal(1f, colour.Z, 5);
    }

    [Fact]
    public void NoLightIsBlack()
    {
        var colour = VanillaLightmap.Colour(0, 0);

        Assert.Equal(0f, colour.X, 5);
        Assert.Equal(0f, colour.Y, 5);
        Assert.Equal(0f, colour.Z, 5);
    }

    [Fact]
    public void SkyTwelveIsTheHandComputedValue()
    {
        // sky 12: level 0.8, brightness(0.8) = 0.8 / (4 - 2.4) = 0.5; white * 0.5 = 0.5;
        // notGamma(0.5) = 0.5 * (1 - 0.5^4)/0.5 = 0.9375; mix(0.5, 0.9375, 0.5) = 0.71875.
        var colour = VanillaLightmap.Colour(12, 0);

        Assert.Equal(0.71875f, colour.X, 4);
        Assert.Equal(0.71875f, colour.Y, 4);
        Assert.Equal(0.71875f, colour.Z, 4);
    }

    [Fact]
    public void BlockLightIsWarm()
    {
        // Torch-style block light uses the warm tint; at mid block light (full block light clamps to white) red must lead
        // green must lead blue.
        var colour = VanillaLightmap.Colour(0, 8);

        Assert.True(colour.X > colour.Y, $"red {colour.X} should exceed green {colour.Y}");
        Assert.True(colour.Y > colour.Z, $"green {colour.Y} should exceed blue {colour.Z}");
        Assert.True(colour.X > 0f);
    }

    [Fact]
    public void BrighterSkyIsBrighter()
    {
        var dim = VanillaLightmap.Colour(8, 0).X;
        var bright = VanillaLightmap.Colour(15, 0).X;

        Assert.True(bright > dim);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(15)]
    public void EveryColourStaysInRange(int level)
    {
        for (var other = 0; other <= 15; other += 5)
        {
            var fromSky = VanillaLightmap.Colour(level, other);
            var fromBlock = VanillaLightmap.Colour(other, level);

            Assert.InRange(fromSky.X, 0f, 1f);
            Assert.InRange(fromSky.Y, 0f, 1f);
            Assert.InRange(fromSky.Z, 0f, 1f);
            Assert.InRange(fromBlock.X, 0f, 1f);
        }
    }

    [Fact]
    public void BrightnessCurveMatchesTheVanillaRamp()
    {
        Assert.Equal(0f, VanillaLightmap.BrightnessCurve(0f), 5);
        Assert.Equal(1f, VanillaLightmap.BrightnessCurve(1f), 5);
        Assert.Equal(0.5f, VanillaLightmap.BrightnessCurve(0.8f), 5);
        Assert.True(VanillaLightmap.BrightnessCurve(0.9f) > VanillaLightmap.BrightnessCurve(0.8f));
    }

    [Fact]
    public void NotGammaLeavesEndpointsAlone()
    {
        Assert.Equal(Vector3.Zero, VanillaLightmap.NotGamma(Vector3.Zero));
        Assert.Equal(Vector3.One.X, VanillaLightmap.NotGamma(Vector3.One).X, 5);
    }

    [Fact]
    public void OverworldAtNoonMatchesTheDayDefaults()
    {
        var noon = LightmapParameters.OverworldAt(6000);

        Assert.Equal(1f, noon.SkyFactor, 3);
        Assert.Equal(1f, noon.SkyLightColor.X, 3);

        var colour = VanillaLightmap.Colour(15, 0, noon);
        Assert.Equal(1f, colour.X, 4);
        Assert.Equal(1f, colour.Z, 4);
    }

    [Fact]
    public void OverworldAtNightDarkensAndTintsTheSkyBlue()
    {
        // At tick 18000 SKY_LIGHT_FACTOR is 0.24 and SKY_LIGHT_COLOR is the night (0.48,0.48,1) linear tint.
        var night = LightmapParameters.OverworldAt(18000);
        Assert.Equal(0.24f, night.SkyFactor, 2);

        var dark = VanillaLightmap.Colour(15, 0, night);
        var day = VanillaLightmap.Colour(15, 0);

        Assert.True(dark.X < day.X, "night must be darker than day");
        Assert.True(dark.X < 0.15f, $"night red {dark.X} should be dark");
        Assert.True(dark.Z > dark.X, "night sky should be blue tinted");
        // The reference night sky light color has R and G on the same track, so those two channels agree exactly.
        Assert.Equal(dark.X, dark.Y, 5);
        Assert.InRange(dark.Z, 0.4f, 0.5f);
    }
}
