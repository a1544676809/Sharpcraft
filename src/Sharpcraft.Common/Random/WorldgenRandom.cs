// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Common.Random;

/// <summary>
/// World-generation random source: mirrors Minecraft's <c>WorldgenRandom</c>, which only
/// overrides <c>next(bits)</c> and derives every other draw from it.
/// </summary>
/// <remarks>
/// This detail matters: for a xoroshiro delegate the derived draws take the <b>high</b>
/// bits (<c>(int)(value &gt;&gt;&gt; (64 - bits))</c>), so a straight pass-through of
/// <c>nextLong()</c> produces a different stream. Also note the mixer asymmetry:
/// <see cref="SetDecorationSeed"/> forces odd multipliers and adds, while
/// <see cref="SetLargeFeatureSeed"/> multiplies raw values and XORs.
/// </remarks>
public sealed class WorldgenRandom : IRandomSource
{
    /// <summary>Generator algorithm selection, mirroring the data-pack switch.</summary>
    public enum Algorithm
    {
        /// <summary>Legacy 48-bit LCG.</summary>
        Legacy,

        /// <summary>xoroshiro128++ (vanilla default).</summary>
        Xoroshiro
    }

    private const float FloatMultiplier = 5.9604645E-8f;
    private const double DoubleMultiplier = 1.110223E-16f;

    private readonly IRandomSource _source;
    private readonly MarsagliaPolarGaussian _gaussian;
    private int _count;

    /// <summary>Wraps an existing source.</summary>
    public WorldgenRandom(IRandomSource source)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _gaussian = new MarsagliaPolarGaussian(this);
    }

    /// <summary>Number of <c>next(bits)</c> calls, mirroring vanilla's counter.</summary>
    public int Count => _count;

    /// <summary>Creates a source for the given algorithm and seed.</summary>
    public static IRandomSource Create(Algorithm algorithm, long seed) => algorithm switch
    {
        Algorithm.Legacy => new LegacyRandomSource(seed),
        Algorithm.Xoroshiro => new XoroshiroRandomSource(seed),
        _ => throw new ArgumentOutOfRangeException(nameof(algorithm))
    };

    /// <inheritdoc />
    public void SetSeed(long seed)
    {
        _source.SetSeed(seed);
        _gaussian.Reset();
    }

    /// <summary>
    /// Next <paramref name="bits"/> bits. Legacy delegates keep their own bit extraction;
    /// other generators expose the high bits of a 64-bit draw.
    /// </summary>
    public int NextBits(int bits)
    {
        _count++;
        return _source is LegacyRandomSource legacy
            ? legacy.NextBits(bits)
            : unchecked((int)((ulong)_source.NextLong() >> (64 - bits)));
    }

    /// <inheritdoc />
    public int NextInt() => NextBits(32);

    /// <inheritdoc />
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
    public long NextLong()
    {
        var upper = NextBits(32);
        var lower = NextBits(32);
        return unchecked(((long)upper << 32) + lower);
    }

    /// <inheritdoc />
    public bool NextBoolean() => NextBits(1) != 0;

    /// <inheritdoc />
    public float NextFloat() => NextBits(24) * FloatMultiplier;

    /// <inheritdoc />
    public double NextDouble()
    {
        var upper = NextBits(26);
        var lower = NextBits(27);
        var combined = unchecked(((long)upper << 27) + lower);
        return combined * DoubleMultiplier;
    }

    /// <inheritdoc />
    public double NextGaussian() => _gaussian.Next();

    /// <summary>Derives a decoration seed for a chunk and re-seeds the source with it.</summary>
    public long SetDecorationSeed(long seed, int chunkX, int chunkZ)
    {
        SetSeed(seed);
        var xScale = NextLong() | 1L;
        var zScale = NextLong() | 1L;
        var result = unchecked((chunkX * xScale) + (chunkZ * zScale)) ^ seed;
        SetSeed(result);
        return result;
    }

    /// <summary>Derives a feature seed from a base seed, feature index and generation step.</summary>
    public void SetFeatureSeed(long seed, int index, int step) => SetSeed(seed + index + (10_000L * step));

    /// <summary>Derives a large-feature seed for a chunk and re-seeds the source with it.</summary>
    public void SetLargeFeatureSeed(long seed, int chunkX, int chunkZ)
    {
        SetSeed(seed);
        var xScale = NextLong();
        var zScale = NextLong();
        var result = unchecked((chunkX * xScale) ^ (chunkZ * zScale) ^ seed);
        SetSeed(result);
    }

    /// <summary>Derives a large-feature seed with an explicit salt and re-seeds the source.</summary>
    public void SetLargeFeatureWithSalt(long seed, int x, int z, int salt) =>
        SetSeed(unchecked((x * 341873128712L) + (z * 132897987541L) + seed + salt));

    /// <summary>Seed used for slime chunk rolls (vanilla returns a thread-local LCG).</summary>
    public static IRandomSource SeedSlimeChunk(int x, int z, long seed, long salt)
    {
        var positional = unchecked((x * x * 4_987_142) + (x * 5_947_611) + ((long)(z * z) * 4_392_871L) + (z * 389_711));
        return new LegacyRandomSource(unchecked(seed + positional) ^ salt);
    }
}
