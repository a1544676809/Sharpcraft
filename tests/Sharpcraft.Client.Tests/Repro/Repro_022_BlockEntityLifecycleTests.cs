// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.World;

namespace Sharpcraft.Client.Tests.Repro;

/// <summary>
/// Issues 22 and 24: a block entity record has to appear when a block entity block is placed and
/// disappear when it is broken, and the eight copper chest blocks have to resolve to the chest
/// type because the reference registers one type for all of them.
/// </summary>
public sealed class Repro_022_BlockEntityLifecycleTests
{
    [Theory]
    [InlineData("chest")]
    [InlineData("copper_chest")]
    [InlineData("exposed_copper_chest")]
    [InlineData("weathered_copper_chest")]
    [InlineData("oxidized_copper_chest")]
    [InlineData("waxed_copper_chest")]
    [InlineData("waxed_exposed_copper_chest")]
    [InlineData("waxed_weathered_copper_chest")]
    [InlineData("waxed_oxidized_copper_chest")]
    public void ChestAndCopperChestBlocksResolveToTheChestType(string path)
    {
        var typeId = VanillaBlockEntityTypes.IdOf(path);

        Assert.Equal(VanillaBlockEntityTypes.Chest, typeId);
        Assert.True(VanillaBlockEntityTypes.IsChestLike(typeId));
    }

    [Fact]
    public void TrappedAndEnderChestsKeepTheirOwnTypes()
    {
        Assert.Equal(VanillaBlockEntityTypes.TrappedChest, VanillaBlockEntityTypes.IdOf("trapped_chest"));
        Assert.Equal(VanillaBlockEntityTypes.EnderChest, VanillaBlockEntityTypes.IdOf("ender_chest"));
    }

    [Fact]
    public void AddingAndRemovingARecordLeavesTheChunkEmpty()
    {
        var chunk = new LevelChunk(0, 0, minSectionY: 0, sectionCount: 1);
        Assert.Empty(chunk.BlockEntities);

        chunk.AddBlockEntity(new BlockEntityRecord(3, 64, 5, VanillaBlockEntityTypes.Chest));
        Assert.Single(chunk.BlockEntities);

        chunk.RemoveBlockEntity(3, 64, 5);
        Assert.Empty(chunk.BlockEntities);
    }

    [Fact]
    public void AddingTwiceReplacesInsteadOfDuplicating()
    {
        var chunk = new LevelChunk(0, 0, minSectionY: 0, sectionCount: 1);

        chunk.AddBlockEntity(new BlockEntityRecord(3, 64, 5, VanillaBlockEntityTypes.Chest));
        chunk.AddBlockEntity(new BlockEntityRecord(3, 64, 5, VanillaBlockEntityTypes.TrappedChest));

        Assert.Single(chunk.BlockEntities);
        Assert.Equal(VanillaBlockEntityTypes.TrappedChest, chunk.BlockEntities[0].TypeId);
    }

    [Fact]
    public void RemovingSomethingThatIsNotThereChangesNothing()
    {
        var chunk = new LevelChunk(0, 0, minSectionY: 0, sectionCount: 1);
        chunk.AddBlockEntity(new BlockEntityRecord(3, 64, 5, VanillaBlockEntityTypes.Chest));

        chunk.RemoveBlockEntity(4, 64, 5);

        Assert.Single(chunk.BlockEntities);
    }
}
