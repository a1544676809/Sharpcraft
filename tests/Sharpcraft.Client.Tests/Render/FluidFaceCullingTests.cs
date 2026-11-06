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
/// A fluid's side face must only be hidden by a block that actually occludes light. The reference FluidRenderer keeps a
/// fluid face unless the neighbour is the same fluid, so a translucent neighbour such as glass must not drop it; otherwise
/// water behind glass disappears (the reported "cannot see water through glass" regression).
/// </summary>
/// <remarks>
/// The check counts translucent vertices lying on the shared plane x = 1 between a water block at the origin and its +X
/// neighbour. That plane isolates the one face the culling decision governs, so the neighbour's own geometry (which lands
/// on other planes) cannot change the number.
/// </remarks>
public sealed class FluidFaceCullingTests
{
    // air = state 0, glass = state 1, stone = state 1 (other layout), water = states 2..17.
    private static BlockStateLayout GlassLayout() => new([
        (new Identifier("minecraft", "air"), 1),
        (new Identifier("minecraft", "glass"), 1),
        (new Identifier("minecraft", "water"), 16)
    ]);

    private static BlockStateLayout StoneLayout() => new([
        (new Identifier("minecraft", "air"), 1),
        (new Identifier("minecraft", "stone"), 1),
        (new Identifier("minecraft", "water"), 16)
    ]);

    private const int GlassState = 1;
    private const int StoneState = 1;
    private const int WaterState = 2;

    private static ClientWorld Scene(BlockStateLayout layout, int centreState, int neighbourState)
    {
        var world = new ClientWorld(minSectionY: 0, sectionCount: 1);
        var chunk = world.AddChunk(0, 0);
        chunk.SetSection(new ChunkSection(singleState: 0, sectionY: 0));
        chunk.SetBlockState(0, 0, 8, centreState);
        chunk.SetBlockState(1, 0, 8, neighbourState);
        chunk.ApplyLight(LightData.FullSky(chunk.Light.LightSectionCount));
        chunk.MarkLoaded();
        return world;
    }

    /// <summary>Translucent triangles that lie wholly in the plane x = 1 (the shared water/neighbour face).</summary>
    private static int SharedPlaneTriangles(ClientWorld world, BlockStateLayout layout)
    {
        var chunk = world.GetChunks().Single();
        using var opaque = new MeshBuffer();
        using var translucent = new MeshBuffer();
        ChunkMesher.MeshChunk(world, chunk, airState: 0, opaque, maxBlocks: 1_000_000, sprites: null, translucent, layout);

        var vertices = translucent.Vertices;
        var indices = translucent.Indices;
        var triangles = 0;
        for (var index = 0; index + 2 < indices.Length; index += 3)
        {
            var a = (int)indices[index] * ChunkMesher.FloatsPerVertex;
            var b = (int)indices[index + 1] * ChunkMesher.FloatsPerVertex;
            var c = (int)indices[index + 2] * ChunkMesher.FloatsPerVertex;
            if (MathF.Abs(vertices[a] - 1f) < 1e-3f &&
                MathF.Abs(vertices[b] - 1f) < 1e-3f &&
                MathF.Abs(vertices[c] - 1f) < 1e-3f)
            {
                triangles++;
            }
        }

        return triangles;
    }

    [Fact]
    public void AGlassNeighbourDoesNotHideAWaterFace()
    {
        var layout = GlassLayout();

        // A single water cube beside air draws its +X face: two triangles in the plane x = 1.
        Assert.Equal(2, SharedPlaneTriangles(Scene(layout, WaterState, 0), layout));

        // Beside glass the same face must still be drawn.
        Assert.Equal(2, SharedPlaneTriangles(Scene(layout, WaterState, GlassState), layout));
    }

    [Fact]
    public void AStoneNeighbourHidesAWaterFace()
    {
        var layout = StoneLayout();

        // A genuinely opaque neighbour still removes the shared face: no translucent triangles in the plane x = 1.
        Assert.Equal(0, SharedPlaneTriangles(Scene(layout, WaterState, StoneState), layout));
    }
}
