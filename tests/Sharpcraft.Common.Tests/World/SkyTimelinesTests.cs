// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.World;
using Xunit;

namespace Sharpcraft.Common.Tests.World;

/// <summary>Cubic-bezier easing and the overworld sun/moon/star angle tracks (docs/modules/time.md 5).</summary>
public sealed class SkyTimelinesTests
{
    [Fact]
    public void CubicBezierHitsItsEndpointsAndMidpoint()
    {
        var ease = CubicBezierEasing.Symmetric(0.362f, 0.241f);
        Assert.Equal(0f, ease.Apply(0f), 4);
        Assert.Equal(1f, ease.Apply(1f), 4);
        // Symmetric bezier is odd around the midpoint: apply(0.5) = 0.5.
        Assert.Equal(0.5f, ease.Apply(0.5f), 4);
    }

    [Fact]
    public void CubicBezierIsMonotonic()
    {
        var ease = CubicBezierEasing.Symmetric(0.362f, 0.241f);
        var previous = ease.Apply(0f);
        for (var i = 1; i <= 20; i++)
        {
            var value = ease.Apply(i / 20f);
            Assert.True(value >= previous - 1e-4f, $"ease not monotonic at {i / 20f}");
            previous = value;
        }
    }

    [Fact]
    public void SunAngleAnchorsMatchTheKeyframes()
    {
        // noon tick (6000) is the keyframe crossing to 0; midnight (18000) is halfway through the 0..360 sweep.
        Assert.Equal(0f, SkyTimelines.SunAngleDegrees(6000), 3);
        Assert.Equal(180f, SkyTimelines.SunAngleDegrees(18000), 3);
        Assert.Equal(0f, SkyTimelines.SunAngleDegrees(30000), 3); // wraps to 6000
    }

    [Fact]
    public void MoonAngleAnchorsMatchTheKeyframes()
    {
        // MOON_ANGLE keyframes are 540 @6000 -> 180 @6000; at midnight (halfway) it is 360.
        Assert.Equal(360f, SkyTimelines.MoonAngleDegrees(18000), 3);
        Assert.Equal(180f, SkyTimelines.MoonAngleDegrees(6000), 3);
    }

    [Fact]
    public void StarBrightnessFollowsItsTrack()
    {
        Assert.Equal(0f, SkyAttributeTracks.StarBrightnessAt(6000), 3);   // day: 0
        Assert.Equal(0.5f, SkyAttributeTracks.StarBrightnessAt(18000), 3); // midnight plateau: 0.5
    }

    [Fact]
    public void SkyLightFactorIsDayOneAndNightPointTwoFour()
    {
        Assert.Equal(1f, SkyAttributeTracks.SkyLightFactorAt(6000), 3);
        Assert.Equal(0.24f, SkyAttributeTracks.SkyLightFactorAt(18000), 3);
    }

    [Fact]
    public void MoonPhaseIndexAdvancesEveryFullDay()
    {
        Assert.Equal(0, SkyAttributeTracks.MoonPhaseIndex(0));
        Assert.Equal(3, SkyAttributeTracks.MoonPhaseIndex((24000L * 3) + 5));
        Assert.Equal(0, SkyAttributeTracks.MoonPhaseIndex(24000L * 8)); // wraps
    }

    [Fact]
    public void SunriseColourIsTransparentInTheDay()
    {
        // The 730/11270 keyframe (16770355) has alpha byte 0, so the sunrise band is invisible at noon.
        Assert.Equal(0f, SkyAttributeTracks.SunriseSunsetColorAt(6000).W, 3);
    }

    [Fact]
    public void SkyLightColourIsWhiteByDayAndBlueAtNight()
    {
        var day = SkyAttributeTracks.SkyLightColorAt(6000);
        Assert.Equal(1f, day.X, 3);
        Assert.Equal(1f, day.Y, 3);
        Assert.Equal(1f, day.Z, 3);

        var night = SkyAttributeTracks.SkyLightColorAt(18000);
        // NIGHT_SKY_LIGHT_COLOR = (0.48,0.48,1.0) sRGB -> linear, blue stays 1.
        static float Srgb(float x) => x >= 0.04045f ? MathF.Pow((x + 0.055f) / 1.055f, 2.4f) : x / 12.92f;
        var expectedRed = Srgb(122f / 255f);
        Assert.Equal(expectedRed, night.X, 3);
        Assert.Equal(expectedRed, night.Y, 3);
        Assert.Equal(1f, night.Z, 3);
    }
}
