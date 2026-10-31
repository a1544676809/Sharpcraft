// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Assets;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Biome tint sampling against the reference rule
/// (docs/specs/quality/vanilla-equivalence-matrix.md row 9).
/// </summary>
/// <remarks>
/// The rule is taken from the reference colour map helper: multiply rainfall by temperature, then
/// <c>x = (1 - temperature) * 255</c>, <c>y = (1 - rain) * 255</c>, <c>index = (y &lt;&lt; 8) | x</c>, and fall back to the
/// supplied colour when the index is outside the map. The test compares the client's sampler with an independent copy of
/// that rule over a grid, so a drift in either direction is caught.
/// </remarks>
public sealed class BiomeTintSamplingTests
{
    [Fact]
    public void TheSamplerMatchesTheReferenceAcrossAGrid()
    {
        // A map whose pixel value equals its index, so the selected cell is visible in the result.
        var pixels = new int[Colormap.PixelCount];

        for (var index = 0; index < pixels.Length; index++)
        {
            pixels[index] = index;
        }

        var map = Colormap.FromPixels(pixels);

        for (var temperatureSteps = 0; temperatureSteps <= 10; temperatureSteps++)
        {
            for (var rainfallSteps = 0; rainfallSteps <= 10; rainfallSteps++)
            {
                var temperature = temperatureSteps / 10.0;
                var rainfall = rainfallSteps / 10.0;

                var expected = Reference(temperature, rainfall, pixels, fallback: -65281);
                var actual = map.Sample(temperature, rainfall, fallback: -65281);

                Assert.Equal(expected, actual);
            }
        }
    }

    [Fact]
    public void AnIndexOutsideTheMapFallsBack()
    {
        var map = Colormap.FromPixels(new int[Colormap.PixelCount]);

        // A temperature above one makes the x term negative, so the index is negative and the fallback is used.
        Assert.Equal(-12012264, map.Sample(1.5, 1.0, fallback: -12012264));

        // The corner case every biome hits: hot and wet selects the very last cell of a real map.
        var indexed = Colormap.FromPixels(Enumerable.Range(0, Colormap.PixelCount).ToArray());
        Assert.Equal((255 << 8) | 255, indexed.Sample(0.0, 0.0, fallback: -1));

        // With no pixel at that index the fallback is returned instead.
        Assert.Equal(-1, Reference(0.0, 0.0, null, fallback: -1));
    }

    private static int Reference(double temperature, double rainfall, int[]? pixels, int fallback)
    {
        var rain = rainfall * temperature;
        var x = (int)((1.0 - temperature) * 255.0);
        var y = (int)((1.0 - rain) * 255.0);
        var index = (y << 8) | x;

        if (index < 0)
        {
            return fallback;
        }

        return pixels is not null && index < pixels.Length ? pixels[index] : fallback;
    }
}
