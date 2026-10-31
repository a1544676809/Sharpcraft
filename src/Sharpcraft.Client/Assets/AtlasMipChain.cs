// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Assets;

/// <summary>
/// Builds the block atlas mip chain one sprite at a time.
/// </summary>
/// <remarks>
/// The filter never leaves the sprite it started in. A plain box filter over the whole atlas pulls neighbouring sprites in
/// as soon as the two texel padding is halved away, and on a large flat surface such as an ice field that showed up as
/// bands and per block colour patches. Each destination texel therefore asks which sprite it belongs to and clamps its
/// source block to that sprite's rectangle, which is the same idea the optimisation mods use. Colour is averaged weighted
/// by alpha, so the black of a transparent texel cannot darken its neighbours.
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
    /// <param name="sprites">Sprite rectangles, one per atlas sprite.</param>
    public static byte[][] Build(byte[] rgba, int width, int height, IReadOnlyList<SpriteRect> sprites)
    {
        ArgumentNullException.ThrowIfNull(rgba);
        ArgumentNullException.ThrowIfNull(sprites);

        var owner = BuildOwnerMap(width, height, sprites);
        var levels = new List<byte[]>(Levels);
        var previous = rgba;
        var previousWidth = width;
        var previousHeight = height;

        for (var level = 0; level < Levels; level++)
        {
            if (previousWidth == 1 && previousHeight == 1)
            {
                break;
            }

            var nextWidth = Math.Max(1, previousWidth / 2);
            var nextHeight = Math.Max(1, previousHeight / 2);
            var next = new byte[nextWidth * nextHeight * 4];
            var shift = level + 1;

            for (var y = 0; y < nextHeight; y++)
            {
                for (var x = 0; x < nextWidth; x++)
                {
                    var sprite = OwnerAt(owner, width, height, x << shift, y << shift);

                    var x0 = Math.Min(x * 2, previousWidth - 1);
                    var x1 = Math.Min((x * 2) + 1, previousWidth - 1);
                    var y0 = Math.Min(y * 2, previousHeight - 1);
                    var y1 = Math.Min((y * 2) + 1, previousHeight - 1);

                    if (sprite >= 0)
                    {
                        var rect = sprites[sprite];
                        var left = rect.X >> level;
                        var top = rect.Y >> level;
                        var right = Math.Max(left, (rect.X + rect.Width - 1) >> level);
                        var bottom = Math.Max(top, (rect.Y + rect.Height - 1) >> level);

                        x0 = Math.Clamp(x0, left, right);
                        x1 = Math.Clamp(x1, left, right);
                        y0 = Math.Clamp(y0, top, bottom);
                        y1 = Math.Clamp(y1, top, bottom);
                    }

                    var weight = 0;
                    var red = 0;
                    var green = 0;
                    var blue = 0;
                    var alpha = 0;

                    for (var oy = y0; oy <= y1; oy++)
                    {
                        for (var ox = x0; ox <= x1; ox++)
                        {
                            var offset = (((oy * previousWidth) + ox) * 4);
                            var texelAlpha = previous[offset + 3];

                            weight += texelAlpha;
                            red += previous[offset] * texelAlpha;
                            green += previous[offset + 1] * texelAlpha;
                            blue += previous[offset + 2] * texelAlpha;
                            alpha += texelAlpha;
                        }
                    }

                    var target = ((y * nextWidth) + x) * 4;

                    if (weight > 0)
                    {
                        next[target] = (byte)(red / weight);
                        next[target + 1] = (byte)(green / weight);
                        next[target + 2] = (byte)(blue / weight);
                        next[target + 3] = (byte)(alpha / Math.Max(1, (x1 - x0 + 1) * (y1 - y0 + 1)));
                    }
                }
            }

            levels.Add(next);
            previous = next;
            previousWidth = nextWidth;
            previousHeight = nextHeight;
        }

        return [.. levels];
    }

    private static int[] BuildOwnerMap(int width, int height, IReadOnlyList<SpriteRect> sprites)
    {
        var owner = new int[width * height];
        Array.Fill(owner, -1);

        for (var index = 0; index < sprites.Count; index++)
        {
            var rect = sprites[index];

            for (var y = Math.Max(0, rect.Y); y < Math.Min(height, rect.Y + rect.Height); y++)
            {
                var row = y * width;

                for (var x = Math.Max(0, rect.X); x < Math.Min(width, rect.X + rect.Width); x++)
                {
                    owner[row + x] = index;
                }
            }
        }

        return owner;
    }

    private static int OwnerAt(int[] owner, int width, int height, int x, int y)
    {
        // Padding texels belong to no sprite; a short ring search finds the sprite whose edge they extend.
        for (var radius = 0; radius <= 8; radius++)
        {
            for (var dy = -radius; dy <= radius; dy++)
            {
                for (var dx = -radius; dx <= radius; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != radius)
                    {
                        continue;
                    }

                    var sx = x + dx;
                    var sy = y + dy;

                    if (sx < 0 || sy < 0 || sx >= width || sy >= height)
                    {
                        continue;
                    }

                    var found = owner[(sy * width) + sx];

                    if (found >= 0)
                    {
                        return found;
                    }
                }
            }
        }

        return -1;
    }
}
