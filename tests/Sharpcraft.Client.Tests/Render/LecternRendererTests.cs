// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Common.World;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Golden tests for the static lectern book slice. Part dimensions and the fixed page state come from
/// <c>BookModel.java</c>; the outer translation/rotation sequence comes from <c>LecternRenderer.java</c>.
/// </summary>
public sealed class LecternRendererTests
{
    [Fact]
    public void BookDefinitionHasTheSevenReferenceParts()
    {
        Assert.Equal(7, BookModelDefinition.Definition.Parts.Count);
        Assert.Equal(
            new[] { "left_lid", "right_lid", "seam", "left_pages", "right_pages", "flip_page1", "flip_page2" },
            BookModelDefinition.Definition.Parts.Select(part => part.Name));
        Assert.Equal(64, BookModelDefinition.Definition.TextureWidth);
        Assert.Equal(32, BookModelDefinition.Definition.TextureHeight);
        Assert.Equal("entity/enchantment/enchanting_table_book", BookModelDefinition.Definition.TexturePath);
    }

    [Theory]
    [InlineData((byte)2, 270f)]
    [InlineData((byte)3, 90f)]
    [InlineData((byte)4, 180f)]
    [InlineData((byte)5, 0f)]
    public void ClockwiseYRotMatchesTheReference(byte facing, float expected)
    {
        Assert.Equal(expected, LecternRenderer.ClockwiseYRot(facing), 3);
    }

    [Fact]
    public void BookStateUsesTheReferenceAnimationFormula()
    {
        var state = BookModelDefinition.BookModelState.ForAnimation(0f, 0.1f, 0.9f, 1.2f);
        Assert.Equal(1.5f, state.Openness, 4);
        Assert.Equal(0.1f, state.PageFlip1, 4);
        Assert.Equal(0.9f, state.PageFlip2, 4);
    }

    /// <summary>
    /// A page vertex from the third emitted part (left_pages) proves the source
    /// <c>leftPages.x = sin(openness)</c> displacement is positive at openness 1.5.
    /// </summary>
    [Fact]
    public void PagePartVertexUsesPositiveXDisplacement()
    {
        var mesher = new LecternRenderer();
        var geometry = mesher.Build(
            [new BlockEntityRenderState(0f, 0f, 0f, VanillaBlockEntityTypes.Lectern, Facing: 2, HasBook: true)],
            Sprite());
        var position = VertexFieldReader.Position(geometry.Vertices, 72);
        var uv = VertexFieldReader.Uv(geometry.Vertices, 72);

        Assert.True(position.X > 0f);
        Assert.Equal(0.1883f, position.X, 3);
        Assert.Equal(0.9976f, position.Y, 3);
        Assert.Equal(0.1210f, position.Z, 3);
        Assert.Equal(6f / 64f, uv.U, 3);
        Assert.Equal(10f / 32f, uv.V, 3);
    }

    /// <summary>The static pose mirrors BookModel.setupAnim source-literal angles (BookModel.java:58-67).</summary>
    [Fact]
    public void BookPoseUsesTheReferencePartRotations()
    {
        var pose = BookModelDefinition.CreatePose(BookModelDefinition.BookModelState.ForAnimation(0f, 0.1f, 0.9f, 1.2f));
        Assert.Equal(MathF.PI + 1.5f, pose.Get("left_lid").Rotation.Y, 4);
        Assert.Equal(-1.5f, pose.Get("right_lid").Rotation.Y, 4);
        Assert.Equal(1.5f, pose.Get("left_pages").Rotation.Y, 4);
        Assert.Equal(-1.5f, pose.Get("right_pages").Rotation.Y, 4);
        Assert.Equal(1.2f, pose.Get("flip_page1").Rotation.Y, 4);
        Assert.Equal(-1.2f, pose.Get("flip_page2").Rotation.Y, 4);
        Assert.Equal(MathF.Sin(1.5f), pose.Get("left_pages").Position.X, 4);
    }

    /// <summary>
    /// Final vertex/UV samples against the raw source frame. UVs are source-literal
    /// (BookVectorProbe / book-raw-vectors.golden.txt): left_lid poly0 vertex 0 u=6/64,
    /// vertex 1 u≈0 (the 0.005-deep lid, legacy face calibration had given 12/64 here).
    /// Four facings plus a second emitted vertex are checked.
    /// </summary>
    [Theory]
    [InlineData((byte)2, 0, 0.4375f, 0.8951f, 0.0958f, 6f / 64f, 0f)]
    [InlineData((byte)2, 1, 0.0634f, 0.9196f, 0.0857f, 0f, 0f)]
    [InlineData((byte)3, 0, 0.5625f, 0.8951f, 0.9042f, 6f / 64f, 0f)]
    [InlineData((byte)4, 0, 0.0958f, 0.8951f, 0.5625f, 6f / 64f, 0f)]
    [InlineData((byte)5, 0, 0.9042f, 0.8951f, 0.4375f, 6f / 64f, 0f)]
    public void LecternBookSamplesFacingPose(byte facing, int vertex, float x, float y, float z, float u, float v)
    {
        var mesher = new LecternRenderer();
        var geometry = mesher.Build(
            [new BlockEntityRenderState(0f, 0f, 0f, VanillaBlockEntityTypes.Lectern, Facing: facing, HasBook: true)],
            Sprite());

        Assert.Equal(1, mesher.LastEntityCount);
        Assert.Equal(7 * 24 * ChunkMesher.FloatsPerVertex, geometry.VertexFloats);
        Assert.Equal(7 * 36, geometry.IndexCount);
        var position = VertexFieldReader.Position(geometry.Vertices, vertex);
        Assert.Equal(x, position.X, 3);
        Assert.Equal(y, position.Y, 3);
        Assert.Equal(z, position.Z, 3);
        var uv = VertexFieldReader.Uv(geometry.Vertices, vertex);
        Assert.Equal(u, uv.U, 3);
        Assert.Equal(v, uv.V, 3);
        Assert.Equal("entity/enchantment/enchanting_table_book", ChestAtlas.EnchantingTableBookTexture);
    }

    [Fact]
    public void NoBookDrawsNoGeometry()
    {
        var mesher = new LecternRenderer();
        var geometry = mesher.Build(
            [new BlockEntityRenderState(0f, 0f, 0f, VanillaBlockEntityTypes.Lectern, Facing: 2, HasBook: false)],
            Sprite());

        Assert.Equal(0, mesher.LastEntityCount);
        Assert.Equal(0, geometry.VertexFloats);
        Assert.Equal(0, geometry.IndexCount);
    }

    [Fact]
    public void TheBookTextureIsInTheBlockEntityAtlas()
    {
        var pixels = new byte[64 * 32 * 4];
        var atlas = ItemAtlas.FromTextures(new Dictionary<string, TextureData>(StringComparer.Ordinal)
        {
            [ChestAtlas.EnchantingTableBookTexture] = new(64, 32, pixels)
        });

        var sprite = ChestAtlas.SpriteForEnchantingTableBook(atlas);
        Assert.NotNull(sprite);
        var table = ChestAtlas.BuildRectTable(atlas);
        var offset = sprite!.RectId * 4;
        Assert.Equal(sprite.U0, table[offset]);
        Assert.Equal(sprite.V0, table[offset + 1]);
        Assert.Equal(sprite.U1, table[offset + 2]);
        Assert.Equal(sprite.V1, table[offset + 3]);

        var mesher = new LecternRenderer();
        var geometry = mesher.Build(
            [new BlockEntityRenderState(0f, 0f, 0f, VanillaBlockEntityTypes.Lectern, Facing: 2, HasBook: true)],
            atlas);
        Assert.Equal(1, mesher.LastEntityCount);
        Assert.True(geometry.VertexFloats > 0);
    }

    [Fact]
    public void RendererHandlesTheLecternOnly()
    {
        var renderers = BlockEntityRenderers.Create();
        Assert.True(BlockEntityRenderers.Handles(VanillaBlockEntityTypes.Lectern, renderers));
        Assert.False(BlockEntityRenderers.Handles(VanillaBlockEntityTypes.Furnace, renderers));
        Assert.True(BlockEntityRenderers.Handles(VanillaBlockEntityTypes.CopperGolemStatue, renderers));
    }

    private static SpriteRect Sprite() =>
        new(0, 0, 64, 32, 1) { RectId = 1, U0 = 0f, V0 = 0f, U1 = 1f, V1 = 1f };
}