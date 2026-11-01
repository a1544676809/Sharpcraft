// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.World;
using Xunit;

namespace Sharpcraft.Common.Tests.World;

/// <summary>
/// Per-section quart biome ids (docs/analysis/common/biome-data.md 1): index = (y&lt;&lt;4)|(z&lt;&lt;2)|x, block→quart by &gt;&gt;2.
/// </summary>
public sealed class ChunkSectionBiomeTests
{
    [Fact]
    public void BiomeIndexMatchesQuartOrdering()
    {
        Assert.Equal(0, ChunkSection.BiomeIndex(0, 0, 0));
        Assert.Equal(1, ChunkSection.BiomeIndex(4, 0, 0));
        Assert.Equal(4, ChunkSection.BiomeIndex(0, 0, 4));
        Assert.Equal(16, ChunkSection.BiomeIndex(0, 4, 0));
        Assert.Equal(63, ChunkSection.BiomeIndex(15, 15, 15));
    }

    [Fact]
    public void ReadsBiomeIdsBySectionCoordinates()
    {
        var ids = new int[ChunkSection.BiomeVolume];
        for (var i = 0; i < ids.Length; i++)
        {
            ids[i] = 100 + i;
        }

        var section = new ChunkSection(new PalettedContainer(ChunkSection.Volume, 0), 0, 0, ids);

        Assert.True(section.HasBiomes);
        Assert.Equal(100, section.GetBiome(0, 0, 0));
        Assert.Equal(101, section.GetBiome(4, 0, 0));
        Assert.Equal(104, section.GetBiome(0, 0, 4));
        Assert.Equal(116, section.GetBiome(0, 4, 0));
        Assert.Equal(163, section.GetBiome(15, 15, 15));
    }

    [Fact]
    public void ASectionWithoutBiomesReadsZero()
    {
        var section = new ChunkSection(singleState: 0, sectionY: 0);

        Assert.False(section.HasBiomes);
        Assert.Equal(0, section.GetBiome(8, 8, 8));
    }
}
