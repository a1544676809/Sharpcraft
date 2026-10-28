// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.World;
using Xunit;

namespace Sharpcraft.Common.Tests;

/// <summary>
/// World storage layer: packed paletted containers, sections and chunks
/// (docs/analysis/common/registry-order.md 5). These structures are what the chunk packet
/// parser will fill next, so their layout is pinned here.
/// </summary>
public sealed class WorldStorageTests
{
    [Fact]
    public void SingleValueContainerReportsOneValueEverywhere()
    {
        var container = new PalettedContainer(4096, 7);

        Assert.True(container.IsSingleValue);
        Assert.Equal(0, container.BitsPerEntry);
        Assert.Equal(7, container.Get(0));
        Assert.Equal(7, container.Get(4095));
    }

    [Fact]
    public void WritingANewValueGrowsThePaletteAndPreservesOldContent()
    {
        var container = new PalettedContainer(16, 1);

        container.Set(5, 2);

        Assert.False(container.IsSingleValue);
        Assert.Equal(2, container.Get(5));
        Assert.Equal(1, container.Get(4));
        Assert.Equal(1, container.Get(6));
        Assert.Contains(1, container.Palette);
        Assert.Contains(2, container.Palette);
    }

    [Fact]
    public void PaletteIndicesAreAssignedInFirstSeenOrder()
    {
        var container = new PalettedContainer(8, 0);

        container.Set(0, 100);
        container.Set(1, 200);
        container.Set(2, 300);

        Assert.Equal([0, 100, 200, 300], container.Palette);
        Assert.Equal(100, container.Get(0));
        Assert.Equal(200, container.Get(1));
        Assert.Equal(300, container.Get(2));
    }

    [Fact]
    public void PackedEntriesNeverCrossALongBoundary()
    {
        // 5 bits per entry allows 12 entries per long; entry 12 must start a new long.
        const int bits = 5;
        const int size = 40;
        var expected = PalettedContainer.ExpectedLongs(size, bits);
        Assert.Equal(4, expected);

        var container = new PalettedContainer(size, 0);
        for (var i = 1; i < 20; i++)
        {
            container.Set(i, i);
        }

        Assert.Equal(expected, container.Data.Count);
        for (var i = 0; i < size; i++)
        {
            Assert.Equal(i < 20 ? i : 0, container.Get(i));
        }
    }

    [Fact]
    public void ContainerRejectsMismatchedBackingData()
    {
        Assert.Throws<ArgumentException>(() => new PalettedContainer(4096, 4, [0], [0L]));
    }

    [Fact]
    public void ParsedContainerReadsPackedValues()
    {
        // Two entries per long at 32 bits; the low half holds entry 0.
        var data = new long[] { (1L << 32) | 0L };
        var container = new PalettedContainer(2, 32, [10, 20], data);

        Assert.Equal(10, container.Get(0));
        Assert.Equal(20, container.Get(1));
    }

    [Fact]
    public void SectionIndexOrderMatchesVanilla()
    {
        Assert.Equal(0, ChunkSection.Index(0, 0, 0));
        Assert.Equal(1, ChunkSection.Index(1, 0, 0));
        Assert.Equal(16, ChunkSection.Index(0, 0, 1));
        Assert.Equal(256, ChunkSection.Index(0, 1, 0));
        Assert.Equal(4095, ChunkSection.Index(15, 15, 15));
    }

    [Fact]
    public void SectionTracksNonAirCount()
    {
        var section = new ChunkSection(singleState: 0, sectionY: 0);

        Assert.True(section.IsEmpty);

        section.Set(0, 0, 0, state: 42, airState: 0);
        section.Set(1, 0, 0, state: 43, airState: 0);
        Assert.Equal(2, section.NonAirCount);

        section.Set(1, 0, 0, state: 0, airState: 0);
        Assert.Equal(1, section.NonAirCount);

        // Writing the same state twice must not double count.
        section.Set(0, 0, 0, state: 42, airState: 0);
        Assert.Equal(1, section.NonAirCount);
    }

    [Fact]
    public void ChunkMapsWorldYToSections()
    {
        var chunk = new LevelChunk(chunkX: 3, chunkZ: -2, minSectionY: -4, sectionCount: 24);

        Assert.Equal(-4, chunk.MinSectionY);
        Assert.Equal(19, chunk.MaxSectionY);
        Assert.Null(chunk.GetSection(-5));
        Assert.Null(chunk.GetSection(20));

        // y = -64 lands in section -4 at local y 0; y = -1 lands in section -1 at local y 15.
        var bottom = new ChunkSection(singleState: 1, sectionY: -4);
        var top = new ChunkSection(singleState: 2, sectionY: -1);
        chunk.SetSection(bottom);
        chunk.SetSection(top);

        Assert.Equal(1, chunk.GetBlockState(0, -64, 0));
        Assert.Equal(2, chunk.GetBlockState(0, -1, 0));

        // Uninstalled sections fall back instead of throwing.
        Assert.Equal(0, chunk.GetBlockState(0, 0, 0));
    }

    [Fact]
    public void ChunkRejectsSectionsOutsideItsRange()
    {
        var chunk = new LevelChunk(0, 0, minSectionY: -4, sectionCount: 24);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => chunk.SetSection(new ChunkSection(0, sectionY: 25)));
    }

    [Fact]
    public void ChunkWritesReachTheRightSection()
    {
        var chunk = new LevelChunk(0, 0, minSectionY: 0, sectionCount: 1);
        chunk.SetSection(new ChunkSection(singleState: 0, sectionY: 0));

        chunk.SetBlockState(15, 15, 15, state: 99);

        Assert.Equal(99, chunk.GetBlockState(15, 15, 15));
        Assert.Equal(99, chunk.GetSection(0)!.Get(15, 15, 15));
        Assert.Equal(1, chunk.GetSection(0)!.NonAirCount);
    }

    [Fact]
    public void ChunkRejectsOutOfRangeHorizontalCoordinates()
    {
        var chunk = new LevelChunk(0, 0, minSectionY: 0, sectionCount: 1);

        Assert.Throws<ArgumentOutOfRangeException>(() => chunk.GetBlockState(16, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => chunk.GetBlockState(0, 0, -1));
    }
}
