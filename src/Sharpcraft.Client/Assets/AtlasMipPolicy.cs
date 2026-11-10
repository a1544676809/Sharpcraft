// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Assets;

/// <summary>
/// Derives the block atlas mip level and its padding the way the reference stitcher does.
/// </summary>
/// <remarks>
/// The reference does not pick a fixed number of mip levels. It walks every sprite, takes the lowest set bit of each
/// sprite's width and height, keeps the smallest such bit across the atlas, and then reduces the requested level until
/// it fits: a sprite narrower than <c>1 &lt;&lt; level</c> texels cannot survive that level's halving. The padding that
/// separates sprites is derived from the same level, doubled again by the anisotropy exponent, because a coarse mip
/// samples a wider footprint and would otherwise reach into its neighbour.
///
/// Getting this wrong is visible: a fixed level on a small atlas over-blurs distant ground into flat patches, and a
/// padding that does not follow the level lets neighbouring sprites bleed into each other at distance.
/// </remarks>
public static class AtlasMipPolicy
{
    /// <summary>The level count used when the caller has no option value of its own.</summary>
    /// <remarks>The reference exposes this as the mipmap level option, whose upper bound is four.</remarks>
    public const int DefaultMaxLevels = 4;

    /// <summary>The anisotropy exponent used when anisotropic filtering is off.</summary>
    public const int NoAnisotropy = 0;

    /// <summary>
    /// Resolves how many mip levels below level zero the atlas may carry.
    /// </summary>
    /// <param name="sizes">Every sprite's pixel size.</param>
    /// <param name="maxLevels">Level count requested by the option.</param>
    /// <returns>A level count between zero and <paramref name="maxLevels"/>.</returns>
    public static int ResolveLevels(ReadOnlySpan<(int Width, int Height)> sizes, int maxLevels)
    {
        if (maxLevels <= 0)
        {
            return 0;
        }

        var lowestOneBit = 1 << maxLevels;
        var minTexelSize = int.MaxValue;
        foreach (var (width, height) in sizes)
        {
            if (width <= 0 || height <= 0)
            {
                continue;
            }

            minTexelSize = Math.Min(minTexelSize, Math.Min(width, height));
            var lowestTextureBit = Math.Min(LowestOneBit(width), LowestOneBit(height));
            if (lowestTextureBit < lowestOneBit)
            {
                lowestOneBit = lowestTextureBit;
            }
        }

        if (minTexelSize == int.MaxValue)
        {
            return 0;
        }

        var minSize = Math.Min(minTexelSize, lowestOneBit);
        var minPowerOfTwo = Log2(minSize);
        return minPowerOfTwo < maxLevels ? minPowerOfTwo : maxLevels;
    }

    /// <summary>
    /// Resolves the texels of padding placed around every atlas sprite.
    /// </summary>
    /// <param name="mipLevels">Level count from <see cref="ResolveLevels"/>.</param>
    /// <param name="anisotropyBit">Anisotropy exponent, or <see cref="NoAnisotropy"/> when filtering is not anisotropic.</param>
    /// <returns>The padding in texels, never below <see cref="MinimumPadding"/>.</returns>
    public static int ResolvePadding(int mipLevels, int anisotropyBit)
    {
        var padding = 1 << Math.Max(0, mipLevels);
        padding <<= Math.Clamp(anisotropyBit - 1, 0, 4);

        // Two texels is the floor even at level zero: the uv range ends exactly on the sprite boundary, so a bilinear
        // sample there reads one texel past it.
        return Math.Max(MinimumPadding, padding);
    }

    /// <summary>Padding that keeps a bilinear sample at the sprite edge inside its own ring.</summary>
    public const int MinimumPadding = 2;

    private static int LowestOneBit(int value) => value & -value;

    private static int Log2(int value)
    {
        var result = 0;
        while (value > 1)
        {
            value >>= 1;
            result++;
        }

        return result;
    }
}
