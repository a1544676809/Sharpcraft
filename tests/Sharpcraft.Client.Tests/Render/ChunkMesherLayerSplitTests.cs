// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Registries;
using Sharpcraft.Common.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// The translucent split (docs/specs/render/pipeline.md 15, S2): water leaves through the second buffer while stone stays in
/// the first, and without a layout everything lands in the opaque buffer as before.
/// </summary>
public sealed class ChunkMesherLayerSplitTests
{
    private static BlockStateLayout Layout() => new([
        (new Identifier("minecraft", "stone"), 1),
        (new Identifier("minecraft", "water"), 1)
    ]);

    private static ClientWorld World(out LevelChunk chunk)
    {
        var world = new ClientWorld(minSectionY: -1, sectionCount: 2);
        chunk = world.AddChunk(0, 0);

        // State 1 is stone, state 2 is water, per the layout above.
        chunk.SetSection(new ChunkSection(singleState: 1, sectionY: -1));
        chunk.SetSection(new ChunkSection(singleState: 2, sectionY: 0));
        chunk.MarkLoaded();

        return world;
    }

    [Fact]
    public void WaterFacesLandInTheTranslucentBuffer()
    {
        var world = World(out var chunk);
        using var opaque = new MeshBuffer();
        using var translucent = new MeshBuffer();

        ChunkMesher.MeshChunk(world, chunk, airState: 0, opaque, maxBlocks: 1_000_000, sprites: null, translucent, Layout());

        Assert.True(opaque.IndexCount > 0);
        Assert.True(translucent.IndexCount > 0);
    }

    [Fact]
    public void WithoutALayoutEverythingStaysOpaque()
    {
        var world = World(out var chunk);
        using var opaque = new MeshBuffer();
        using var translucent = new MeshBuffer();

        ChunkMesher.MeshChunk(world, chunk, airState: 0, opaque, maxBlocks: 1_000_000, sprites: null, translucent);

        Assert.True(opaque.IndexCount > 0);
        Assert.Equal(0, translucent.IndexCount);
    }

    [Fact]
    public void SplittingDoesNotChangeTheTotalFaceCount()
    {
        var world = World(out var chunk);
        using var whole = new MeshBuffer();
        ChunkMesher.MeshChunk(world, chunk, airState: 0, whole, maxBlocks: 1_000_000, sprites: null);

        var world2 = World(out var chunk2);
        using var opaque = new MeshBuffer();
        using var translucent = new MeshBuffer();
        ChunkMesher.MeshChunk(world2, chunk2, airState: 0, opaque, maxBlocks: 1_000_000, sprites: null, translucent, Layout());

        Assert.Equal(whole.IndexCount, opaque.IndexCount + translucent.IndexCount);
    }
}
