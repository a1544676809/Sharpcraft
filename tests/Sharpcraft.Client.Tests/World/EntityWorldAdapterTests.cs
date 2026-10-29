// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.World;
using Sharpcraft.Common.Entities;
using Xunit;

namespace Sharpcraft.Client.Tests.World;

/// <summary>
/// The world adapter decides what stops an entity. Unknown terrain must block movement: reading a missing
/// chunk as air lets the player walk into space the server has blocks in, and the server then reports a
/// position overlapping its own terrain (docs/specs/network/play-movement.md 7.10).
/// </summary>
public sealed class EntityWorldAdapterTests
{
    [Fact]
    public void TerrainOutsideTheDownloadedChunksBlocksMovement()
    {
        var world = new ClientWorld(-4, 24);
        world.AddChunk(0, 0);

        var adapter = new EntityWorldAdapter(world);

        // Chunk (0,0) is loaded and empty here, so it is walkable air.
        Assert.False(adapter.IsSolid(0, 70, 0));

        // A thousand blocks away nothing is downloaded; that must not read as air.
        Assert.True(adapter.IsSolid(1000, 70, 0));
    }

    [Fact]
    public void SolidBlocksInALoadedChunkAreSolid()
    {
        var world = new ClientWorld(-4, 24);
        var chunk = world.AddChunk(0, 0);
        chunk.SetSection(new Sharpcraft.Common.World.ChunkSection(singleState: 0, sectionY: 4));
        chunk.SetBlockState(0, 64, 0, state: 5);

        var adapter = new EntityWorldAdapter(world);

        Assert.True(adapter.IsSolid(0, 64, 0));
        Assert.False(adapter.IsSolid(0, 65, 0));
    }

    [Fact]
    public void AnEntityCannotWalkIntoUndownloadedTerrain()
    {
        var world = new ClientWorld(-4, 24);
        var chunk = world.AddChunk(0, 0);

        // A floor across the loaded chunk so the player has something to stand on.
        chunk.SetSection(new Sharpcraft.Common.World.ChunkSection(singleState: 0, sectionY: 3));
        for (var x = 0; x < 16; x++)
        {
            for (var z = 0; z < 16; z++)
            {
                chunk.SetBlockState(x, 63, z, state: 1);
            }
        }

        var player = new PlayerEntity(new EntityWorldAdapter(world), new Vector3(0.5f, 64f, 0.5f));

        for (var tick = 0; tick < 200; tick++)
        {
            player.Move(new Vector3(0.3f, 0f, 0f));
        }

        // Chunk 0 spans x 0..15, so the wall of unknown terrain must stop the player before x = 16.
        Assert.True(player.Position.X < 16f, $"walked to x={player.Position.X}, past the loaded chunk");
    }
}
