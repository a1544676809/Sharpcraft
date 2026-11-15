// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.World;
using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Registries;
using Sharpcraft.Common.World;

namespace Sharpcraft.Client.Tests.Repro;

/// <summary>
/// Issue 24: a broken chest has to disappear from the next frame. The record removal is what makes
/// the geometry builder produce nothing for that position, so that is what this pins down.
/// </summary>
[Collection(NonParallelCollection.Name)]
public sealed class Repro_024_GhostChestTests
{
    [Fact]
    public void ABrokenChestLeavesNoRecordAndSoNoGeometry()
    {
        var world = new ClientWorld(minSectionY: 0, sectionCount: 1);
        var chunk = world.AddChunk(0, 0);
        chunk.SetSection(new ChunkSection(singleState: 0, sectionY: 0));
        var chest = VanillaBlockRegistry.Layout.GetBaseId(Identifier.Parse("minecraft:chest"));

        world.SetBlockState(3, 8, 5, chest);
        Assert.Single(chunk.BlockEntities);

        world.SetBlockState(3, 8, 5, state: 0);

        Assert.Empty(chunk.BlockEntities);
    }

    [Fact]
    public void BreakingOneChestLeavesTheOtherOneAlone()
    {
        var world = new ClientWorld(minSectionY: 0, sectionCount: 1);
        var chunk = world.AddChunk(0, 0);
        chunk.SetSection(new ChunkSection(singleState: 0, sectionY: 0));
        var chest = VanillaBlockRegistry.Layout.GetBaseId(Identifier.Parse("minecraft:chest"));

        world.SetBlockState(3, 8, 5, chest);
        world.SetBlockState(9, 8, 5, chest);
        Assert.Equal(2, chunk.BlockEntities.Count);

        world.SetBlockState(3, 8, 5, state: 0);

        var remaining = Assert.Single(chunk.BlockEntities);
        Assert.Equal(9, remaining.LocalX);
    }
}
