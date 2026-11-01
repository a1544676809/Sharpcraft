// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;

namespace Sharpcraft.Common.World;

/// <summary>
/// The overworld sky colour at a position: each nearby biome's <c>calculateSkyColor(temperature)</c> blended with the
/// reference spatial gaussian (docs/analysis/common/biome-data.md 4, docs/specs/render/sky-celestial.md 6.2).
/// </summary>
/// <remarks>
/// Mirrors EnvironmentAttributeProbe: samples biome attributes at quart coordinates <c>pos*0.25</c> with the 7-tap kernel
/// <c>{0,1,4,6,4,1,0}</c> (product of the three axes) and normalises by the accumulated weight. Returns linear 0..1 RGB.
/// </remarks>
public static class SkyColourSource
{
    /// <summary>
    /// Sky colour at a world block position. <paramref name="biomeIdAt"/> maps a world block position to a biome id (-1 when
    /// unknown); unknown biomes contribute no weight so a partially loaded area still averages what it has.
    /// </summary>
    public static Vector3 ColorAt(Func<int, int, int, int> biomeIdAt, BiomeRegistry registry, int x, int y, int z)
    {
        ArgumentNullException.ThrowIfNull(biomeIdAt);
        ArgumentNullException.ThrowIfNull(registry);

        var quartX = FloorDiv(x, 4);
        var quartY = FloorDiv(y, 4);
        var quartZ = FloorDiv(z, 4);

        double totalRed = 0;
        double totalGreen = 0;
        double totalBlue = 0;
        long totalWeight = 0;

        for (var dz = -GaussianWeights.Radius; dz <= GaussianWeights.Radius; dz++)
        {
            for (var dy = -GaussianWeights.Radius; dy <= GaussianWeights.Radius; dy++)
            {
                for (var dx = -GaussianWeights.Radius; dx <= GaussianWeights.Radius; dx++)
                {
                    var weight = GaussianWeights.Weight(dx, dy, dz);
                    if (weight == 0)
                    {
                        continue;
                    }

                    var biomeId = biomeIdAt((quartX + dx) * 4, (quartY + dy) * 4, (quartZ + dz) * 4);
                    var entry = registry.ById(biomeId);
                    if (entry is null)
                    {
                        continue;
                    }

                    var packed = SkyColour.FromTemperature(entry.Climate.Temperature);
                    totalRed += weight * ((packed >> 16) & 0xFF);
                    totalGreen += weight * ((packed >> 8) & 0xFF);
                    totalBlue += weight * (packed & 0xFF);
                    totalWeight += weight;
                }
            }
        }

        if (totalWeight == 0)
        {
            return Vector3.Zero;
        }

        return new Vector3(
            (float)(totalRed / totalWeight) / 255f,
            (float)(totalGreen / totalWeight) / 255f,
            (float)(totalBlue / totalWeight) / 255f);
    }

    private static int FloorDiv(int value, int divisor) => (int)Math.Floor((double)value / divisor);
}
