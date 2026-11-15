// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Common.World;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Golden tests for the static enchanting-table book slice. The expected vertices are the final
/// output after the source outer pose and the default book state; reference inputs are
/// <c>EnchantTableRenderer.java</c> and <c>BookModel.java</c>.
/// </summary>
public sealed class EnchantTableRendererTests
{
    [Fact]
    public void BookModelDefinitionRegression()
    {
        Assert.Equal(7, BookModelDefinition.Definition.Parts.Count);
        Assert.Equal(64, BookModelDefinition.Definition.TextureWidth);
        Assert.Equal(32, BookModelDefinition.Definition.TextureHeight);
        Assert.Equal("entity/enchantment/enchanting_table_book", BookModelDefinition.Definition.TexturePath);

        var state = BookModelDefinition.BookModelState.ForAnimation(0f, 0.1f, 0.9f, 0f);
        Assert.Equal(0f, state.Openness, 4);
        Assert.Equal(0.1f, state.PageFlip1, 4);
        Assert.Equal(0.9f, state.PageFlip2, 4);
        Assert.Equal(0f, EnchantTableRenderer.BookState.Openness, 4);
    }

    /// <summary>
    /// Four final vertex/UV samples from the source default state: the four emitted part groups
    /// (lid, second lid, pages, right pages) each exercise a different book-model part.
    /// </summary>
    [Theory]
    [InlineData(0, 0.80775f, 0.79573f, 0.4375f, 0.09375f, 0f)]
    [InlineData(24, 0.87287f, 1.16504f, 0.5625f, 0.34375f, 0f)]
    [InlineData(72, 0.80047f, 1.11434f, 0.50063f, 0.09375f, 0.3125f)]
    [InlineData(96, 0.80047f, 1.11434f, 0.56188f, 0.28125f, 0.3125f)]
    public void EnchantTableBookSamplesFinalVertices(int vertex, float x, float y, float z, float u, float v)
    {
        var mesher = new EnchantTableRenderer();
        var geometry = mesher.Build(
            [new BlockEntityRenderState(0f, 0f, 0f, VanillaBlockEntityTypes.EnchantingTable)],
            Sprite());

        Assert.Equal(1, mesher.LastEntityCount);
        Assert.Equal(7 * 24 * ChunkMesher.FloatsPerVertex, geometry.VertexFloats);
        Assert.Equal(7 * 36, geometry.IndexCount);
        var position = VertexFieldReader.Position(geometry.Vertices, vertex);
        var uv = VertexFieldReader.Uv(geometry.Vertices, vertex);
        Assert.Equal(x, position.X, 3);
        Assert.Equal(y, position.Y, 3);
        Assert.Equal(z, position.Z, 3);
        Assert.Equal(u, uv.U, 3);
        Assert.Equal(v, uv.V, 3);
    }

    [Fact]
    public void RendererHandlesTheEnchantingTableOnly()
    {
        var renderers = BlockEntityRenderers.Create();
        Assert.True(BlockEntityRenderers.Handles(VanillaBlockEntityTypes.EnchantingTable, renderers));
        Assert.False(BlockEntityRenderers.Handles(VanillaBlockEntityTypes.Furnace, renderers));
    }

    [Fact]
    public void TheBookTextureIsTheExistingBlockEntityTexture()
    {
        Assert.Equal("entity/enchantment/enchanting_table_book", ChestAtlas.EnchantingTableBookTexture);

        var pixels = new byte[64 * 32 * 4];
        var atlas = ItemAtlas.FromTextures(new Dictionary<string, TextureData>(StringComparer.Ordinal)
        {
            [ChestAtlas.EnchantingTableBookTexture] = new(64, 32, pixels)
        });
        var sprite = ChestAtlas.SpriteForEnchantingTableBook(atlas);
        Assert.NotNull(sprite);

        var geometry = new EnchantTableRenderer().Build(
            [new BlockEntityRenderState(0f, 0f, 0f, VanillaBlockEntityTypes.EnchantingTable)],
            atlas);
        Assert.True(geometry.VertexFloats > 0);
    }

    private static SpriteRect Sprite() =>
        new(0, 0, 64, 32, 1) { RectId = 1, U0 = 0f, V0 = 0f, U1 = 1f, V1 = 1f };
}
