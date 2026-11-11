// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Framework.Rendering;

/// <summary>
/// Free interval bookkeeping for one shared page: first fit allocation, coalescing release.
/// </summary>
/// <remarks>
/// <para>
/// A page is one vertex buffer and one index buffer of the same capacity; this type tracks the ranges that are free in
/// either of them. Kept free of GPU types so the allocation rules can be tested without a device (ADR-0029).
/// </para>
/// <para>
/// Intervals are kept sorted by offset, so a release merges with its immediate neighbours and fragmentation stays local.
/// That is what keeps unloading a chunk O(n) in the free list rather than a rebuild of the page.
/// </para>
/// </remarks>
internal sealed class GeometryPageFreeList
{
    private readonly record struct Interval(long Offset, long Length);

    private readonly List<Interval> _free = [];

    /// <summary>Creates a page with the given capacity, entirely free.</summary>
    /// <param name="capacity">Page capacity in bytes (or in indices, the unit is the caller's).</param>
    public GeometryPageFreeList(long capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        Capacity = capacity;
        _free.Add(new Interval(0, capacity));
    }

    /// <summary>Total size of the page.</summary>
    public long Capacity { get; }

    /// <summary>Bytes currently handed out.</summary>
    public long Used { get; private set; }

    /// <summary>Free bytes, including alignment padding that no request can use.</summary>
    public long FreeBytes
    {
        get
        {
            var total = 0L;
            foreach (var interval in _free)
            {
                total += interval.Length;
            }

            return total;
        }
    }

    /// <summary>Finds room for a request, first fit.</summary>
    /// <param name="length">Requested length.</param>
    /// <param name="alignment">Required alignment of the returned offset, or 1 for none.</param>
    /// <returns>The offset to use, or null when the page has no room for it.</returns>
    public long? Allocate(long length, long alignment = 1)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(alignment);

        for (var index = 0; index < _free.Count; index++)
        {
            var interval = _free[index];
            var end = interval.Offset + interval.Length;
            var start = AlignUp(interval.Offset, alignment);
            if (start + length > end)
            {
                continue;
            }

            var padding = start - interval.Offset;
            var tail = end - (start + length);

            // Replace the free interval with the padding in front and the tail behind; either may be empty.
            _free.RemoveAt(index);
            if (tail > 0)
            {
                _free.Insert(index, new Interval(start + length, tail));
            }

            if (padding > 0)
            {
                _free.Insert(index, new Interval(interval.Offset, padding));
            }

            Used += length;
            return start;
        }

        return null;
    }

    /// <summary>Returns a range to the page and merges it with adjacent free ranges.</summary>
    /// <param name="offset">Offset that was handed out.</param>
    /// <param name="length">Length that was handed out.</param>
    public void Release(long offset, long length)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);
        if (offset < 0 || offset + length > Capacity)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), "the range is outside the page");
        }

        var at = 0;
        while (at < _free.Count && _free[at].Offset < offset)
        {
            at++;
        }

        var start = offset;
        var end = offset + length;

        // Merge with the previous range when it ends where this one starts.
        if (at > 0 && _free[at - 1].Offset + _free[at - 1].Length == offset)
        {
            at--;
            start = _free[at].Offset;
            end = Math.Max(end, _free[at].Offset + _free[at].Length);
            _free.RemoveAt(at);
        }

        // Merge with every following range that now touches.
        while (at < _free.Count && _free[at].Offset <= end)
        {
            end = Math.Max(end, _free[at].Offset + _free[at].Length);
            _free.RemoveAt(at);
        }

        _free.Insert(at, new Interval(start, end - start));
        Used -= length;
    }

    /// <summary>Rounds an offset up to an alignment.</summary>
    /// <param name="value">Offset to align.</param>
    /// <param name="alignment">Alignment, a positive power of two or any positive number.</param>
    /// <returns>The aligned offset.</returns>
    private static long AlignUp(long value, long alignment)
    {
        var remainder = value % alignment;
        return remainder == 0 ? value : value + (alignment - remainder);
    }
}
