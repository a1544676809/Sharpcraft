// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Identifiers;

namespace Sharpcraft.Common.Random;

/// <summary>
/// Per-key reproducible random sequence (used by loot tables in 26.3): the stream
/// depends only on the world seed and the key, never on call order.
/// </summary>
public sealed class RandomSequence
{
    private readonly XoroshiroRandomSource _source;

    /// <summary>Creates a sequence for a world seed and an optional key.</summary>
    public RandomSequence(long seed, Identifier? key)
    {
        var (lo, hi) = SeedSupport.UpgradeTo128BitUnmixed(seed);

        if (key is { } identifier)
        {
            var (keyLo, keyHi) = SeedFromKey(identifier);
            lo ^= keyLo;
            hi ^= keyHi;
        }

        _source = new XoroshiroRandomSource(SeedSupport.MixStafford13(lo), SeedSupport.MixStafford13(hi));
    }

    /// <summary>The sequence's random source.</summary>
    public IRandomSource Random => _source;

    /// <summary>Seed contributed by a key (MD5 of the identifier text).</summary>
    public static (long Lo, long Hi) SeedFromKey(Identifier key) => SeedSupport.SeedFromHashOf(key.ToString());
}
