// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Common.World;

/// <summary>
/// Light storage for one chunk column, mirroring the wire format of
/// <c>level_chunk_with_light</c>: one 2048-byte nibble array per 16x16x16 light section, with two
/// extra sections (one below the lowest block section and one above the highest) because light
/// propagates across the block bounds.
/// </summary>
/// <remarks>
/// A nibble holds a level 0..15; the low nibble of a byte is the even-x sample, as in the protocol.
/// Arrays are kept as the network sent them, indexed by light section, so no reordering cost is
/// paid on chunk load.
/// </remarks>
public sealed class ChunkLight
{
    /// <summary>Bytes per light section (16x16x16 nibbles).</summary>
    public const int BytesPerSection = 2048;

    private readonly byte[]?[] _sky;
    private readonly byte[]?[] _block;

    /// <summary>Creates light storage for a column with the given block section range.</summary>
    /// <param name="minSectionY">Lowest block section index.</param>
    /// <param name="sectionCount">Number of block sections.</param>
    public ChunkLight(int minSectionY, int sectionCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sectionCount);

        MinSectionY = minSectionY;
        SectionCount = sectionCount;
        _sky = new byte[]?[sectionCount + 2];
        _block = new byte[]?[sectionCount + 2];
    }

    /// <summary>Lowest block section index.</summary>
    public int MinSectionY { get; }

    /// <summary>Number of block sections.</summary>
    public int SectionCount { get; }

    /// <summary>Number of light sections (block sections plus one below and one above).</summary>
    public int LightSectionCount => SectionCount + 2;

    /// <summary>Whether any sky light array is present.</summary>
    public bool HasSkyLight => _sky.Any(array => array is not null);

    /// <summary>Whether any block light array is present.</summary>
    public bool HasBlockLight => _block.Any(array => array is not null);

    /// <summary>Sky light level (0..15) at a light-section-local coordinate.</summary>
    public byte GetSkyLight(int lightSection, int x, int y, int z) =>
        Read(_sky, lightSection, x, y, z);

    /// <summary>Block light level (0..15) at a light-section-local coordinate.</summary>
    public byte GetBlockLight(int lightSection, int x, int y, int z) =>
        Read(_block, lightSection, x, y, z);

    /// <summary>
    /// Installs a raw light array. Out-of-range sections are ignored rather than throwing, because
    /// a server may send sections the client does not model.
    /// </summary>
    public void SetSkyLight(int lightSection, byte[]? data)
    {
        if (data is not null && data.Length != BytesPerSection)
        {
            throw new ArgumentException($"expected {BytesPerSection} bytes, got {data.Length}", nameof(data));
        }

        if (lightSection >= 0 && lightSection < _sky.Length)
        {
            _sky[lightSection] = data;
        }
    }

    /// <summary>Installs a raw block light array.</summary>
    public void SetBlockLight(int lightSection, byte[]? data)
    {
        if (data is not null && data.Length != BytesPerSection)
        {
            throw new ArgumentException($"expected {BytesPerSection} bytes, got {data.Length}", nameof(data));
        }

        if (lightSection >= 0 && lightSection < _block.Length)
        {
            _block[lightSection] = data;
        }
    }

    /// <summary>Fills every light section with the same level; the top section is assumed lit.</summary>
    public void FillSkyLight(byte level)
    {
        // One array per section: sharing a single array made every section the same buffer, so the per cell writes the
        // light engine does afterwards changed every section at once.
        for (var i = 0; i < _sky.Length; i++)
        {
            _sky[i] = Pack(level);
        }
    }

    /// <summary>Fills every block light section with the same level.</summary>
    public void FillBlockLight(byte level)
    {
        for (var i = 0; i < _block.Length; i++)
        {
            _block[i] = Pack(level);
        }
    }

    /// <summary>Builds a whole section of one repeated level.</summary>
    private static byte[] Pack(byte level)
    {
        var value = (byte)((level & 0xF) * 0x11);
        var data = new byte[BytesPerSection];
        Array.Fill(data, value);
        return data;
    }

    /// <summary>
    /// Sky light level at world coordinates, for the client light engine that rewrites one cell at a time.
    /// </summary>
    /// <param name="x">Section-local X, 0..15.</param>
    /// <param name="y">World Y.</param>
    /// <param name="z">Section-local Z, 0..15.</param>
    /// <remarks>
    /// The two extra light sections exist because light crosses the block range, so the world Y maps to
    /// <c>section - MinSectionY + 1</c>; anything outside returns zero rather than throwing, which the propagation
    /// relies on at the top and bottom of the world.
    /// </remarks>
    public byte GetSkyLight(int x, int y, int z) =>
        Read(_sky, LightSectionOf(y), x & 15, y & 15, z & 15);

    /// <summary>Writes one sky light cell, allocating the section array on first write.</summary>
    /// <param name="x">Section-local X, 0..15.</param>
    /// <param name="y">World Y.</param>
    /// <param name="z">Section-local Z, 0..15.</param>
    /// <param name="level">Level to store, clamped to 0..15.</param>
    /// <returns>True when the stored level actually changed.</returns>
    public bool SetSkyLight(int x, int y, int z, byte level)
    {
        var lightSection = LightSectionOf(y);
        if (lightSection < 0 || lightSection >= _sky.Length)
        {
            return false;
        }

        var data = _sky[lightSection] ??= new byte[BytesPerSection];
        var index = ((y & 15) << 8) | ((z & 15) << 4) | (x & 15);
        var at = index >> 1;
        var clamped = (byte)Math.Min((int)level, 15);
        var before = data[at];
        var after = (index & 1) == 0
            ? (byte)((before & 0xF0) | clamped)
            : (byte)((before & 0x0F) | (clamped << 4));

        if (after == before)
        {
            return false;
        }

        data[at] = after;
        return true;
    }

    /// <summary>Light section index that holds a world Y, or -1 when the column has no storage there.</summary>
    /// <param name="y">World Y.</param>
    private int LightSectionOf(int y) => (y >> 4) - MinSectionY + 1;

    private static byte Read(byte[]?[] sections, int lightSection, int x, int y, int z)
    {
        if (lightSection < 0 || lightSection >= sections.Length || sections[lightSection] is not { } data)
        {
            return 0;
        }

        var index = (y << 8) | (z << 4) | x;
        var packed = data[index >> 1];
        return (index & 1) == 0
            ? (byte)(packed & 0xF)
            : (byte)(packed >> 4);
    }
}
