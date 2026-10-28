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
        Assert.Equal(6, mesh.QuadCount);
        Assert.Equal(6f, mesh.SurfaceArea, 3);
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

        // Two blocks, ten unit faces: the two touching faces are hidden, and greedy meshing merges
        // the remaining coplanar pairs into four double-size quads plus two single ones.
        Assert.Equal(2, mesh.BlockCount);
        Assert.Equal(6, mesh.QuadCount);
        Assert.Equal(10f, mesh.SurfaceArea, 3);
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
        Assert.Equal(10f, mesh.SurfaceArea, 3);
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
    public void BlockCapStopsMeshingBeforeTheNextSection()
    {
        var world = new ClientWorld(minSectionY: 0, sectionCount: 2);
        var chunk = world.AddChunk(0, 0);
        var lower = new ChunkSection(singleState: 0, sectionY: 0);
        var upper = new ChunkSection(singleState: 0, sectionY: 1);
        chunk.SetSection(lower);
        chunk.SetSection(upper);
        for (var i = 0; i < 8; i++)
        {
            lower.Set(i, 0, 0, 1);
            upper.Set(i, 0, 0, 1);
        }

        // The cap is section-granular: a merged quad can span many blocks, so the mesher stops
        // before a section rather than mid-quad.
        var capped = ChunkMesher.Mesh(world, maxBlocks: 4);
        var full = ChunkMesher.Mesh(world);

        Assert.Equal(8, capped.BlockCount);
        Assert.True(capped.SurfaceArea < full.SurfaceArea, "the cap must drop the second section");
    }

    [Fact]
    public void VertexLayoutInterleavesPositionAndShadedColour()
    {
        var world = SingleBlockWorld(2);

        var mesh = ChunkMesher.Mesh(world);

        Assert.Equal(0, mesh.Vertices.Length % ChunkMesher.FloatsPerVertex);

        // Colour is the state colour scaled by the light and facing shade, so the hue must survive
        // while the magnitude may differ from the raw state colour.
        var (red, green, blue) = ChunkMesher.ColourOf(2);
        var scale = mesh.Vertices[3] / red;

        Assert.InRange(scale, 0f, 1f);
        Assert.Equal(green * scale, mesh.Vertices[4], 4);
        Assert.Equal(blue * scale, mesh.Vertices[5], 4);
    }

    [Fact]
    public void FacesAreShadedByFacingSoTopIsBrightest()
    {
        var world = SingleBlockWorld(2);

        var mesh = ChunkMesher.Mesh(world);

        // Quad order is -X, +X, -Y, +Y, -Z, +Z; the first vertex of each quad carries its colour.
        var topColour = mesh.Vertices[(3 * ChunkMesher.VerticesPerQuad * ChunkMesher.FloatsPerVertex) + 3];
        var bottomColour = mesh.Vertices[(2 * ChunkMesher.VerticesPerQuad * ChunkMesher.FloatsPerVertex) + 3];
        var sideColour = mesh.Vertices[3];

        Assert.True(topColour > sideColour, $"top {topColour} should be brighter than side {sideColour}");
        Assert.True(sideColour > bottomColour, $"side {sideColour} should be brighter than bottom {bottomColour}");
    }

    [Fact]
    public void UnlitBlocksAreDarkerThanLitBlocks()
    {
        var dark = SingleBlockWorld(2);
        var lit = SingleBlockWorld(2);
        lit.GetChunk(0, 0)!.Light.FillSkyLight(15);

        var darkColour = ChunkMesher.Mesh(dark).Vertices[3];
        var litColour = ChunkMesher.Mesh(lit).Vertices[3];

        Assert.True(litColour > darkColour, $"lit {litColour} should be brighter than dark {darkColour}");
        Assert.True(darkColour > 0f, "unlit geometry must not go pure black");
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
