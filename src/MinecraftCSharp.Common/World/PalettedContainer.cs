// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 MinecraftCSharp contributors

namespace MinecraftCSharp.Common.World;

/// <summary>
/// Paletted container with the packed layout used on the wire
/// (docs/analysis/common/registry-order.md 5, docs/specs/network/protocol-phases.md).
/// </summary>
/// <remarks>
/// Two properties matter for compatibility:
/// <list type="bullet">
///   <item>entries are packed 16/32/64 bits at a time and <b>never cross a long boundary</b>;</item>
///   <item>palette indices are assigned in first-seen order, so growth must stay stable.</item>
/// </list>
/// Vanilla uses three strategies (single value, local palette, global direct); this type supports
/// the first two for writing and all three for reading.
/// </remarks>
public sealed class PalettedContainer
{
    /// <summary>Maximum bits per entry used with a local palette.</summary>
    public const int MaxLocalBits = 8;

    /// <summary>Bits used by the direct (global) strategy for block states.</summary>
    public const int DirectBits = 15;

    private readonly int _size;
    private readonly bool _direct;
    private int[] _palette;
    private long[] _data;
    private int _bitsPerEntry;

    /// <summary>Creates a container holding a single value everywhere.</summary>
    public PalettedContainer(int size, int singleValue)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);
        _size = size;
        _palette = [singleValue];
        _bitsPerEntry = 0;
        _data = [];
    }

    /// <summary>Wraps already unpacked data (parsing path).</summary>
    /// <param name="size">Number of entries.</param>
    /// <param name="bitsPerEntry">Width of one entry.</param>
    /// <param name="palette">Local palette, or empty when <paramref name="direct"/>.</param>
    /// <param name="data">Packed entries.</param>
    /// <param name="direct">True when packed values are global ids rather than palette indices.</param>
    public PalettedContainer(int size, int bitsPerEntry, int[] palette, long[] data, bool direct = false)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);

        _size = size;
        _bitsPerEntry = bitsPerEntry;
        _palette = palette;
        _data = data;
        _direct = direct;

        if (bitsPerEntry < 0 || bitsPerEntry > 32)
        {
            throw new ArgumentOutOfRangeException(nameof(bitsPerEntry), bitsPerEntry, "unsupported bits per entry");
        }

        if (bitsPerEntry > 0 && data.Length != ExpectedLongs(size, bitsPerEntry))
        {
            throw new ArgumentException(
                $"expected {ExpectedLongs(size, bitsPerEntry)} longs for {size} entries at {bitsPerEntry} bits, got {data.Length}",
                nameof(data));
        }
    }

    /// <summary>Number of entries in the container.</summary>
    public int Size => _size;

    /// <summary>Bits per entry; 0 means a single value covers the container.</summary>
    public int BitsPerEntry => _bitsPerEntry;

    /// <summary>Current palette (single value, or local palette).</summary>
    public IReadOnlyList<int> Palette => _palette;

    /// <summary>Packed backing data, empty for the single-value strategy.</summary>
    public IReadOnlyList<long> Data => _data;

    /// <summary>True when a single value covers the whole container.</summary>
    public bool IsSingleValue => _bitsPerEntry == 0;

    /// <summary>True when packed values are global ids without a palette.</summary>
    public bool IsDirect => _direct;

    /// <summary>Number of longs needed for a size/bits combination.</summary>
    public static int ExpectedLongs(int size, int bitsPerEntry)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);

        if (bitsPerEntry == 0)
        {
            return 0;
        }

        var perLong = 64 / bitsPerEntry;
        return (size + perLong - 1) / perLong;
    }

    /// <summary>Reads the value at a linear index.</summary>
    public int Get(int index)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _size);
        ArgumentOutOfRangeException.ThrowIfNegative(index);

        if (_bitsPerEntry == 0)
        {
            return _palette[0];
        }

        var (longIndex, offset) = Locate(index, _bitsPerEntry);
        var mask = (1L << _bitsPerEntry) - 1L;
        var packed = (int)((ulong)(_data[longIndex] >> offset) & (ulong)mask);
        if (_direct)
        {
            return packed;
        }

        return packed < _palette.Length ? _palette[packed] : 0;
    }

    /// <summary>Writes a value, growing the palette when a new value appears.</summary>
    public void Set(int index, int value)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _size);
        ArgumentOutOfRangeException.ThrowIfNegative(index);

        var paletteIndex = Array.IndexOf(_palette, value);
        if (paletteIndex < 0)
        {
            // Only widen when the current width cannot address one more palette entry;
            // widening on every new value would burn the whole bit budget.
            var required = BitsFor(_palette.Length + 1);
            if (required <= _bitsPerEntry)
            {
                _palette = [.. _palette, value];
                paletteIndex = _palette.Length - 1;
            }
            else
            {
                if (required > MaxLocalBits)
                {
                    throw new InvalidOperationException("local palette is full; this container cannot grow further");
                }

                Grow(required, value);
                paletteIndex = Array.IndexOf(_palette, value);
            }
        }

        if (_bitsPerEntry == 0)
        {
            // Value already present everywhere: nothing to write.
            return;
        }

        var (longIndex, offset) = Locate(index, _bitsPerEntry);
        var mask = (1L << _bitsPerEntry) - 1L;
        _data[longIndex] = (_data[longIndex] & ~(mask << offset)) | ((long)paletteIndex & mask) << offset;
    }

    /// <summary>Bits needed to address <paramref name="paletteSize"/> entries, with the minimum of two.</summary>
    public static int BitsFor(int paletteSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(paletteSize);

        var bits = 1;
        while ((1 << bits) < paletteSize)
        {
            bits++;
        }

        return Math.Max(2, bits);
    }

    /// <summary>Counts entries that are not <paramref name="emptyValue"/>.</summary>
    public int CountNotEqualTo(int emptyValue)
    {
        var count = 0;
        for (var i = 0; i < _size; i++)
        {
            if (Get(i) != emptyValue)
            {
                count++;
            }
        }

        return count;
    }

    private static (int LongIndex, int Offset) Locate(int index, int bitsPerEntry)
    {
        var perLong = 64 / bitsPerEntry;
        return (index / perLong, (index % perLong) * bitsPerEntry);
    }

    private void Grow(int newBits, int extraValue)
    {
        var palette = new List<int>(_palette) { extraValue };
        var data = new long[ExpectedLongs(_size, newBits)];

        if (_bitsPerEntry > 0)
        {
            for (var i = 0; i < _size; i++)
            {
                var existing = Get(i);
                var paletteIndex = palette.IndexOf(existing);
                var (longIndex, offset) = Locate(i, newBits);
                data[longIndex] |= ((long)paletteIndex & ((1L << newBits) - 1)) << offset;
            }
        }
        else
        {
            // Every entry keeps the single value it had, which is palette index 0.
            for (var i = 0; i < _size; i++)
            {
                var (longIndex, offset) = Locate(i, newBits);
                data[longIndex] |= 0L << offset;
            }
        }

        _bitsPerEntry = newBits;
        _palette = [.. palette];
        _data = data;
    }
}
