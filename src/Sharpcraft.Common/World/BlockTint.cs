// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Common.World;

/// <summary>
/// Averages a biome colour over the client's <c>biomeBlendRadius</c> square, matching the reference
/// <c>ClientLevel.calculateBlockTint</c> (docs/modules/weather-and-sky-color.md 6.1): every cell in the
/// <c>(2r+1)²</c> square contributes its resolver colour, summed per channel and divided by the count.
/// </summary>
public static class BlockTint
{
    /// <summary>
    /// The blended tint at (<paramref name="x"/>, <paramref name="z"/>). <paramref name="blendRadius"/> is the option value
    /// (0 = single cell; the reference default is 2, i.e. a 5×5 square). <paramref name="resolve"/> maps a world column to
    /// its per-biome colour (packed ARGB).
    /// </summary>
    public static int Calculate(int x, int z, int blendRadius, Func<int, int, int> resolve)
    {
        ArgumentNullException.ThrowIfNull(resolve);

        if (blendRadius <= 0)
        {
            return resolve(x, z);
        }

        var width = (blendRadius * 2) + 1;
        var count = width * width;
        var totalRed = 0;
        var totalGreen = 0;
        var totalBlue = 0;

        for (var dz = -blendRadius; dz <= blendRadius; dz++)
        {
            for (var dx = -blendRadius; dx <= blendRadius; dx++)
            {
                var color = resolve(x + dx, z + dz);
                totalRed += ColourBlend.Red(color);
                totalGreen += ColourBlend.Green(color);
                totalBlue += ColourBlend.Blue(color);
            }
        }

        return ColourBlend.Rgb(totalRed / count, totalGreen / count, totalBlue / count);
    }
}
