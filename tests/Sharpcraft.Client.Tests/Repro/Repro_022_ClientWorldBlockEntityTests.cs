// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.World;
using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Registries;
using Sharpcraft.Common.World;

namespace Sharpcraft.Client.Tests.Repro;

/// <summary>
/// Issue 22: placing a block entity block through <see cref="ClientWorld.SetBlockState"/> has to
/// create its record, breaking it has to remove the record, and a change next to another block
/// entity must leave that block entity alone.
/// </summary>
[Collection(NonParallelCollection.Name)]
public sealed class Repro_022_ClientWorldBlockEntityTests
{
    private static ClientWorld NewWorld(out LevelChunk chunk)
    {
        var world = new ClientWorld(minSectionY: 0, sectionCount: 1);
        chunk = world.AddChunk(0, 0);
        chunk.SetSection(new ChunkSection(singleState: 0, sectionY: 0));
        return world;
    }

    private static int ChestState() =>
        VanillaBlockRegistry.Layout.GetBaseId(Identifier.Parse("minecraft:chest"));

    [Fact]
    public void PlacingAChestCreatesItsRecord()
    {
        var world = NewWorld(out var chunk);

        Assert.True(world.SetBlockState(3, 8, 5, ChestState()));

        var record = Assert.Single(chunk.BlockEntities);
        Assert.Equal(3, record.LocalX);
        Assert.Equal(8, record.Y);
        Assert.Equal(5, record.LocalZ);
        Assert.Equal(VanillaBlockEntityTypes.Chest, record.TypeId);
    }

    [Fact]
    public void BreakingAChestRemovesItsRecord()
    {
        var world = NewWorld(out var chunk);
        world.SetBlockState(3, 8, 5, ChestState());
        Assert.Single(chunk.BlockEntities);

        world.SetBlockState(3, 8, 5, state: 0);

        Assert.Empty(chunk.BlockEntities);
    }


    [Fact]
    public void BreakingAShulkerBoxRemovesItsRecord()
    {
        var world = NewWorld(out var chunk);
        var state = VanillaBlockRegistry.Layout.GetBaseId(Identifier.Parse("minecraft:shulker_box"));
        world.SetBlockState(3, 8, 5, state);
        Assert.Single(chunk.BlockEntities);

        world.SetBlockState(3, 8, 5, state: 0);

        Assert.Empty(chunk.BlockEntities);
    }

    [Fact]
    public void AChangeOnANonChestBlockEntityLeavesItsRecordAlone()
    {
        var world = NewWorld(out var chunk);

        // A furnace carries a block entity this client does not build geometry for yet; replace the
        // block at a neighbouring position and the record must survive.
        chunk.AddBlockEntity(new BlockEntityRecord(4, 8, 5, TypeId: 0));
        world.SetBlockState(3, 8, 5, ChestState());

        Assert.Contains(chunk.BlockEntities, r => r.LocalX == 4 && r.Y == 8 && r.LocalZ == 5);
    }

    [Fact]
    public void PlacingOverANonChestRecordDoesNotRemoveIt()
    {
        var world = NewWorld(out var chunk);
        chunk.AddBlockEntity(new BlockEntityRecord(3, 8, 5, TypeId: 0));

        world.SetBlockState(3, 8, 5, state: 0);

        Assert.Contains(chunk.BlockEntities, r => r.TypeId == 0);
    }

    [Theory]
    [InlineData("minecraft:oak_sign", 7)]
    [InlineData("minecraft:white_banner", 20)]
    [InlineData("minecraft:skeleton_skull", 16)]
    [InlineData("minecraft:decorated_pot", 41)]
    [InlineData("minecraft:furnace", 0)]
    public void PlacingAnyBlockEntityBlockCreatesItsRecord(string blockId, int expectedType)
    {
        var world = NewWorld(out var chunk);
        var state = VanillaBlockRegistry.Layout.GetBaseId(Identifier.Parse(blockId));

        Assert.True(world.SetBlockState(3, 8, 5, state));

        var record = Assert.Single(chunk.BlockEntities);
        Assert.Equal(expectedType, record.TypeId);
    }

    [Fact]
    public void APlainPistonCarriesNoBlockEntity()
    {
        var world = NewWorld(out var chunk);
        var state = VanillaBlockRegistry.Layout.GetBaseId(Identifier.Parse("minecraft:piston"));

        Assert.True(world.SetBlockState(3, 8, 5, state));

        Assert.Empty(chunk.BlockEntities);
    }
}
