// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Common.World;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Golden tests for the copper golem statue's standing slice. The expected offsets come from the
/// reference <c>CopperGolemModel</c> part constants and <c>CopperGolemStatueBlockRenderer</c>'s
/// <c>translation(0.5, 0, 0.5)</c> plus <c>-facing.getOpposite().toYRot()</c> transform.
/// </summary>
public sealed class CopperGolemStatueMesherTests
{
    /// <summary>
    /// Four horizontal facings times two oxidation stages. The first vertex of the first body cube
    /// is a stable source-derived sample: its world position rotates with facing, while its UV is
    /// the same 64x64 model coordinate for every sprite.
    /// </summary>
    [Theory]
    [InlineData((byte)2, 0, 0.75f, 0.3125f, 0.6875f)]
    [InlineData((byte)2, 1, 0.75f, 0.3125f, 0.6875f)]
    [InlineData((byte)3, 0, 0.25f, 0.3125f, 0.3125f)]
    [InlineData((byte)3, 1, 0.25f, 0.3125f, 0.3125f)]
    [InlineData((byte)4, 0, 0.6875f, 0.3125f, 0.25f)]
    [InlineData((byte)4, 1, 0.6875f, 0.3125f, 0.25f)]
    [InlineData((byte)5, 0, 0.3125f, 0.3125f, 0.75f)]
    [InlineData((byte)5, 1, 0.3125f, 0.3125f, 0.75f)]
    public void StandingStatueSamplesFacingAndOxidation(byte facing, int oxidation, float x, float y, float z)
    {
        var mesher = new CopperGolemStatueMesher();
        var geometry = mesher.Build(
            [new BlockEntityRenderState(0f, 0f, 0f, VanillaBlockEntityTypes.CopperGolemStatue, Facing: facing, ChestMaterial: (byte)oxidation)],
            Sprites());

        Assert.Equal(1, mesher.LastEntityCount);
        Assert.Equal(9 * 24 * ChunkMesher.FloatsPerVertex, geometry.VertexFloats);
        Assert.Equal(9 * 36, geometry.IndexCount);
        Assert.Equal((x, y, z), VertexFieldReader.Position(geometry.Vertices, 0));
        Assert.Equal((14f / 64f, 15f / 64f), VertexFieldReader.Uv(geometry.Vertices, 0));
        Assert.Equal(oxidation + 1, VertexFieldReader.SpriteId(geometry.Vertices, 0));
    }

    /// <summary>Every bronze statue direction maps through the reference's opposite toYRot angle.</summary>
    [Theory]
    [InlineData((byte)0, -270f)]
    [InlineData((byte)1, -270f)]
    [InlineData((byte)2, 0f)]
    [InlineData((byte)3, -180f)]
    [InlineData((byte)4, -270f)]
    [InlineData((byte)5, -90f)]
    public void FacingDegreesMatchTheReferenceTransform(byte facing, float expected)
    {
        Assert.Equal(expected, CopperGolemStatueMesher.FacingDegrees(facing), 3);
    }

    /// <summary>The four body textures are stitched into the block entity atlas and resolve by stage.</summary>
    [Fact]
    public void TheCopperGolemTexturesAreInTheBlockEntityAtlas()
    {
        Assert.Equal(
            new[]
            {
                "entity/copper_golem/copper_golem",
                "entity/copper_golem/copper_golem_exposed",
                "entity/copper_golem/copper_golem_weathered",
                "entity/copper_golem/copper_golem_oxidized"
            },
            ChestAtlas.CopperGolemStatueTextures);

        var pixels = new byte[64 * 64 * 4];
        var textures = ChestAtlas.CopperGolemStatueTextures.ToDictionary(
            path => path,
            _ => new TextureData(64, 64, pixels),
            StringComparer.Ordinal);
        var atlas = ItemAtlas.FromTextures(textures);

        var table = ChestAtlas.BuildRectTable(atlas);
        for (var stage = 0; stage < ChestAtlas.CopperGolemStatueTextures.Length; stage++)
        {
            var sprite = ChestAtlas.SpriteForCopperGolemStatue(atlas, stage);
            Assert.NotNull(sprite);
            AssertRectInTable(table, sprite!);
        }

        var mesher = new CopperGolemStatueMesher();
        var geometry = mesher.Build(
            [new BlockEntityRenderState(3f, 2f, 5f, VanillaBlockEntityTypes.CopperGolemStatue, Facing: 2, ChestMaterial: CopperGolemStatueMesher.Oxidized)],
            atlas);
        Assert.Equal(1, mesher.LastEntityCount);
        Assert.True(geometry.VertexFloats > 0);
    }

    /// <summary>All four texture stages are packed and use distinct atlas sprites.</summary>
    [Fact]
    public void OxidationStagesSelectDistinctSprites()
    {
        var sprites = Sprites();
        var mesher = new CopperGolemStatueMesher();
        var geometry = mesher.Build(
            [new BlockEntityRenderState(0f, 0f, 0f, VanillaBlockEntityTypes.CopperGolemStatue, Facing: 2, ChestMaterial: CopperGolemStatueMesher.Oxidized)],
            sprites);

        Assert.Equal(CopperGolemStatueMesher.Oxidized + 1, VertexFieldReader.SpriteId(geometry.Vertices, 0));
        Assert.Equal("entity/copper_golem/copper_golem_oxidized", ChestAtlas.CopperGolemStatueTexture(CopperGolemStatueMesher.Oxidized));
    }

    /// <summary>The statue uses the centralized Direction ordinal predicate and its NORTH fallback.</summary>
    [Fact]
    public void TheStatueUsesTheCentralizedDirectionOrdinalPath()
    {
        Assert.Equal(47, VanillaBlockEntityTypes.CopperGolemStatue);
        Assert.True(VanillaBlockEntityTypes.UsesDirectionOrdinalFacing(VanillaBlockEntityTypes.CopperGolemStatue));
        Assert.Equal(2, VanillaBlockEntityTypes.DirectionOrdinalFallback(VanillaBlockEntityTypes.CopperGolemStatue));
        Assert.False(VanillaBlockEntityTypes.UsesDirectionOrdinalFacing(VanillaBlockEntityTypes.Furnace));
    }

    /// <summary>The statue type is registered and does not fall back to a placeholder renderer.</summary>
    [Fact]
    public void BlockEntityRendererHandlesTheStatue()
    {
        var renderers = BlockEntityRenderers.Create();
        Assert.True(BlockEntityRenderers.Handles(VanillaBlockEntityTypes.CopperGolemStatue, renderers));
        Assert.False(BlockEntityRenderers.Handles(VanillaBlockEntityTypes.Furnace, renderers));
        Assert.True(BlockEntityRenderers.Handles(VanillaBlockEntityTypes.DecoratedPot, renderers));
    }

    /// <summary>Rebuilding a warm statue mesher allocates nothing on the steady-state path.</summary>
    [Fact]
    public void RebuildingDoesNotAllocateOnceWarm()
    {
        var mesher = new CopperGolemStatueMesher();
        BlockEntityRenderState[] entities =
        [
            new(0f, 0f, 0f, VanillaBlockEntityTypes.CopperGolemStatue, Facing: 2, ChestMaterial: 0),
            new(3f, 2f, 4f, VanillaBlockEntityTypes.CopperGolemStatue, Facing: 5, ChestMaterial: 1)
        ];
        var sprites = Sprites();
        mesher.Build(entities, sprites);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var frame = 0; frame < 64; frame++)
        {
            mesher.Build(entities, sprites);
        }

        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
    }

    private static SpriteRect[] Sprites()
    {
        var sprites = new SpriteRect[4];
        for (var stage = 0; stage < sprites.Length; stage++)
        {
            sprites[stage] = new SpriteRect(0, 0, 64, 64, 1)
            {
                RectId = stage + 1,
                U0 = 0f,
                V0 = 0f,
                U1 = 1f,
                V1 = 1f
            };
        }

        return sprites;
    }

    private static void AssertRectInTable(float[] table, SpriteRect sprite)
    {
        var offset = sprite.RectId * 4;
        Assert.Equal(sprite.U0, table[offset]);
        Assert.Equal(sprite.V0, table[offset + 1]);
        Assert.Equal(sprite.U1, table[offset + 2]);
        Assert.Equal(sprite.V1, table[offset + 3]);
    }
}
