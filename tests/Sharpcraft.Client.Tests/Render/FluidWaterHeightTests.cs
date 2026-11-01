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
/// Water surface height in the mesher (docs/specs/render/fluid-rendering.md 2): a source block tops out at 8/9 and a
/// flowing block is lower, instead of the old fixed 14/16.
/// </summary>
public sealed class FluidWaterHeightTests
{
    private static BlockStateLayout Layout() => new(
    [
        (new Identifier("minecraft", "air"), 1),
        (new Identifier("minecraft", "water"), 16)
    ]);

    private static ClientWorld WaterWorld(int waterState, out LevelChunk chunk)
    {
        var world = new ClientWorld(minSectionY: 0, sectionCount: 1);
        chunk = world.AddChunk(0, 0);
        chunk.SetSection(new ChunkSection(singleState: 0, sectionY: 0));
        chunk.SetBlockState(8, 8, 8, waterState);
        chunk.ApplyLight(LightData.FullSky(chunk.Light.LightSectionCount));
        chunk.MarkLoaded();
        return world;
    }

    private static float TopHeight(int waterState)
    {
        var world = WaterWorld(waterState, out var chunk);
        var layout = Layout();
        using var buffer = new MeshBuffer();
        ChunkMesher.MeshChunk(world, chunk, airState: 0, buffer, maxBlocks: 1000, sprites: null, layout: layout);
        var mesh = buffer.ToMeshData(0);

        var top = float.MinValue;
        for (var i = 1; i < mesh.Vertices.Length; i += ChunkMesher.FloatsPerVertex)
        {
            top = MathF.Max(top, mesh.Vertices[i]);
        }

        return top;
    }

    [Fact]
    public void SourceWaterTopsOutAtEightNinths()
    {
        // Layout base id of water is 1, so state 1 is level 0 (source, amount 8 -> 8/9). Block at y=8 -> top 8 + 8/9.
        var top = TopHeight(waterState: 1);

        Assert.Equal(8f + (8f / 9f), top, 4);
    }

    [Fact]
    public void FlowingWaterIsLower()
    {
        // State 5 = level 4 (amount 4 -> 4/9), so the surface is 8 + 4/9.
        var top = TopHeight(waterState: 5);

        Assert.Equal(8f + (4f / 9f), top, 4);
    }
}
