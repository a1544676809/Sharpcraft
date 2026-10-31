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

        // 6 faces of a 16x16 shell; internal water/water faces are culled.
        Assert.Equal(6 * 16 * 16, translucent.IndexCount / ChunkMesher.IndicesPerQuad);
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
}
