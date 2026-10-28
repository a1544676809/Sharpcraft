// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 MinecraftCSharp contributors

namespace MinecraftCSharp.Common.World;

/// <summary>
/// One column of chunk sections. Block coordinates are chunk-local in <c>[0, 15]</c>; the world
/// Y axis spans <see cref="MinSectionY"/>..<see cref="MaxSectionY"/> inclusive.
/// </summary>
public sealed class LevelChunk
{
    private readonly ChunkSection?[] _sections;

    /// <summary>Creates a chunk with the given vertical section range.</summary>
    /// <param name="chunkX">Chunk coordinate along X.</param>
    /// <param name="chunkZ">Chunk coordinate along Z.</param>
    /// <param name="minSectionY">Lowest section index.</param>
    /// <param name="sectionCount">Number of sections.</param>
    public LevelChunk(int chunkX, int chunkZ, int minSectionY, int sectionCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sectionCount);

        ChunkX = chunkX;
        ChunkZ = chunkZ;
        MinSectionY = minSectionY;
        _sections = new ChunkSection[sectionCount];
    }

    /// <summary>Chunk coordinate along X.</summary>
    public int ChunkX { get; }

    /// <summary>Chunk coordinate along Z.</summary>
    public int ChunkZ { get; }

    /// <summary>Lowest section index (for a 1.18+ overworld this is -4).</summary>
    public int MinSectionY { get; }

    /// <summary>Highest section index.</summary>
    public int MaxSectionY => MinSectionY + _sections.Length - 1;

    /// <summary>Number of sections in the column.</summary>
    public int SectionCount => _sections.Length;

    /// <summary>Whether the chunk has been filled from the network.</summary>
    public bool Loaded { get; private set; }

    /// <summary>Section at an index, or null when absent.</summary>
    public ChunkSection? GetSection(int sectionY) =>
        sectionY >= MinSectionY && sectionY <= MaxSectionY ? _sections[sectionY - MinSectionY] : null;

    /// <summary>Installs a section; later duplicates replace the previous one.</summary>
    public void SetSection(ChunkSection section)
    {
        ArgumentNullException.ThrowIfNull(section);

        if (section.SectionY < MinSectionY || section.SectionY > MaxSectionY)
        {
            throw new ArgumentOutOfRangeException(
                nameof(section), section.SectionY, $"section outside {MinSectionY}..{MaxSectionY}");
        }

        _sections[section.SectionY - MinSectionY] = section;
    }

    /// <summary>Marks the chunk as loaded.</summary>
    public void MarkLoaded() => Loaded = true;

    /// <summary>Block state at chunk-local coordinates, or <paramref name="fallbackState"/> when absent.</summary>
    public int GetBlockState(int x, int y, int z, int fallbackState = 0)
    {
        if (x is < 0 or > 15 || z is < 0 or > 15)
        {
            throw new ArgumentOutOfRangeException(nameof(x), $"section-local coordinates must be 0..15, got ({x},{z})");
        }

        var sectionY = y >> ChunkSection.SideBits;
        var localY = y & 15;
        var section = GetSection(sectionY);
        return section is null ? fallbackState : section.Get(x, localY, z);
    }

    /// <summary>Writes a block state at chunk-local coordinates.</summary>
    public void SetBlockState(int x, int y, int z, int state, int airState = 0)
    {
        var sectionY = y >> ChunkSection.SideBits;
        var localY = y & 15;
        GetSection(sectionY)?.Set(x, localY, z, state, airState);
    }
}
