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
        // At tick 18000 SKY_LIGHT_FACTOR is 0.24 and SKY_LIGHT_COLOR is the raw night colour #7a7aff = (122,122,255)/255.
        var night = LightmapParameters.OverworldAt(18000);
        Assert.Equal(0.24f, night.SkyFactor, 2);

        var dark = VanillaLightmap.Colour(15, 0, night);
        var day = VanillaLightmap.Colour(15, 0);

        Assert.True(dark.X < day.X, "night must be darker than day");
        // Hand-computed: sky 0.478*0.24 = 0.115; notGamma 0.3188/0.6664; brightness mix gives (0.2168,0.2168,0.4532).
        Assert.Equal(0.2168f, dark.X, 3);
        Assert.True(dark.Z > dark.X, "night sky should be blue tinted");
        // The reference night sky light color has R and G on the same track, so those two channels agree exactly.
        Assert.Equal(dark.X, dark.Y, 5);
        Assert.Equal(0.4532f, dark.Z, 3);
    }

    [Fact]
    public void OverworldAtInterpolatesSkyFactorBetweenTicks()
    {
        // 13140 is the tick where SKY_LIGHT_FACTOR drops from 1.0 to 0.24; halfway is the mean of the two ticks.
        var previous = Sharpcraft.Common.World.SkyAttributeTracks.SkyLightFactorAt(13139);
        var current = Sharpcraft.Common.World.SkyAttributeTracks.SkyLightFactorAt(13140);
        var mid = LightmapParameters.OverworldAt(13140, 0.5f);

        Assert.Equal((previous + current) * 0.5f, mid.SkyFactor, 4);
        Assert.True(mid.SkyFactor < previous && mid.SkyFactor > current);
    }

    [Fact]
    public void PerFragmentLightmapDiffersFromBakedInterpolation()
    {
        // A face lit sky=15 at one corner and sky=0 at the other. The old baked approach wrote f(endpoint) per vertex and the
        // GPU linearly interpolated those results; the shader-side approach interpolates the level and evaluates f per pixel.
        var litCorner = VanillaLightmap.Colour(15, 0).X;   // 1
        var darkCorner = VanillaLightmap.Colour(0, 0).X;   // 0
        var bakedMid = (litCorner + darkCorner) * 0.5f;    // 0.5

        // f at level 0.5: brightness(0.5) = 0.5/(4-1.5) = 0.2; notGamma(0.2) = 0.5904; mix by brightnessFactor 0.5 -> 0.3952.
        var level = 0.5f;
        var raw = VanillaLightmap.BrightnessCurve(level);
        var boosted = VanillaLightmap.NotGamma(new Vector3(raw)).X;
        var perFragmentMid = raw + ((boosted - raw) * 0.5f);

        Assert.Equal(0.5f, bakedMid, 4);
        Assert.Equal(0.3952f, perFragmentMid, 3);
        Assert.True(perFragmentMid < bakedMid, "per-fragment mid-gradient value is darker than the baked interpolation");
    }

    [Fact]
    public void BlockLightTintIsRawSrgbChannels()
    {
        // Reference BLOCK_LIGHT_TINT default = ARGB.vector3fFromRGB24(-10100) = raw (0xFF,0xD8,0x8C)/255, no gamma.
        var tint = LightmapParameters.DayOverworld.BlockLightTint;

        Assert.Equal(1.0f, tint.X, 3);
        Assert.Equal(0xD8 / 255f, tint.Y, 3);
        Assert.Equal(0x8C / 255f, tint.Z, 3);
    }
}
