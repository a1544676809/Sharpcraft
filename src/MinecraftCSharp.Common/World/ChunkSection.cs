// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 MinecraftCSharp contributors

namespace MinecraftCSharp.Common.World;

/// <summary>
/// One 16x16x16 block state section. Index order follows vanilla:
/// <c>(y &lt;&lt; 8) | (z &lt;&lt; 4) | x</c>.
/// </summary>
public sealed class ChunkSection
{
    /// <summary>Blocks per section side.</summary>
    public const int Side = 16;

    /// <summary>Blocks per section axis bits.</summary>
    public const int SideBits = 4;

    /// <summary>Total blocks in a section.</summary>
    public const int Volume = Side * Side * Side;

    private readonly PalettedContainer _states;

    /// <summary>Creates a section filled with one block state.</summary>
    public ChunkSection(int singleState, int sectionY)
    {
        _states = new PalettedContainer(Volume, singleState);
        SectionY = sectionY;
    }

    /// <summary>Creates a section from an already parsed container.</summary>
    public ChunkSection(PalettedContainer states, int sectionY, int nonAirCount)
    {
        _states = states ?? throw new ArgumentNullException(nameof(states));
        SectionY = sectionY;
        NonAirCount = nonAirCount;
    }

    /// <summary>Section index along the world Y axis.</summary>
    public int SectionY { get; }

    /// <summary>Block states in this section.</summary>
    public PalettedContainer States => _states;

    /// <summary>Number of blocks that are not air, as reported by the server.</summary>
    public int NonAirCount { get; private set; }

    /// <summary>True when the section contains only air according to the server.</summary>
    public bool IsEmpty => NonAirCount == 0;

    /// <summary>Linear index inside the section; coordinates must be 0..15.</summary>
    public static int Index(int x, int y, int z) => (y << 8) | (z << 4) | x;

    /// <summary>Reads a block state by section-local coordinates.</summary>
    public int Get(int x, int y, int z) => _states.Get(Index(x, y, z));

    /// <summary>Writes a block state by section-local coordinates.</summary>
    public void Set(int x, int y, int z, int state, int airState = 0)
    {
        var before = _states.Get(Index(x, y, z));
        _states.Set(Index(x, y, z), state);

        var wasAir = before == airState;
        var isAir = state == airState;
        if (wasAir && !isAir)
        {
            NonAirCount++;
        }
        else if (!wasAir && isAir)
        {
            NonAirCount--;
        }
    }
}
