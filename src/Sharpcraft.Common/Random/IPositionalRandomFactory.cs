// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Common.Random;

/// <summary>
/// Derives child random sources from a position or a name, mirroring Minecraft's
/// <c>PositionalRandomFactory</c>.
/// </summary>
public interface IPositionalRandomFactory
{
    /// <summary>Random source for a block position.</summary>
    IRandomSource At(int x, int y, int z);

    /// <summary>Random source derived from a name (the two implementations hash it differently).</summary>
    IRandomSource FromHashOf(string name);

    /// <summary>Random source from an explicit seed.</summary>
    IRandomSource FromSeed(long seed);
}
