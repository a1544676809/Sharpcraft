// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 MinecraftCSharp contributors

namespace MinecraftCSharp.Common.Random;

/// <summary>
/// Positional hash used to derive per-block-position seeds, equivalent to
/// Minecraft's <c>Mth.getSeed(int, int, int)</c>.
/// </summary>
public static class PositionalSeed
{
    /// <summary>
    /// Hashes a block position. The first multiplication deliberately wraps at 32 bits
    /// and is then sign-extended into the 64-bit mix, exactly like the original.
    /// </summary>
    public static long Of(int x, int y, int z)
    {
        var mixed = unchecked(x * 3129871);
        var seed = unchecked((long)mixed) ^ unchecked(z * 116129781L) ^ y;
        seed = unchecked((seed * seed * 42317861L) + (seed * 11L));
        return seed >> 16;
    }
}
