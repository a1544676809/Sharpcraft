// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 MinecraftCSharp contributors

namespace MinecraftCSharp.Common.World;

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
        var data = Pack(level);
        for (var i = 0; i < _sky.Length; i++)
        {
            _sky[i] = data;
        }
    }

    /// <summary>Fills every block light section with the same level.</summary>
    public void FillBlockLight(byte level)
    {
        var data = Pack(level);
        for (var i = 0; i < _block.Length; i++)
        {
            _block[i] = data;
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
