// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Common.World;
using Xunit;

namespace Sharpcraft.Common.Tests.World;

/// <summary>
/// Colour blend + weather layering match the reference (docs/modules/weather-and-sky-color.md 6.3). Expected values are
/// hand-computed from the documented formulas.
/// </summary>
public sealed class WeatherBlendTests
{
    [Fact]
    public void GreyscaleUsesTheReferenceWeights()
    {
        Assert.Equal(0.3f, ColourBlend.Greyscale(new Vector3(1f, 0f, 0f)).X, 4);
        Assert.Equal(0.59f, ColourBlend.Greyscale(new Vector3(0f, 1f, 0f)).X, 4);
        Assert.Equal(0.11f, ColourBlend.Greyscale(new Vector3(0f, 0f, 1f)).X, 4);
    }

    [Fact]
    public void BlendToGrayOfWhiteMatchesHandComputation()
    {
        // greyscale(white)=1, scale by 0.75 -> 0.75, srgbLerp(0.6, 1, 0.75) = 1 + 0.6*(0.75-1) = 0.85.
        var result = ColourBlend.BlendToGray(Vector3.One, factor: 0.6f, brightness: 0.75f);
        Assert.Equal(0.85f, result.X, 4);
        Assert.Equal(0.85f, result.Y, 4);
        Assert.Equal(0.85f, result.Z, 4);
    }

    [Fact]
    public void RainBrightnessIsOneMinusRain()
    {
        Assert.Equal(1f, WeatherBlend.RainBrightness(0f), 4);
        Assert.Equal(0f, WeatherBlend.RainBrightness(1f), 4);
        Assert.Equal(0.7f, WeatherBlend.RainBrightness(0.3f), 4);
    }

    [Fact]
    public void SkyLightFactorBlendsRainAndThunderByWeight()
    {
        // No weather: unchanged.
        Assert.Equal(1f, WeatherBlend.SkyLightFactor(1f, 0f, 0f), 4);

        // Full rain: lerp(0.3125, 1, 0.24) = 0.7625.
        Assert.Equal(0.7625f, WeatherBlend.SkyLightFactor(1f, 1f, 0f), 4);

        // Full thunder (rain 0): lerp(0.52734375, 1, 0.24) = 0.59921875.
        Assert.Equal(0.5992f, WeatherBlend.SkyLightFactor(1f, 0f, 1f), 4);
    }

    [Fact]
    public void SkyLightFactorAppliesRainThenThunderSequentially()
    {
        // rain weight = 0.8-0.3 = 0.5 then thunder weight 0.3, each applied to the running result.
        // rain: 1 + 0.5*(0.7625-1) = 0.88125; thunder: lerp(0.52734375,0.88125,0.24)=0.543128 then
        // result = 0.88125 + 0.3*(0.543128-0.88125) = 0.779813.
        Assert.Equal(0.7798f, WeatherBlend.SkyLightFactor(1f, 0.8f, 0.3f), 3);
    }

    [Fact]
    public void RainDarkensDayButLeavesNightAlone()
    {
        // Day base factor 1 blended toward 0.24 by alpha 0.3125 -> 0.7625; night base already 0.24 -> unchanged.
        Assert.Equal(0.7625f, WeatherBlend.SkyLightFactor(1f, 1f, 0f), 4);
        Assert.Equal(0.24f, WeatherBlend.SkyLightFactor(0.24f, 1f, 0f), 4);
    }

    [Fact]
    public void ApplySkyColourGreyscalesUnderRain()
    {
        Assert.Equal(Vector3.One, WeatherBlend.ApplySkyColour(Vector3.One, 0f, 0f));
        var rainy = WeatherBlend.ApplySkyColour(Vector3.One, 1f, 0f);
        Assert.Equal(0.85f, rainy.X, 4);
        Assert.Equal(0.85f, rainy.Z, 4);
    }
}
