// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 MinecraftCSharp contributors

using MinecraftCSharp.Client.World;
using MinecraftCSharp.Common.Network;
using MinecraftCSharp.Common.World;
using Xunit;

namespace MinecraftCSharp.Client.Tests;

/// <summary>
/// Client world storage: chunk lookup with negative coordinates, JoinGame application and
/// block state routing (docs/analysis/common/coordinates.md).
/// </summary>
public sealed class ClientWorldTests
{
    [Fact]
    public void JoinGameUpdatesIdentityAndViewDistance()
    {
        var world = new ClientWorld(minSectionY: -4, sectionCount: 24);

        world.ApplyJoin(new JoinGameInfo(
            PlayerId: 24617,
            Hardcore: false,
            Levels: ["minecraft:overworld", "minecraft:the_nether"],
            MaxPlayers: 20,
            ChunkRadius: 16,
            SimulationDistance: 10,
            ReducedDebugInfo: false,
            ShowDeathScreen: true,
            LimitedCrafting: false));

        Assert.Equal(24617, world.LocalEntityId);
        Assert.Equal(16, world.ViewDistance);
        Assert.Equal(["minecraft:overworld", "minecraft:the_nether"], world.Dimensions);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(-1, -1)]
    [InlineData(12345, -67890)]
    [InlineData(int.MaxValue, int.MinValue)]
    public void ChunkKeysRoundTrip(int chunkX, int chunkZ)
    {
        var key = ClientWorld.Pack(chunkX, chunkZ);

        Assert.Equal((chunkX, chunkZ), ClientWorld.Unpack(key));
    }

    [Fact]
    public void NegativeWorldCoordinatesMapToTheRightChunkAndSection()
    {
        var world = new ClientWorld(minSectionY: -4, sectionCount: 24);
        var chunk = world.AddChunk(-1, -1);
        chunk.SetSection(new ChunkSection(singleState: 1, sectionY: -4));

        // (-1, -64, -1) is chunk (-1,-1), local (15,15), section -4 at local y 0.
        Assert.Equal(1, world.GetBlockState(-1, -64, -1));
        Assert.Equal(1, world.GetBlockState(-16, -64, -16));

        // A neighbouring chunk that is not loaded falls back instead of throwing.
        Assert.Equal(0, world.GetBlockState(0, -64, 0));
    }

    [Fact]
    public void WritesReachLoadedChunksOnly()
    {
        var world = new ClientWorld(minSectionY: 0, sectionCount: 1);
        var chunk = world.AddChunk(0, 0);
        chunk.SetSection(new ChunkSection(singleState: 0, sectionY: 0));

        Assert.True(world.SetBlockState(3, 4, 5, state: 77));
        Assert.Equal(77, world.GetBlockState(3, 4, 5));

        Assert.False(world.SetBlockState(100, 4, 100, state: 77));
        Assert.Equal(0, world.GetBlockState(100, 4, 100));
    }

    [Fact]
    public void RemovingAChunkDropsItsState()
    {
        var world = new ClientWorld(minSectionY: 0, sectionCount: 1);
        world.AddChunk(2, 3);

        Assert.Equal(1, world.LoadedChunkCount);
        Assert.NotNull(world.GetChunk(2, 3));

        Assert.True(world.RemoveChunk(2, 3));
        Assert.False(world.RemoveChunk(2, 3));
        Assert.Equal(0, world.LoadedChunkCount);
        Assert.Null(world.GetChunk(2, 3));
    }

    [Fact]
    public void AddingTheSameChunkTwiceReplacesIt()
    {
        var world = new ClientWorld(minSectionY: 0, sectionCount: 1);

        var first = world.AddChunk(0, 0);
        var second = world.AddChunk(0, 0);

        Assert.NotSame(first, second);
        Assert.Same(second, world.GetChunk(0, 0));
        Assert.Equal(1, world.LoadedChunkCount);
    }
}
