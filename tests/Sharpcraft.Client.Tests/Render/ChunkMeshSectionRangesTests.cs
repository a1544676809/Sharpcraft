// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Registries;
using Sharpcraft.Common.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Per section index ranges (T-829): a chunk column's geometry is recorded as one contiguous run of indices per
/// section, so the renderer can cull sixteen cube sections instead of the whole column.
/// </summary>
public sealed class ChunkMeshSectionRangesTests
{
    private static BlockStateLayout Layout() => new([
        (new Identifier("minecraft", "air"), 1),
        (new Identifier("minecraft", "stone"), 1)
    ]);

    private static ClientWorld World(int sectionCount, out LevelChunk chunk, params int[] filledSections)
    {
        var world = new ClientWorld(minSectionY: -1, sectionCount: sectionCount);
        chunk = world.AddChunk(0, 0);

        foreach (var sectionY in filledSections)
        {
            chunk.SetSection(new ChunkSection(singleState: 1, sectionY: sectionY));
        }

        chunk.MarkLoaded();
        return world;
    }

    private static int[] Ranges(int sectionCount, params int[] filledSections)
    {
        var world = World(sectionCount, out var chunk, filledSections);
        using var opaque = new MeshBuffer();

        ChunkMesher.MeshChunk(
            world,
            chunk,
            airState: 0,
            opaque,
            maxBlocks: 1_000_000,
            sprites: null,
            layout: Layout());

        var starts = opaque.ToMeshData(0).SectionStarts;
        Assert.NotNull(starts);
        return starts!;
    }

    [Fact]
    public void EverySectionContributesOneBoundary()
    {
        // One entry per section plus the closing end of the last range.
        Assert.Equal(3 + 1, Ranges(3, -1, 0, 1).Length);
    }

    [Fact]
    public void RangesSpanTheWholeIndexBufferWithoutGaps()
    {
        var starts = Ranges(3, -1, 0, 1);

        Assert.Equal(0, starts[0]);
        for (var i = 1; i < starts.Length; i++)
        {
            Assert.True(starts[i] >= starts[i - 1], $"range {i} moved backwards");
        }
    }

    [Fact]
    public void AnEmptySectionContributesAZeroLengthRun()
    {
        // Only the middle section is filled, so the first and last runs are empty.
        var starts = Ranges(3, 0);

        Assert.Equal(4, starts.Length);
        Assert.Equal(starts[0], starts[1]);
        Assert.True(starts[2] > starts[1]);
        Assert.Equal(starts[2], starts[3]);
    }

    [Fact]
    public void GeometryProducesANonEmptyRangeInTheFilledSection()
    {
        var starts = Ranges(3, 0);

        // The filled section is the middle one and holds every index the mesher emitted.
        Assert.True(starts[2] - starts[1] > 0);
        Assert.Equal(0, starts[0]);
    }
}
