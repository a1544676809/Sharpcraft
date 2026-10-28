// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 MinecraftCSharp contributors

using MinecraftCSharp.Client.Render;
using MinecraftCSharp.Client.World;
using MinecraftCSharp.Common.World;
using Xunit;

namespace MinecraftCSharp.Client.Tests;

/// <summary>
/// Face culling and quad layout of the mesher (docs/modules/world.md). Geometry is verified by
/// counting faces and vertices, which is what a wrong winding or a missing cull would change.
/// </summary>
public sealed class ChunkMesherTests
{
    private static ClientWorld SingleBlockWorld(int state)
    {
        var world = new ClientWorld(minSectionY: 0, sectionCount: 1);
        var chunk = world.AddChunk(0, 0);
        chunk.SetSection(new ChunkSection(singleState: 0, sectionY: 0));
        chunk.SetBlockState(8, 8, 8, state);
        return world;
    }

    [Fact]
    public void IsolatedBlockProducesSixFaces()
    {
        var world = SingleBlockWorld(1);

        var mesh = ChunkMesher.Mesh(world);

        Assert.Equal(1, mesh.BlockCount);
        Assert.Equal(6 * ChunkMesher.VerticesPerQuad, mesh.VertexCount);
        Assert.Equal(6 * ChunkMesher.IndicesPerQuad, mesh.Indices.Length);
        Assert.Equal(12, mesh.TriangleCount);
    }

    [Fact]
    public void AdjacentBlocksCullTheSharedFaces()
    {
        var world = new ClientWorld(minSectionY: 0, sectionCount: 1);
        var chunk = world.AddChunk(0, 0);
        chunk.SetSection(new ChunkSection(singleState: 0, sectionY: 0));
        chunk.SetBlockState(8, 8, 8, state: 1);
        chunk.SetBlockState(9, 8, 8, state: 1);

        var mesh = ChunkMesher.Mesh(world);

        // Two blocks, ten faces: the two touching faces are hidden.
        Assert.Equal(2, mesh.BlockCount);
        Assert.Equal(10 * ChunkMesher.VerticesPerQuad, mesh.VertexCount);
    }

    [Fact]
    public void FacesAcrossAChunkBorderAreCulledUsingTheNeighbourChunk()
    {
        var world = new ClientWorld(minSectionY: 0, sectionCount: 1);
        var left = world.AddChunk(0, 0);
        left.SetSection(new ChunkSection(singleState: 0, sectionY: 0));
        left.SetBlockState(15, 8, 8, state: 1);

        var right = world.AddChunk(1, 0);
        right.SetSection(new ChunkSection(singleState: 0, sectionY: 0));
        right.SetBlockState(0, 8, 8, state: 1);

        var mesh = ChunkMesher.Mesh(world);

        // The faces at x=16 are shared and must be culled even though they live in different chunks.
        Assert.Equal(2, mesh.BlockCount);
        Assert.Equal(10 * ChunkMesher.VerticesPerQuad, mesh.VertexCount);
    }

    [Fact]
    public void AirOnlyWorldProducesNoGeometry()
    {
        var world = new ClientWorld(minSectionY: 0, sectionCount: 1);
        var chunk = world.AddChunk(0, 0);
        chunk.SetSection(new ChunkSection(singleState: 0, sectionY: 0));

        var mesh = ChunkMesher.Mesh(world);

        Assert.Equal(0, mesh.BlockCount);
        Assert.Equal(0, mesh.VertexCount);
        Assert.Empty(mesh.Indices);
    }

    [Fact]
    public void BlockCapStopsMeshing()
    {
        var world = new ClientWorld(minSectionY: 0, sectionCount: 1);
        var chunk = world.AddChunk(0, 0);
        chunk.SetSection(new ChunkSection(singleState: 0, sectionY: 0));
        for (var i = 0; i < 8; i++)
        {
            chunk.SetBlockState(i, 0, 0, state: 1);
        }

        var mesh = ChunkMesher.Mesh(world, maxBlocks: 3);

        Assert.Equal(3, mesh.BlockCount);
    }

    [Fact]
    public void VertexLayoutInterleavesPositionAndColour()
    {
        var world = SingleBlockWorld(2);

        var mesh = ChunkMesher.Mesh(world);

        Assert.Equal(0, mesh.Vertices.Length % ChunkMesher.FloatsPerVertex);
        var (red, green, blue) = ChunkMesher.ColourOf(2);
        Assert.Equal(red, mesh.Vertices[3]);
        Assert.Equal(green, mesh.Vertices[4]);
        Assert.Equal(blue, mesh.Vertices[5]);
    }

    [Fact]
    public void ColourIsStableForAStateAndDiffersBetweenStates()
    {
        var first = ChunkMesher.ColourOf(42);
        var again = ChunkMesher.ColourOf(42);
        var other = ChunkMesher.ColourOf(43);

        Assert.Equal(first, again);
        Assert.NotEqual(first, other);
    }
}
