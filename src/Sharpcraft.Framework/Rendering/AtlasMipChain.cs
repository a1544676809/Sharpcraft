// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Framework.Rendering;

/// <summary>
/// Generates the mip chain for the block atlas.
/// </summary>
/// <remarks>
/// Vanilla stitches its block atlas with several mip levels and samples it with linear filtering
/// (ref://mc-26.3-analysis/src/net/minecraft/client/renderer/RenderPipelines.java uses the same terrain snippet with an
/// alpha cutout define, and the sprite loader stitches with maxMipmapLevels). Without mips a sixteen pixel texture sampled
/// nearest turns into speckle as soon as a block covers fewer pixels than its texture has texels, which is exactly what the
/// distant terrain showed.
/// </remarks>
public static class AtlasMipChain
{
    /// <summary>Levels generated below level zero. Four matches what vanilla stitches for blocks.</summary>
    public const int Levels = 4;

    /// <summary>
    /// Builds the levels below level zero by averaging each two by two block, alpha included.
    /// </summary>
    /// <param name="rgba">Level zero pixels, four bytes per texel.</param>
    /// <param name="width">Level zero width.</param>
    /// <param name="height">Level zero height.</param>
    /// <returns>One array per level, from level one upwards.</returns>
    public static byte[][] Build(byte[] rgba, int width, int height)
    {
        var levels = new List<byte[]>(Levels);
        var source = rgba;
        var sourceWidth = width;
        var sourceHeight = height;

        for (var level = 0; level < Levels; level++)
        {
            var nextWidth = Math.Max(1, sourceWidth / 2);
            var nextHeight = Math.Max(1, sourceHeight / 2);

            if (sourceWidth == 1 && sourceHeight == 1)
            {
                break;
            }

            var next = new byte[nextWidth * nextHeight * 4];

            for (var y = 0; y < nextHeight; y++)
            {
                for (var x = 0; x < nextWidth; x++)
                {
                    for (var channel = 0; channel < 4; channel++)
                    {
                        var total = 0;
                        var count = 0;

                        for (var dy = 0; dy < 2; dy++)
                        {
                            for (var dx = 0; dx < 2; dx++)
                            {
                                var sx = Math.Min((x * 2) + dx, sourceWidth - 1);
                                var sy = Math.Min((y * 2) + dy, sourceHeight - 1);
                                total += source[(((sy * sourceWidth) + sx) * 4) + channel];
                                count++;
                            }
                        }

                        next[(((y * nextWidth) + x) * 4) + channel] = (byte)(total / count);
                    }
                }
            }

            levels.Add(next);
            source = next;
            sourceWidth = nextWidth;
            sourceHeight = nextHeight;
        }

        return [.. levels];
    }
}
