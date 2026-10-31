// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Assets;

/// <summary>
/// Builds the block atlas mip chain.
/// </summary>
/// <remarks>
/// The atlas already carries one texel of edge padding per sprite, so a plain box filter over the whole image cannot pull a
/// neighbouring sprite into a sample. Colour is averaged weighted by alpha: a straight average mixes the black of a
/// transparent texel into its neighbours, which produced thin dark lines along block edges that grew thicker with distance.
/// Level zero of the visible image is untouched.
/// </remarks>
public static class AtlasMipChain
{
    /// <summary>Levels generated below level zero.</summary>
    public const int Levels = 4;

    /// <summary>
    /// Builds the levels below level zero.
    /// </summary>
    /// <param name="rgba">Level zero pixels, four bytes per texel.</param>
    /// <param name="width">Atlas width.</param>
    /// <param name="height">Atlas height.</param>
    /// <param name="sprites">Sprite rectangles; kept for the caller's convenience, the filter covers the whole atlas.</param>
    public static byte[][] Build(byte[] rgba, int width, int height, IReadOnlyList<SpriteRect> sprites)
    {
        ArgumentNullException.ThrowIfNull(rgba);
        ArgumentNullException.ThrowIfNull(sprites);

        var levels = new List<byte[]>(Levels);
        var previous = rgba;
        var previousWidth = width;
        var previousHeight = height;

        for (var level = 0; level < Levels; level++)
        {
            var nextWidth = Math.Max(1, previousWidth / 2);
            var nextHeight = Math.Max(1, previousHeight / 2);

            if (previousWidth == 1 && previousHeight == 1)
            {
                break;
            }

            var next = new byte[nextWidth * nextHeight * 4];

            for (var y = 0; y < nextHeight; y++)
            {
                for (var x = 0; x < nextWidth; x++)
                {
                    var x0 = Math.Min(x * 2, previousWidth - 1);
                    var x1 = Math.Min((x * 2) + 1, previousWidth - 1);
                    var y0 = Math.Min(y * 2, previousHeight - 1);
                    var y1 = Math.Min((y * 2) + 1, previousHeight - 1);

                    var offsets = new[]
                    {
                        (((y0 * previousWidth) + x0) * 4),
                        (((y0 * previousWidth) + x1) * 4),
                        (((y1 * previousWidth) + x0) * 4),
                        (((y1 * previousWidth) + x1) * 4)
                    };

                    var weight = 0;
                    var red = 0;
                    var green = 0;
                    var blue = 0;
                    var alpha = 0;

                    foreach (var offset in offsets)
                    {
                        var texelAlpha = previous[offset + 3];
                        weight += texelAlpha;
                        red += previous[offset] * texelAlpha;
                        green += previous[offset + 1] * texelAlpha;
                        blue += previous[offset + 2] * texelAlpha;
                        alpha += texelAlpha;
                    }

                    var target = (((y * nextWidth) + x) * 4);

                    if (weight == 0)
                    {
                        // Every texel transparent: keep it transparent and black, there is no colour to average.
                        next[target] = 0;
                        next[target + 1] = 0;
                        next[target + 2] = 0;
                        next[target + 3] = 0;
                        continue;
                    }

                    next[target] = (byte)(red / weight);
                    next[target + 1] = (byte)(green / weight);
                    next[target + 2] = (byte)(blue / weight);
                    next[target + 3] = (byte)(alpha / 4);
                }
            }

            levels.Add(next);
            previous = next;
            previousWidth = nextWidth;
            previousHeight = nextHeight;
        }

        return [.. levels];
    }
}
