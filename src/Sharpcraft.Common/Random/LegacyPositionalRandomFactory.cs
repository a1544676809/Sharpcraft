// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Common.Random;

/// <summary>
/// Legacy positional factory: derives LCG seeds with XOR of a positional hash, and
/// keys names with <see cref="JavaStringHash"/> (not MD5).
/// </summary>
public sealed class LegacyPositionalRandomFactory : IPositionalRandomFactory
{
    private readonly long _seed;

    /// <summary>Creates the factory from a seed.</summary>
    public LegacyPositionalRandomFactory(long seed) => _seed = seed;

    /// <inheritdoc />
    public IRandomSource At(int x, int y, int z) => new LegacyRandomSource(PositionalSeed.Of(x, y, z) ^ _seed);

    /// <inheritdoc />
    public IRandomSource FromHashOf(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return new LegacyRandomSource(JavaStringHash.Of(name) ^ _seed);
    }

    /// <inheritdoc />
    public IRandomSource FromSeed(long seed) => new LegacyRandomSource(seed);
}
