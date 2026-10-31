// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Assets;

/// <summary>
/// Builds the block atlas mip chain the way the reference does.
/// </summary>
/// <remarks>
/// Ported from the reference algorithm: fully transparent texels are first filled from their nearest opaque neighbour
/// (<c>TextureUtil.solidify</c>), each level averages colour in linear space while alpha is averaged plainly
/// (<c>ARGB.meanLinear</c>), and for a texture with holes the level's alpha is rescaled so the alpha test keeps the same
/// coverage as level zero (<c>MipmapGenerator.scaleAlphaToCoverage</c>). Without that last step the silhouette erodes level
/// by level and the texture dissolves into a checkerboard at distance.
/// Reference: ref://mc-26.3-analysis/src/net/minecraft/client/renderer/texture/MipmapGenerator.java
/// and ref://mc-26.3-analysis/src/com/mojang/blaze3d/platform/ARGB.java.
/// </remarks>
public static class AtlasMipChain
{
    /// <summary>Levels generated below level zero.</summary>
    public const int Levels = 4;

    /// <summary>Alpha test threshold the reference uses for cutout terrain.</summary>
    public const float AlphaCutoff = 0.5f;

    /// <summary>Subsamples per axis when measuring alpha test coverage.</summary>
    private const int Subsamples = 4;

    private static readonly float[] SrgbToLinear = BuildSrgbToLinear();
    private static readonly byte[] LinearToSrgb = BuildLinearToSrgb();

    /// <summary>
    /// Builds every mip level of an atlas.
    /// </summary>
    /// <param name="rgba">Level zero pixels, four bytes per texel.</param>
    /// <param name="width">Atlas width.</param>
    /// <param name="height">Atlas height.</param>
    /// <param name="sprites">Sprite rectangles, processed independently like the reference does per sprite.</param>
    /// <returns>One array per level, level one upwards, sized like the atlas halved each time.</returns>
    public static byte[][] Build(byte[] rgba, int width, int height, IReadOnlyList<SpriteRect> sprites)
    {
        ArgumentNullException.ThrowIfNull(rgba);
        ArgumentNullException.ThrowIfNull(sprites);

        var levels = new List<byte[]>(Levels);
        var levelWidth = width;
        var levelHeight = height;

        for (var level = 0; level < Levels; level++)
        {
            levelWidth = Math.Max(1, levelWidth / 2);
            levelHeight = Math.Max(1, levelHeight / 2);
            levels.Add(new byte[levelWidth * levelHeight * 4]);
        }

        foreach (var sprite in sprites)
        {
            BuildSprite(rgba, width, height, sprite, levels);
        }

        return [.. levels];
    }

    private static void BuildSprite(byte[] rgba, int width, int height, SpriteRect sprite, List<byte[]> levels)
    {
        var spriteWidth = Math.Min(sprite.Width, width - sprite.X);
        var spriteHeight = Math.Min(sprite.Height, height - sprite.Y);

        if (spriteWidth <= 0 || spriteHeight <= 0)
        {
            return;
        }

        // Level zero copy with the transparent texels filled in, so minification never pulls black out of a hole.
        var current = new byte[spriteWidth * spriteHeight * 4];

        for (var y = 0; y < spriteHeight; y++)
        {
            for (var x = 0; x < spriteWidth; x++)
            {
                var source = ((((sprite.Y + y) * width) + sprite.X + x) * 4);
                var target = (((y * spriteWidth) + x) * 4);
                current[target] = rgba[source];
                current[target + 1] = rgba[source + 1];
                current[target + 2] = rgba[source + 2];
                current[target + 3] = rgba[source + 3];
            }
        }

        var hasHoles = false;

        for (var index = 3; index < current.Length; index += 4)
        {
            if (current[index] != 255)
            {
                hasHoles = true;
                break;
            }
        }

        Solidify(current, spriteWidth, spriteHeight);

        var desiredCoverage = hasHoles ? AlphaTestCoverage(current, spriteWidth, spriteHeight, AlphaCutoff, 1f) : 0f;
        var currentWidth = spriteWidth;
        var currentHeight = spriteHeight;

        for (var level = 0; level < levels.Count; level++)
        {
            var nextWidth = Math.Max(1, currentWidth / 2);
            var nextHeight = Math.Max(1, currentHeight / 2);

            if (currentWidth == 1 && currentHeight == 1)
            {
                break;
            }

            var next = new byte[nextWidth * nextHeight * 4];

            for (var y = 0; y < nextHeight; y++)
            {
                for (var x = 0; x < nextWidth; x++)
                {
                    var x0 = Math.Min(x * 2, currentWidth - 1);
                    var x1 = Math.Min((x * 2) + 1, currentWidth - 1);
                    var y0 = Math.Min(y * 2, currentHeight - 1);
                    var y1 = Math.Min((y * 2) + 1, currentHeight - 1);

                    var c1 = (((y0 * currentWidth) + x0) * 4);
                    var c2 = (((y0 * currentWidth) + x1) * 4);
                    var c3 = (((y1 * currentWidth) + x0) * 4);
                    var c4 = (((y1 * currentWidth) + x1) * 4);
                    var target = (((y * nextWidth) + x) * 4);

                    next[target] = LinearChannelMean(current[c1], current[c2], current[c3], current[c4]);
                    next[target + 1] = LinearChannelMean(current[c1 + 1], current[c2 + 1], current[c3 + 1], current[c4 + 1]);
                    next[target + 2] = LinearChannelMean(current[c1 + 2], current[c2 + 2], current[c3 + 2], current[c4 + 2]);
                    next[target + 3] = (byte)((current[c1 + 3] + current[c2 + 3] + current[c3 + 3] + current[c4 + 3]) / 4);
                }
            }

            if (hasHoles)
            {
                ScaleAlphaToCoverage(next, nextWidth, nextHeight, desiredCoverage);
            }

            // Place the level into the atlas sized level array.
            var destination = levels[level];
            var destinationWidth = Math.Max(1, width >> (level + 1));
            var destinationHeight = Math.Max(1, height >> (level + 1));
            var originX = sprite.X >> (level + 1);
            var originY = sprite.Y >> (level + 1);

            for (var y = 0; y < nextHeight; y++)
            {
                var destinationY = originY + y;

                if (destinationY >= destinationHeight)
                {
                    break;
                }

                for (var x = 0; x < nextWidth; x++)
                {
                    var destinationX = originX + x;

                    if (destinationX >= destinationWidth)
                    {
                        break;
                    }

                    var source = (((y * nextWidth) + x) * 4);
                    var target = (((destinationY * destinationWidth) + destinationX) * 4);
                    destination[target] = next[source];
                    destination[target + 1] = next[source + 1];
                    destination[target + 2] = next[source + 2];
                    destination[target + 3] = next[source + 3];
                }
            }

            // Repeat this level's edge one texel outward, matching the padding ring the stitcher leaves at level zero, so
            // minification near a sprite border still reads the sprite.
            for (var y = 0; y < nextHeight; y++)
            {
                var destinationY = originY + y;

                if (destinationY >= destinationHeight)
                {
                    break;
                }

                var left = (((destinationY * destinationWidth) + originX) * 4);
                var right = (((destinationY * destinationWidth) + Math.Min(originX + nextWidth - 1, destinationWidth - 1)) * 4);

                if (originX > 0)
                {
                    Buffer.BlockCopy(destination, left, destination, left - 4, 4);
                }

                if (originX + nextWidth < destinationWidth)
                {
                    Buffer.BlockCopy(destination, right, destination, right + 4, 4);
                }
            }

            if (originY > 0)
            {
                var rowBytes = Math.Min(nextWidth, destinationWidth - originX) * 4;
                Buffer.BlockCopy(
                    destination,
                    ((originY * destinationWidth) + originX) * 4,
                    destination,
                    (((originY - 1) * destinationWidth) + originX) * 4,
                    rowBytes);
            }

            if (originY + nextHeight < destinationHeight)
            {
                var rowBytes = Math.Min(nextWidth, destinationWidth - originX) * 4;
                Buffer.BlockCopy(
                    destination,
                    (((originY + nextHeight - 1) * destinationWidth) + originX) * 4,
                    destination,
                    (((originY + nextHeight) * destinationWidth) + originX) * 4,
                    rowBytes);
            }

            current = next;
            currentWidth = nextWidth;
            currentHeight = nextHeight;
        }
    }

    private static void Solidify(byte[] pixels, int width, int height)
    {
        // Flood fill: every fully transparent texel takes the colour of its nearest opaque neighbour, which is the
        // reference's TextureUtil.solidify.
        var queue = new Queue<(int X, int Y)>();
        var visited = new bool[width * height];

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (pixels[(((y * width) + x) * 4) + 3] != 0)
                {
                    queue.Enqueue((x, y));
                    visited[(y * width) + x] = true;
                }
            }
        }

        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();
            var source = (((y * width) + x) * 4);

            for (var direction = 0; direction < 4; direction++)
            {
                var nx = x + (direction == 0 ? 1 : direction == 1 ? -1 : 0);
                var ny = y + (direction == 2 ? 1 : direction == 3 ? -1 : 0);

                if (nx < 0 || ny < 0 || nx >= width || ny >= height || visited[(ny * width) + nx])
                {
                    continue;
                }

                var target = (((ny * width) + nx) * 4);

                if (pixels[target + 3] == 0)
                {
                    pixels[target] = pixels[source];
                    pixels[target + 1] = pixels[source + 1];
                    pixels[target + 2] = pixels[source + 2];
                }

                visited[(ny * width) + nx] = true;
                queue.Enqueue((nx, ny));
            }
        }
    }

    private static float AlphaTestCoverage(byte[] pixels, int width, int height, float reference, float scale)
    {
        if (width < 2 || height < 2)
        {
            return 0f;
        }

        var covered = 0f;
        var cells = 0;

        for (var y = 0; y < height - 1; y++)
        {
            for (var x = 0; x < width - 1; x++)
            {
                var a00 = Math.Clamp((pixels[(((y * width) + x) * 4) + 3] / 255f) * scale, 0f, 1f);
                var a10 = Math.Clamp((pixels[(((y * width) + x + 1) * 4) + 3] / 255f) * scale, 0f, 1f);
                var a01 = Math.Clamp((pixels[((((y + 1) * width) + x) * 4) + 3] / 255f) * scale, 0f, 1f);
                var a11 = Math.Clamp((pixels[((((y + 1) * width) + x + 1) * 4) + 3] / 255f) * scale, 0f, 1f);

                var hits = 0;

                for (var subY = 0; subY < Subsamples; subY++)
                {
                    var fy = (subY + 0.5f) / Subsamples;

                    for (var subX = 0; subX < Subsamples; subX++)
                    {
                        var fx = (subX + 0.5f) / Subsamples;
                        var alpha = (a00 * (1f - fx) * (1f - fy)) + (a10 * fx * (1f - fy)) +
                                    (a01 * (1f - fx) * fy) + (a11 * fx * fy);

                        if (alpha > reference)
                        {
                            hits++;
                        }
                    }
                }

                covered += hits / (float)(Subsamples * Subsamples);
                cells++;
            }
        }

        return cells == 0 ? 0f : covered / cells;
    }

    private static void ScaleAlphaToCoverage(byte[] pixels, int width, int height, float desiredCoverage)
    {
        var minimum = 0f;
        var maximum = 4f;
        var scale = 1f;
        var bestScale = 1f;
        var bestError = float.MaxValue;

        for (var iteration = 0; iteration < 5; iteration++)
        {
            var coverage = AlphaTestCoverage(pixels, width, height, AlphaCutoff, scale);
            var error = MathF.Abs(coverage - desiredCoverage);

            if (error < bestError)
            {
                bestError = error;
                bestScale = scale;
            }

            if (coverage < desiredCoverage)
            {
                minimum = scale;
                scale = (scale + maximum) / 2f;
            }
            else
            {
                maximum = scale;
                scale = (scale + minimum) / 2f;
            }
        }

        if (MathF.Abs(bestScale - 1f) < 0.001f)
        {
            return;
        }

        for (var index = 3; index < pixels.Length; index += 4)
        {
            pixels[index] = (byte)Math.Clamp(pixels[index] * bestScale, 0f, 255f);
        }
    }

    private static byte LinearChannelMean(byte first, byte second, byte third, byte fourth) =>
        LinearToSrgb[(int)((SrgbToLinear[first] + SrgbToLinear[second] + SrgbToLinear[third] + SrgbToLinear[fourth]) / 4f)];

    private static float[] BuildSrgbToLinear()
    {
        var table = new float[256];

        for (var value = 0; value < 256; value++)
        {
            var channel = value / 255f;
            table[value] = channel <= 0.04045f ? channel / 12.92f : MathF.Pow((channel + 0.055f) / 1.055f, 2.4f);
        }

        return table;
    }

    private static byte[] BuildLinearToSrgb()
    {
        var table = new byte[256];

        for (var value = 0; value < 256; value++)
        {
            var linear = value / 255f;
            var channel = linear <= 0.0031308f ? linear * 12.92f : (1.055f * MathF.Pow(linear, 1f / 2.4f)) - 0.055f;
            table[value] = (byte)Math.Clamp(MathF.Round(channel * 255f), 0f, 255f);
        }

        return table;
    }
}
