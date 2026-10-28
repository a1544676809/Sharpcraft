// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 MinecraftCSharp contributors

using MinecraftCSharp.Client.Assets;
using MinecraftCSharp.Client.Render;
using MinecraftCSharp.Client.World;
using MinecraftCSharp.Common.Identifiers;
using MinecraftCSharp.Common.Imaging;
using MinecraftCSharp.Common.Registries;
using MinecraftCSharp.Common.World;
using Xunit;

namespace MinecraftCSharp.Client.Tests;

/// <summary>
/// Texture-aware meshing (docs/modules/render.md §6.1): textured quads carry light only, their UVs
/// tile inside the sprite cell, and untextured quads keep the old hash colour.
/// </summary>
public sealed class TexturedMesherTests
{
    private const int FloatsPerVertex = ChunkMesher.FloatsPerVertex;

    [Fact]
    public void TexturedQuadsCarryLightInsteadOfTheStateHashColour()
    {
        var world = FlatWorld(state: 1);
        var sprites = SpriteMap();

        var mesh = ChunkMesher.Mesh(world, airState: 0, maxBlocks: 1000, sprites: sprites);

        // Pick any vertex of the top face: fully lit, top shade, so the vertex colour is neutral.
        var vertices = mesh.Vertices;
        Assert.NotEmpty(vertices);

        for (var i = 0; i < vertices.Length; i += FloatsPerVertex)
        {
            var red = vertices[i + 3];
            var green = vertices[i + 4];
            var blue = vertices[i + 5];

            Assert.Equal(red, green, 5);
            Assert.Equal(green, blue, 5);
        }
    }

    [Fact]
    public void UntexturedQuadsKeepTheHashColour()
    {
        var world = FlatWorld(state: 1);

        var mesh = ChunkMesher.Mesh(world, airState: 0, maxBlocks: 1000);

        var red = mesh.Vertices[3];
        var green = mesh.Vertices[4];
        var blue = mesh.Vertices[5];

        Assert.False(red == green && green == blue, "without sprites the state colour should stay tinted");
    }

    [Fact]
    public void MergedQuadTilesItsTextureInsteadOfStretchingIt()
    {
        var world = FlatWorld(state: 1);
        var sprites = SpriteMap();

        var mesh = ChunkMesher.Mesh(world, airState: 0, maxBlocks: 1000, sprites: sprites);

        // Every quad of the flat plane spans four blocks, so its UVs must reach 4 in one axis.
        var maxUv = 0f;
        for (var i = 0; i < mesh.Vertices.Length; i += FloatsPerVertex)
        {
            maxUv = MathF.Max(maxUv, MathF.Max(mesh.Vertices[i + 6], mesh.Vertices[i + 7]));
        }

        Assert.Equal(4f, maxUv, 3);
    }

    [Fact]
    public void SpriteRectReachesTheVertexStream()
    {
        var world = FlatWorld(state: 1);
        var sprites = SpriteMap();

        var mesh = ChunkMesher.Mesh(world, airState: 0, maxBlocks: 1000, sprites: sprites);

        var expected = sprites.ForState(1);
        var offset = 8;
        Assert.Equal(expected.U0, mesh.Vertices[offset], 5);
        Assert.Equal(expected.V0, mesh.Vertices[offset + 1], 5);
        Assert.Equal(expected.U1, mesh.Vertices[offset + 2], 5);
        Assert.Equal(expected.V1, mesh.Vertices[offset + 3], 5);
    }

    private static ClientWorld FlatWorld(int state)
    {
        var world = new ClientWorld(minSectionY: 0, sectionCount: 1);
        var chunk = world.AddChunk(0, 0);
        chunk.SetSection(new ChunkSection(singleState: 0, sectionY: 0));
        chunk.Light.FillSkyLight(15);

        for (var x = 0; x < 4; x++)
        {
            for (var z = 0; z < 4; z++)
            {
                chunk.SetBlockState(x, 0, z, state);
            }
        }

        return world;
    }

    private static BlockSpriteMap SpriteMap()
    {
        var atlas = SpriteAtlasBuilder.Stitch(
        [
            (new Identifier("minecraft", "block/stone"), Solid(16, 16, 143, 143, 143), 1),
            (SpriteAtlas.MissingSprite, Solid(16, 16, 255, 0, 255), 1)
        ]);

        var layout = new BlockStateLayout(
        [
            (new Identifier("minecraft", "air"), 1),
            (new Identifier("minecraft", "stone"), 1)
        ]);

        return new BlockSpriteMap(atlas, layout);
    }

    private static RgbaImage Solid(int width, int height, byte r, byte g, byte b)
    {
        var pixels = new byte[width * height * RgbaImage.BytesPerPixel];
        for (var i = 0; i < pixels.Length; i += RgbaImage.BytesPerPixel)
        {
            pixels[i] = r;
            pixels[i + 1] = g;
            pixels[i + 2] = b;
            pixels[i + 3] = 255;
        }

        return new RgbaImage(width, height, pixels);
    }
}
