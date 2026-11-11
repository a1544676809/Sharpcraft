// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Framework.Tests.Rendering;

/// <summary>
/// The page allocator the shared geometry arena is built on (ADR-0029): first fit within a page, coalescing release, and
/// alignment padding that never overlaps.
/// </summary>
public sealed class GeometryPageFreeListTests
{
    [Fact]
    public void AllocatesFromTheStartAndCountsUsedBytes()
    {
        var free = new GeometryPageFreeList(1024);

        Assert.Equal(0, free.Allocate(256));
        Assert.Equal(256, free.Used);
        Assert.Equal(768, free.FreeBytes);

        Assert.Equal(256, free.Allocate(256));
        Assert.Equal(512, free.Used);
    }

    [Fact]
    public void FailsWhenNoSingleIntervalIsLargeEnough()
    {
        var free = new GeometryPageFreeList(1024);
        Assert.Equal(0, free.Allocate(600));
        Assert.Equal(600, free.Allocate(400));

        // 24 bytes are left, which is less than the request, so the allocation must fail rather than overlap.
        Assert.Null(free.Allocate(100));
        Assert.Equal(1000, free.Used);
    }

    [Fact]
    public void AlignsTheStartAndLeavesThePaddingUnusable()
    {
        var free = new GeometryPageFreeList(1024);
        Assert.Equal(0, free.Allocate(10));
        var aligned = free.Allocate(10, alignment: 56);

        Assert.NotNull(aligned);
        Assert.Equal(0, aligned!.Value % 56);
        Assert.Equal(56, aligned.Value);

        // The 46 bytes of padding in front of the aligned range stay free, and a later first fit reuses them.
        Assert.Equal(10, free.Allocate(40));
    }

    [Fact]
    public void ReusesAFreedRangeFirst()
    {
        var free = new GeometryPageFreeList(1024);
        var first = free.Allocate(100);
        Assert.Equal(0, first);
        Assert.Equal(100, free.Allocate(100));

        free.Release(first!.Value, 100);
        Assert.Equal(100, free.Used);

        // First fit finds the released hole again instead of taking the tail.
        Assert.Equal(0, free.Allocate(100));
        Assert.Equal(200, free.Used);
    }

    [Fact]
    public void MergesWithBothNeighbours()
    {
        var free = new GeometryPageFreeList(300);
        var first = free.Allocate(100)!.Value;
        var middle = free.Allocate(100)!.Value;
        var last = free.Allocate(100)!.Value;

        free.Release(middle, 100);
        Assert.Equal(200, free.Used);

        // Freeing the neighbours must merge into one 300 byte interval, which only a merge can satisfy.
        free.Release(first, 100);
        free.Release(last, 100);
        Assert.Equal(0, free.Used);
        Assert.Equal(0, free.Allocate(300));
        Assert.Equal(300, free.Used);
        Assert.Equal(0, free.FreeBytes);
    }

    [Fact]
    public void RejectsRangesOutsideThePage()
    {
        var free = new GeometryPageFreeList(64);
        Assert.Throws<ArgumentOutOfRangeException>(() => free.Release(32, 64));
        Assert.Throws<ArgumentOutOfRangeException>(() => free.Release(-8, 8));
    }
}
