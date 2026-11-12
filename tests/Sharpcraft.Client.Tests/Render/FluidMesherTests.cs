// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Network;
using Sharpcraft.Common.Registries;
using Sharpcraft.Common.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// The independent fluid pass (docs/specs/render/fluid-rendering.md): water leaves through the translucent buffer and an
/// adjacent same fluid hides the shared face, so a full 16^3 water section emits only its outer shell (6*16*16 quads),
/// not every internal face.
/// </summary>
public sealed class FluidMesherTests
{
    private static BlockStateLayout Layout() => new(
    [
        (new Identifier("minecraft", "air"), 1),
        (new Identifier("minecraft", "water"), 16)
    ]);

    private static ClientWorld WaterSection(out LevelChunk chunk)
    {
        var world = new ClientWorld(minSectionY: 0, sectionCount: 1);
        chunk = world.AddChunk(0, 0);
        chunk.SetSection(new ChunkSection(singleState: 1, sectionY: 0)); // state 1 = water level 0 (source)
        chunk.ApplyLight(LightData.FullSky(chunk.Light.LightSectionCount));
        chunk.MarkLoaded();
        return world;
    }

    [Fact]
    public void FullWaterSectionEmitsOnlyItsOuterShell()
    {
        var world = WaterSection(out var chunk);
        using var opaque = new MeshBuffer();
        using var translucent = new MeshBuffer();

        ChunkMesher.MeshChunk(world, chunk, airState: 0, opaque, maxBlocks: 1_000_000, sprites: null, translucent, Layout());

        // Bottom plus the four sides still emit one quad per cell. The top merges: its border ring cannot, because
        // the column heights next to open air differ there, so 60 cells stay solo quads while the 14x14 interior
        // collapses into one.
        const int facesOtherThanTop = 5 * 16 * 16;
        const int topBorderCells = (16 * 16) - (14 * 14);
        Assert.Equal(facesOtherThanTop + topBorderCells + 1, translucent.IndexCount / ChunkMesher.IndicesPerQuad);
        Assert.Equal(0, opaque.IndexCount);
    }

    [Fact]
    public void IsolatedWaterBlockEmitsSixFaces()
    {
        var world = new ClientWorld(minSectionY: 0, sectionCount: 1);
        var chunk = world.AddChunk(0, 0);
        chunk.SetSection(new ChunkSection(singleState: 0, sectionY: 0));
        chunk.SetBlockState(8, 8, 8, 1);
        chunk.ApplyLight(LightData.FullSky(chunk.Light.LightSectionCount));
        chunk.MarkLoaded();

        using var opaque = new MeshBuffer();
        using var translucent = new MeshBuffer();
        ChunkMesher.MeshChunk(world, chunk, airState: 0, opaque, maxBlocks: 1000, sprites: null, translucent, Layout());

        Assert.Equal(6, translucent.IndexCount / ChunkMesher.IndicesPerQuad);
    }

    [Fact]
    public void LavaStaysOnTheOpaqueBlockPath()
    {
        // Lava is opaque and must NOT be skipped by the fluid (water-only) pass: otherwise nothing draws it.
        var layout = new BlockStateLayout(
        [
            (new Identifier("minecraft", "air"), 1),
            (new Identifier("minecraft", "lava"), 16)
        ]);
        var world = new ClientWorld(minSectionY: 0, sectionCount: 1);
        var chunk = world.AddChunk(0, 0);
        chunk.SetSection(new ChunkSection(singleState: 1, sectionY: 0)); // state 1 = lava
        chunk.MarkLoaded();

        using var opaque = new MeshBuffer();
        using var translucent = new MeshBuffer();
        ChunkMesher.MeshChunk(world, chunk, airState: 0, opaque, maxBlocks: 1_000_000, sprites: null, translucent, layout);

        Assert.True(opaque.IndexCount > 0);
        Assert.Equal(0, translucent.IndexCount);
    }

    [Fact]
    public void FullLavaSectionEmitsADoubleSidedOuterShellIntoOpaque()
    {
        var layout = new BlockStateLayout(
        [
            (new Identifier("minecraft", "air"), 1),
            (new Identifier("minecraft", "lava"), 16)
        ]);
        var world = new ClientWorld(minSectionY: 0, sectionCount: 1);
        var chunk = world.AddChunk(0, 0);
        chunk.SetSection(new ChunkSection(singleState: 1, sectionY: 0)); // full lava section
        chunk.MarkLoaded();

        using var opaque = new MeshBuffer();
        using var translucent = new MeshBuffer();
        ChunkMesher.MeshChunk(world, chunk, airState: 0, opaque, maxBlocks: 1_000_000, sprites: null, translucent, layout);

        // Lava is opaque -> double-sided (reference addBackFace): 6*16*16 outer faces, both windings.
        Assert.Equal(2 * 6 * 16 * 16, opaque.IndexCount / ChunkMesher.IndicesPerQuad);
        Assert.Equal(0, translucent.IndexCount);
    }

    [Fact]
    public void WaterNextToTranslucentNeighbourKeepsThatFace()
    {
        // Reference FluidRenderer: a noOcclusion neighbour (glass) does not hide the adjacent fluid face, so a single
        // water block with glass on one side still shows all six faces (matrix row 11).
        var layout = new BlockStateLayout(
        [
            (new Identifier("minecraft", "air"), 1),
            (new Identifier("minecraft", "water"), 1),
            (new Identifier("minecraft", "glass"), 1)
        ]);

        Assert.Equal(6, WaterFaceCountWithSideNeighbour(layout, sideState: 2));
    }

    [Fact]
    public void WaterNextToOpaqueNeighbourCullsThatFace()
    {
        // A solid opaque neighbour (stone) does hide the shared face, so only five water faces remain. This is the
        // control for the translucent case above: same geometry, one neighbour that occludes.
        var layout = new BlockStateLayout(
        [
            (new Identifier("minecraft", "air"), 1),
            (new Identifier("minecraft", "water"), 1),
            (new Identifier("minecraft", "stone"), 1)
        ]);

        Assert.Equal(5, WaterFaceCountWithSideNeighbour(layout, sideState: 2));
    }

    private static int WaterFaceCountWithSideNeighbour(BlockStateLayout layout, int sideState)
    {
        // The neighbour lives in the EAST chunk, which is deliberately NOT meshed. Neighbourhood resolves it across the
        // border for the face-occlusion query, but because that chunk is never meshed the neighbour's own translucent
        // geometry never enters the buffer. This isolates the water's face count as the only observed quantity.
        var world = new ClientWorld(minSectionY: 0, sectionCount: 1);
        var centre = world.AddChunk(0, 0);
        centre.SetSection(new ChunkSection(singleState: 0, sectionY: 0)); // all air
        centre.SetBlockState(15, 8, 8, 1);                                 // water at the east edge (world x = 15)
        centre.ApplyLight(LightData.FullSky(centre.Light.LightSectionCount));
        centre.MarkLoaded();

        var east = world.AddChunk(1, 0);
        east.SetSection(new ChunkSection(singleState: 0, sectionY: 0));
        east.SetBlockState(0, 8, 8, sideState);                            // neighbour at world x = 16
        east.MarkLoaded();

        using var opaque = new MeshBuffer();
        using var translucent = new MeshBuffer();
        ChunkMesher.MeshChunk(world, centre, airState: 0, opaque, maxBlocks: 1000, sprites: null, translucent, layout);
        return translucent.IndexCount / ChunkMesher.IndicesPerQuad;
    }
}
