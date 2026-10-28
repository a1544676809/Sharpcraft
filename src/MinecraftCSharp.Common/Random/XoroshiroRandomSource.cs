// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 MinecraftCSharp contributors

using MinecraftCSharp.Common.Diagnostics;

namespace MinecraftCSharp.Common.Random;

/// <summary>
/// Random source backed by <see cref="Xoroshiro128PlusPlus"/>, bit-compatible with
/// Minecraft 26.3's <c>XoroshiroRandomSource</c>.
/// </summary>
/// <remarks>
/// Vanilla uses this generator for world generation and for loot sequences, while
/// <see cref="RandomSource.Create"/> stays on the legacy LCG.
/// </remarks>
public sealed class XoroshiroRandomSource : IRandomSource
{
    /// <summary>Vanilla widens this float constant, so it must not be replaced by the exact 2^-24.</summary>
    private const float FloatMultiplier = 5.9604645E-8f;

    /// <summary>
    /// Declared as a double initialised from a float literal in vanilla, so the product
    /// uses double precision with a float-rounded constant.
    /// </summary>
    private const double DoubleMultiplier = 1.110223E-16f;

    private const ulong LowWordMask = 0xFFFF_FFFFUL;

    private Xoroshiro128PlusPlus _generator;

    /// <summary>Seeds from a single 64-bit value, upgrading it like vanilla.</summary>
    public XoroshiroRandomSource(long seed)
    {
        var (lo, hi) = SeedSupport.UpgradeTo128Bit(seed);
        _generator = new Xoroshiro128PlusPlus(lo, hi);
    }

    /// <summary>Seeds from the two halves of a 128-bit seed.</summary>
    public XoroshiroRandomSource(long seedLo, long seedHi) => _generator = new Xoroshiro128PlusPlus(seedLo, seedHi);

    /// <summary>Current 128-bit state, for snapshots and tests.</summary>
    public (long Lo, long Hi) State => (_generator.StateLo, _generator.StateHi);

    /// <inheritdoc />
    public void SetSeed(long seed)
    {
        var (lo, hi) = SeedSupport.UpgradeTo128Bit(seed);
        _generator = new Xoroshiro128PlusPlus(lo, hi);
    }

    /// <inheritdoc />
    [HotPath("world generation draws")]
    public long NextLong() => _generator.NextLong();

    /// <inheritdoc />
    [HotPath("world generation draws")]
    public int NextInt() => unchecked((int)NextLong());

    /// <inheritdoc />
    [HotPath("world generation draws")]
    public int NextInt(int bound)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bound);

        var unsignedBound = unchecked((uint)bound);
        var randomBits = unchecked((uint)NextInt());
        var multiplied = (ulong)randomBits * unsignedBound;
        var fractional = multiplied & LowWordMask;

        if (fractional < unsignedBound)
        {
            // Unbiased-bucket correction: (-bound mod 2^32) mod bound.
            var threshold = unchecked((uint)(-(long)bound)) % unsignedBound;
            while (fractional < threshold)
            {
                randomBits = unchecked((uint)NextInt());
                multiplied = (ulong)randomBits * unsignedBound;
                fractional = multiplied & LowWordMask;
            }
        }

        return unchecked((int)(multiplied >> 32));
    }

    /// <inheritdoc />
    [HotPath("world generation draws")]
    public bool NextBoolean() => (NextLong() & 1L) != 0L;

    /// <inheritdoc />
    [HotPath("world generation draws")]
    public float NextFloat() => NextBits(24) * FloatMultiplier;

    /// <inheritdoc />
    [HotPath("world generation draws")]
    public double NextDouble() => NextBits(53) * DoubleMultiplier;

    /// <summary>Top <paramref name="bits"/> bits of the next value (logical shift).</summary>
    public long NextBits(int bits) => unchecked((long)((ulong)NextLong() >> (64 - bits)));
}
