// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 MinecraftCSharp contributors

namespace MinecraftCSharp.Common.Random;

/// <summary>
/// Random source with the semantics of Minecraft 26.3's <c>RandomSource</c>.
/// Implementations are <b>not</b> thread-safe; one instance belongs to one thread.
/// </summary>
public interface IRandomSource
{
    /// <summary>Reseeds the source, resetting any cached derived state.</summary>
    void SetSeed(long seed);

    /// <summary>Next signed 64-bit value.</summary>
    long NextLong();

    /// <summary>Next signed 32-bit value.</summary>
    int NextInt();

    /// <summary>Next value in <c>[0, bound)</c>; <paramref name="bound"/> must be positive.</summary>
    int NextInt(int bound);

    /// <summary>Next boolean; consumes one bit.</summary>
    bool NextBoolean();

    /// <summary>Next float in <c>[0, 1)</c>.</summary>
    float NextFloat();

    /// <summary>Next double in <c>[0, 1)</c>.</summary>
    double NextDouble();
}
