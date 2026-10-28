// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Diagnostics;

namespace Sharpcraft.Common.Random;

/// <summary>
/// 48-bit linear congruential generator, bit-compatible with Minecraft's
/// <c>LegacyRandomSource</c> (and therefore with <c>java.util.Random</c>).
/// See docs/analysis/common/random.md.
/// </summary>
/// <remarks>
/// Vanilla's <c>RandomSource.create(seed)</c> returns this implementation, not xoroshiro;
/// entities, block random ticks and particles all draw from it.
/// </remarks>
public sealed class LegacyRandomSource : IRandomSource
{
    private const int ModulusBits = 48;
    private const long ModulusMask = 0xFFFF_FFFF_FFFFL;
    private const long Multiplier = 0x0005_DEEC_E66DL;
    private const long Increment = 0xBL;

    /// <summary>Vanilla widens this float constant; using the exact 2^-53 would change the low bits.</summary>
    private const float FloatMultiplier = 5.9604645E-8f;

    /// <summary>
    /// Vanilla declares this as a <c>double</c> initialised from a float literal, so the
    /// multiplication runs in double precision with a float-rounded constant. Using the
    /// exact 2^-53 (or float arithmetic) changes the low bits of the result.
    /// </summary>
    private const double DoubleMultiplier = 1.110223E-16f;

    private readonly MarsagliaPolarGaussian _gaussian;
    private long _seed;

    /// <summary>Creates a generator seeded exactly like vanilla.</summary>
    public LegacyRandomSource(long seed)
    {
        _gaussian = new MarsagliaPolarGaussian(this);
        SetSeed(seed);
    }

    /// <inheritdoc />
    public void SetSeed(long seed)
    {
        _seed = unchecked((seed ^ Multiplier) & ModulusMask);
        _gaussian.Reset();
    }

    /// <summary>Advances the state and returns the top <paramref name="bits"/> bits.</summary>
    [HotPath("per-tick random draws")]
    public int NextBits(int bits)
    {
        _seed = unchecked((_seed * Multiplier + Increment) & ModulusMask);
        return unchecked((int)(_seed >> (ModulusBits - bits)));
    }

    /// <inheritdoc />
    [HotPath("per-tick random draws")]
    public int NextInt() => NextBits(32);

    /// <inheritdoc />
    [HotPath("per-tick random draws")]
    public int NextInt(int bound)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bound);

        if ((bound & (bound - 1)) == 0)
        {
            return unchecked((int)(((long)bound * NextBits(31)) >> 31));
        }

        int sample;
        int modulo;
        do
        {
            sample = NextBits(31);
            modulo = sample % bound;
        }
        while (unchecked(sample - modulo + (bound - 1) < 0));

        return modulo;
    }

    /// <inheritdoc />
    [HotPath("per-tick random draws")]
    public long NextLong()
    {
        var upper = NextBits(32);
        var lower = NextBits(32);
        return unchecked(((long)upper << 32) + lower);
    }

    /// <inheritdoc />
    [HotPath("per-tick random draws")]
    public bool NextBoolean() => NextBits(1) != 0;

    /// <inheritdoc />
    public double NextGaussian() => _gaussian.Next();

    /// <summary>Creates an independent child source (vanilla <c>fork()</c>).</summary>
    public IRandomSource Fork() => new LegacyRandomSource(NextLong());

    /// <summary>Creates a positional factory seeded from the next draw (vanilla <c>forkPositional()</c>).</summary>
    public IPositionalRandomFactory ForkPositional() => new LegacyPositionalRandomFactory(NextLong());


    /// <inheritdoc />
    [HotPath("per-tick random draws")]
    public float NextFloat() => NextBits(24) * FloatMultiplier;

    /// <inheritdoc />
    [HotPath("per-tick random draws")]
    public double NextDouble()
    {
        var upper = NextBits(26);
        var lower = NextBits(27);
        var combined = unchecked(((long)upper << 27) + lower);
        return combined * DoubleMultiplier;
    }
}
