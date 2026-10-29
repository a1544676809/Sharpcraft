// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.World;
using Sharpcraft.Common.Entities;
using Xunit;

namespace Sharpcraft.Client.Tests.World;

/// <summary>
/// The world adapter decides what stops an entity. Unknown terrain reads as air, matching vanilla, whose
/// block lookup falls back to void air for a missing chunk: vanilla keeps the player inside loaded terrain by
/// streaming chunks, not by turning the edge of what has arrived into a wall
/// (docs/specs/network/play-movement.md 7.12).
/// </summary>
public sealed class EntityWorldAdapterTests
{
    [Fact]
    public void TerrainOutsideTheDownloadedChunksReadsAsAir()
    {
        var world = new ClientWorld(-4, 24);
        world.AddChunk(0, 0);

        var adapter = new EntityWorldAdapter(world);

        Assert.False(world.IsChunkLoaded(1000, 0));
        Assert.False(adapter.IsSolid(1000, 70, 0));
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
    public void AChunkArrivingWhileWalkingMakesItsGroundSolid()
    {
        var world = new ClientWorld(-4, 24);
        var adapter = new EntityWorldAdapter(world);
        var player = new PlayerEntity(adapter, new Vector3(10.5f, 64f, 0.5f));

        // Nothing is downloaded yet, so the player falls through the unknown column.
        player.Move(new Vector3(0f, -1f, 0f));
        Assert.Equal(63f, player.Position.Y, 3);

        // The server streams the chunk in; its floor now exists and stops the fall.
        var chunk = world.AddChunk(0, 0);
        chunk.SetSection(new Sharpcraft.Common.World.ChunkSection(singleState: 0, sectionY: 3));
        for (var x = 0; x < 16; x++)
        {
            for (var z = 0; z < 16; z++)
            {
                chunk.SetBlockState(x, 63, z, state: 1);
            }
        }

        // A fresh player standing just above the freshly streamed floor lands on it.
        var afterStreaming = new PlayerEntity(adapter, new Vector3(10.5f, 64.5f, 0.5f));
        afterStreaming.Move(new Vector3(0f, -1f, 0f));
        Assert.Equal(64f, afterStreaming.Position.Y, 3);
    }
}
