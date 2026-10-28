// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Diagnostics;

namespace Sharpcraft.Common.Random;

/// <summary>
/// xoroshiro128++ state machine (the generator behind Minecraft's
/// <c>XoroshiroRandomSource</c>), bit-compatible with 26.3.
/// See docs/analysis/common/random.md.
/// </summary>
public sealed class Xoroshiro128PlusPlus
{
    private const long FallbackLo = unchecked((long)0x9E37_79B9_7F4A_7C15UL);
    private const long FallbackHi = unchecked((long)0x6A09_E667_F3BC_C909UL);

    private ulong _lo;
    private ulong _hi;

    /// <summary>Creates the generator from a 128-bit seed.</summary>
    public Xoroshiro128PlusPlus(long seedLo, long seedHi)
    {
        _lo = unchecked((ulong)seedLo);
        _hi = unchecked((ulong)seedHi);

        // Vanilla replaces an all-zero state with the golden/silver ratio pair.
        if ((_lo | _hi) == 0UL)
        {
            _lo = unchecked((ulong)FallbackLo);
            _hi = unchecked((ulong)FallbackHi);
        }
    }

    /// <summary>Current low word (exposed for state snapshots).</summary>
    public long StateLo => unchecked((long)_lo);

    /// <summary>Current high word (exposed for state snapshots).</summary>
    public long StateHi => unchecked((long)_hi);

    /// <summary>Produces the next 64-bit value and advances the state.</summary>
    [HotPath("world generation and loot draws")]
    public long NextLong()
    {
        var s0 = _lo;
        var s1 = _hi;

        var result = unchecked(s0 + s1);
        result = (result << 17) | (result >> 47); // rotate left 17
        result = unchecked(result + s0);

        s1 ^= s0;
        var shifted = unchecked(s1 << 21);
        var rotated = (s0 << 49) | (s0 >> 15); // rotate left 49
        _lo = rotated ^ s1 ^ shifted;
        _hi = (s1 << 28) | (s1 >> 36); // rotate left 28

        return unchecked((long)result);
    }
}
