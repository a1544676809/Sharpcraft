// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Game;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Registries;
using Sharpcraft.Common.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// The frame request must never mesh the world itself (T-793).
/// </summary>
/// <remarks>
/// The windowed startup used to build its first request by meshing every chunk a second time, without the per pass
/// <see cref="StateCullTable"/> and <see cref="StateVisualCache"/> the cache builds, which cost forty five seconds of the
/// thirty second startup budget (docs/specs/performance-baseline.md 11). The cache is the only mesher: this test pins its
/// counter as well as the returned geometry, so a future change that meshes inside the request builder fails here instead
/// of quietly adding half a minute to every launch.
/// </remarks>
public sealed class FrameRequestDoesNotMeshTests
{
    /// <summary>State 1 is stone, state 2 is water, per the layout below.</summary>
    private static BlockStateLayout Layout() => new([
        (new Identifier("minecraft", "air"), 1),
        (new Identifier("minecraft", "stone"), 1),
        (new Identifier("minecraft", "water"), 16)
    ]);

    private static ClientWorld World(out LevelChunk chunk, BlockStateLayout layout)
    {
        var world = new ClientWorld(minSectionY: -1, sectionCount: 2);
        chunk = world.AddChunk(0, 0);
        chunk.SetSection(new ChunkSection(singleState: 1, sectionY: -1));
        chunk.SetSection(new ChunkSection(singleState: 2, sectionY: 0));
        chunk.MarkLoaded();
        return world;
    }

    [Fact]
    public void BuildingARequestFromACacheMeshesNothing()
    {
        var layout = Layout();
        var world = World(out _, layout);
        var cache = new ChunkMeshCache();
        using var cutoutSink = new MeshBuffer(64);
        using var translucentSink = new MeshBuffer(64);
        cache.Layers = new LayerSinks(layout, cutoutSink, translucentSink, Tint: null);

        cache.Build(world, sprites: null, maxBlocks: 2_000_000, maxChunksPerBuild: 8, maxMillisecondsPerBuild: 5_000d);
        var meshedBefore = cache.TotalChunksMeshed;
        Assert.True(meshedBefore > 0, "the warm-up build should have meshed the chunk");

        var request = FrameCommand.BuildRequest(
            world,
            width: 64,
            height: 64,
            out var mesh,
            out var drawRanges,
            sprites: null,
            atlas: null,
            cache: cache,
            meshSynchronously: false,
            layout: layout);

        // The counter is the guard: it only moves when ChunkMesher runs through the cache.
        Assert.Equal(meshedBefore, cache.TotalChunksMeshed);

        // The windowed path takes its per chunk ranges from the arenas, not from one merged list.
        Assert.Null(drawRanges);

        // The solid geometry is the cache's own combine, so the counts match the snapshot exactly.
        var snapshot = cache.Snapshot();
        Assert.Equal(snapshot.Vertices.Length, request.Vertices.Length);
        Assert.Equal(snapshot.Indices.Length, request.Indices.Length);
        Assert.Equal(mesh.Indices.Length, request.Indices.Length);
        Assert.NotEmpty(request.Indices);

        // The blended layer comes from the sink the cache wrote into, not from a second meshing pass.
        Assert.NotNull(request.TranslucentVertices);
        Assert.NotNull(request.TranslucentIndices);
        Assert.Equal(translucentSink.ToMeshData(0).Indices.Length, request.TranslucentIndices!.Length);

        // Cutout faces stay inside the solid mesh by design, so the request carries no separate cutout layer.
        Assert.Null(request.CutoutVertices);
    }
}
