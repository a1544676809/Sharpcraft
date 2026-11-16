// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Common.World;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Per-renderer block entity cull grouping: renderer declarations, solid/no-cull index ranges, and
/// the vertex rebase across the single concatenated batch.
/// </summary>
public sealed class BlockEntityRenderTypesTests
{
    [Theory]
    [InlineData(VanillaBlockEntityTypes.Bell, BlockEntityRenderMode.Solid)]
    [InlineData(VanillaBlockEntityTypes.DecoratedPot, BlockEntityRenderMode.Solid)]
    [InlineData(VanillaBlockEntityTypes.CopperGolemStatue, BlockEntityRenderMode.Solid)]
    [InlineData(VanillaBlockEntityTypes.Chest, BlockEntityRenderMode.NoCull)]
    [InlineData(VanillaBlockEntityTypes.ShulkerBox, BlockEntityRenderMode.NoCull)]
    [InlineData(VanillaBlockEntityTypes.Shelf, BlockEntityRenderMode.NoCull)]
    [InlineData(VanillaBlockEntityTypes.Campfire, BlockEntityRenderMode.NoCull)]
    [InlineData(VanillaBlockEntityTypes.Lectern, BlockEntityRenderMode.NoCull)]
    [InlineData(VanillaBlockEntityTypes.EnchantingTable, BlockEntityRenderMode.NoCull)]
    [InlineData(VanillaBlockEntityTypes.Skull, BlockEntityRenderMode.NoCull)]
    public void RendererDeclaresTheReferenceRenderMode(int typeId, BlockEntityRenderMode expected)
    {
        var renderer = BlockEntityRenderers.Create().First(renderer => renderer.Handles(typeId));
        Assert.Equal(expected, renderer.RenderMode);
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0, 0, 0)]
    [InlineData(6, 0, 0, 0, 0, 0, 6, 0)]
    [InlineData(6, 6, 0, 6, 0, 6, 0, 0)]
    [InlineData(6, 2, 0, 2, 0, 2, 4, 0)]
    [InlineData(9, 6, 0, 6, 0, 6, 3, 0)]
    public void DrawPlanSplitsSolidAndNoCullRanges(
        int indexCount,
        int solidIndexCount,
        int solidFirst,
        int solidCount,
        int solidBase,
        int noCullFirst,
        int noCullCount,
        int noCullBase)
    {
        var plan = BlockEntitySubmission.CreateDrawPlan(indexCount, solidIndexCount);

        Assert.Equal(solidFirst, plan.Solid.FirstIndex);
        Assert.Equal(solidCount, plan.Solid.IndexCount);
        Assert.Equal(solidBase, plan.Solid.BaseVertex);
        Assert.Equal(noCullFirst, plan.NoCull.FirstIndex);
        Assert.Equal(noCullCount, plan.NoCull.IndexCount);
        Assert.Equal(noCullBase, plan.NoCull.BaseVertex);
    }

    [Fact]
    public void DrawPlanRejectsSolidCountBeyondTotal()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => BlockEntitySubmission.CreateDrawPlan(3, 4));
    }

    [Fact]
    public void SolidGeometryOccupiesTheLeadingRange()
    {
        var solid = new FakeRenderer("solid", BlockEntityRenderMode.Solid, marker: 1f);
        var noCull = new FakeRenderer("no-cull", BlockEntityRenderMode.NoCull, marker: 2f);
        var dispatcher = new BlockEntityRenderDispatcher([noCull, solid]);
        var entities = new[] { new BlockEntityRenderState(0f, 0f, 0f, 1) };

        var result = dispatcher.Build(entities, EmptyAtlas());

        Assert.NotNull(result);
        Assert.Equal(3, result.SolidIndexCount);
        Assert.Equal(6, result.Geometry.IndexCount);
        Assert.Equal([0u, 1u, 2u, 3u, 4u, 5u], result.Geometry.Indices[..result.Geometry.IndexCount]);
        Assert.Equal(1f, result.Geometry.Vertices[0]);
        Assert.Equal(2f, result.Geometry.Vertices[3 * ChunkMesher.FloatsPerVertex]);
        Assert.Equal(6 * ChunkMesher.FloatsPerVertex, result.Geometry.VertexFloats);
    }

    [Fact]
    public void NoCullOnlyBatchHasNoSolidRange()
    {
        var dispatcher = new BlockEntityRenderDispatcher([new FakeRenderer("no-cull", BlockEntityRenderMode.NoCull, marker: 2f)]);
        var entities = new[] { new BlockEntityRenderState(0f, 0f, 0f, 1) };

        var result = dispatcher.Build(entities, EmptyAtlas());

        Assert.NotNull(result);
        Assert.Equal(0, result.SolidIndexCount);
        Assert.Equal(3, result.Geometry.IndexCount);
    }

    [Fact]
    public void MixedBatchKeepsIndicesAndVertexOffsetsConsistent()
    {
        var solidA = new FakeRenderer("solid-a", BlockEntityRenderMode.Solid, marker: 1f, vertices: 4);
        var solidB = new FakeRenderer("solid-b", BlockEntityRenderMode.Solid, marker: 3f, vertices: 2);
        var noCull = new FakeRenderer("no-cull", BlockEntityRenderMode.NoCull, marker: 2f, vertices: 3);
        var dispatcher = new BlockEntityRenderDispatcher([noCull, solidA, solidB]);
        var entities = new[] { new BlockEntityRenderState(0f, 0f, 0f, 1) };

        var result = dispatcher.Build(entities, EmptyAtlas());

        Assert.NotNull(result);
        Assert.Equal(6, result.SolidIndexCount);
        Assert.Equal(9, result.Geometry.IndexCount);
        Assert.Equal(9 * ChunkMesher.FloatsPerVertex, result.Geometry.VertexFloats);
        Assert.Equal(1f, result.Geometry.Vertices[0]);
        Assert.Equal(3f, result.Geometry.Vertices[4 * ChunkMesher.FloatsPerVertex]);
        Assert.Equal(2f, result.Geometry.Vertices[6 * ChunkMesher.FloatsPerVertex]);
        for (var index = 0; index < result.Geometry.IndexCount; index++)
        {
            Assert.Equal((uint)index, result.Geometry.Indices[index]);
        }
    }

    [Fact]
    public void RegisteredSolidRenderersProduceSolidGeometry()
    {
        var atlas = BlockEntityAtlas();
        var dispatcher = new BlockEntityRenderDispatcher();

        var bell = dispatcher.Build([new BlockEntityRenderState(0f, 0f, 0f, VanillaBlockEntityTypes.Bell)], atlas);
        Assert.NotNull(bell);
        Assert.True(bell.SolidIndexCount > 0);
        Assert.Equal(bell.Geometry.IndexCount, bell.SolidIndexCount);

        var pot = dispatcher.Build([new BlockEntityRenderState(0f, 0f, 0f, VanillaBlockEntityTypes.DecoratedPot)], atlas);
        Assert.NotNull(pot);
        Assert.True(pot.SolidIndexCount > 0);
        Assert.Equal(pot.Geometry.IndexCount, pot.SolidIndexCount);

        var statue = dispatcher.Build([new BlockEntityRenderState(0f, 0f, 0f, VanillaBlockEntityTypes.CopperGolemStatue)], atlas);
        Assert.NotNull(statue);
        Assert.True(statue.SolidIndexCount > 0);
        Assert.Equal(statue.Geometry.IndexCount, statue.SolidIndexCount);
    }

    [Fact]
    public void RegisteredNoCullRendererProducesNoSolidGeometry()
    {
        var atlas = BlockEntityAtlas();
        var dispatcher = new BlockEntityRenderDispatcher();

        var chest = dispatcher.Build([new BlockEntityRenderState(0f, 0f, 0f, VanillaBlockEntityTypes.Chest)], atlas);
        Assert.NotNull(chest);
        Assert.True(chest.Geometry.IndexCount > 0);
        Assert.Equal(0, chest.SolidIndexCount);
    }

    private static ItemAtlas EmptyAtlas() =>
        ItemAtlas.FromTextures(new Dictionary<string, TextureData>(StringComparer.Ordinal));

    private static ItemAtlas BlockEntityAtlas()
    {
        var textures = new Dictionary<string, TextureData>(StringComparer.Ordinal);
        foreach (var path in ChestAtlas.Textures)
        {
            textures[path] = new(16, 16, new byte[16 * 16 * 4]);
        }

        foreach (var path in ChestAtlas.ShulkerTextures)
        {
            textures[path] = new(64, 64, new byte[64 * 64 * 4]);
        }

        foreach (var path in ChestAtlas.CopperGolemStatueTextures)
        {
            textures[path] = new(64, 64, new byte[64 * 64 * 4]);
        }

        textures[ChestAtlas.BellTexture] = new(32, 32, new byte[32 * 32 * 4]);
        textures[ChestAtlas.DecoratedPotBaseTexture] = new(32, 32, new byte[32 * 32 * 4]);
        textures[ChestAtlas.DecoratedPotSideTexture] = new(16, 16, new byte[16 * 16 * 4]);
        return ItemAtlas.FromTextures(textures);
    }

    private sealed class FakeRenderer : IBlockEntityRenderer
    {
        private readonly DynamicGeometry _geometry;

        public FakeRenderer(string name, BlockEntityRenderMode mode, float marker, int vertices = 3)
        {
            Name = name;
            RenderMode = mode;
            _geometry = new DynamicGeometry
            {
                Vertices = new float[vertices * ChunkMesher.FloatsPerVertex],
                VertexFloats = vertices * ChunkMesher.FloatsPerVertex,
                Indices = new uint[vertices],
                IndexCount = vertices
            };
            for (var vertex = 0; vertex < vertices; vertex++)
            {
                _geometry.Vertices[vertex * ChunkMesher.FloatsPerVertex] = marker;
                _geometry.Indices[vertex] = (uint)vertex;
            }
        }

        public string Name { get; }

        public BlockEntityRenderMode RenderMode { get; }

        public int LastEntityCount => 1;

        public bool Handles(int typeId) => true;

        public DynamicGeometry? Build(ReadOnlySpan<BlockEntityRenderState> entities, ItemAtlas atlas) => _geometry;
    }
}
