// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 MinecraftCSharp contributors

namespace MinecraftCSharp.Common.Random;

/// <summary>
/// Xoroshiro positional factory: only the low word is XOR-ed with a positional hash,
/// and names are keyed with MD5 instead of a Java string hash.
/// </summary>
public sealed class XoroshiroPositionalRandomFactory : IPositionalRandomFactory
{
    private readonly long _seedLo;
    private readonly long _seedHi;

    /// <summary>Creates the factory from the two halves of a 128-bit seed.</summary>
    public XoroshiroPositionalRandomFactory(long seedLo, long seedHi)
    {
        _seedLo = seedLo;
        _seedHi = seedHi;
    }

    /// <inheritdoc />
    public IRandomSource At(int x, int y, int z) =>
        new XoroshiroRandomSource(PositionalSeed.Of(x, y, z) ^ _seedLo, _seedHi);

    /// <inheritdoc />
    public IRandomSource FromHashOf(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var (lo, hi) = SeedSupport.SeedFromHashOf(name);
        return new XoroshiroRandomSource(lo ^ _seedLo, hi ^ _seedHi);
    }

    /// <inheritdoc />
    public IRandomSource FromSeed(long seed) => new XoroshiroRandomSource(seed ^ _seedLo, seed ^ _seedHi);
}
