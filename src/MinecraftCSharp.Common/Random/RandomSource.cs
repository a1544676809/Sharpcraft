// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 MinecraftCSharp contributors

namespace MinecraftCSharp.Common.Random;

/// <summary>
/// Factory mirroring Minecraft 26.3's <c>RandomSource.create</c>.
/// </summary>
/// <remarks>
/// Vanilla's factory returns a <see cref="LegacyRandomSource"/> (48-bit LCG), not
/// xoroshiro; this was confirmed both in source and by comparing observed streams
/// (docs/analysis/common/random-vectors.md).
/// </remarks>
public static class RandomSource
{
    /// <summary>Creates the default generator for a seed.</summary>
    public static IRandomSource Create(long seed) => new LegacyRandomSource(seed);

    /// <summary>Creates a generator that consumes nothing on creation (deterministic seeds).</summary>
    public static IRandomSource CreateDeterministic(long seed) => new LegacyRandomSource(seed);
}
