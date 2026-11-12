// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Imaging;
using Sharpcraft.Common.Registries;
using Sharpcraft.Common.World;
using Xunit;

namespace Sharpcraft.Client.Tests;

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

        for (var vertex = 0; vertex < vertices.Length / FloatsPerVertex; vertex++)
        {
            var (red, green, blue) = VertexFieldReader.Colour(vertices, vertex);

            Assert.Equal(red, green, 2);
            Assert.Equal(green, blue, 2);
        }
    }

    [Fact]
    public void UntexturedQuadsKeepTheHashColour()
    {
        var world = FlatWorld(state: 1);

        var mesh = ChunkMesher.Mesh(world, airState: 0, maxBlocks: 1000);

        var (red, green, blue) = VertexFieldReader.Colour(mesh.Vertices, 0);

        // A vertex on a chunk-edge side face has no neighbour light (the reference is black there too), so scan vertices
        // for any tinted one rather than trusting the first.
        var tinted = red != green || green != blue;

        for (var vertex = 1; !tinted && vertex < mesh.Vertices.Length / FloatsPerVertex; vertex++)
        {
            var (r, g, b) = VertexFieldReader.Colour(mesh.Vertices, vertex);
            tinted = r != g || g != b;
        }

        Assert.True(tinted, "without sprites the state colour should stay tinted");
    }

    [Fact]
    public void MergedQuadTilesItsTextureInsteadOfStretchingIt()
    {
        var world = FlatWorld(state: 1);
        var sprites = SpriteMap();

        var mesh = ChunkMesher.Mesh(world, airState: 0, maxBlocks: 1000, sprites: sprites);

        // Every quad of the flat plane spans four blocks, so its UVs must reach 4 in one axis.
        var maxUv = 0f;
        for (var vertex = 0; vertex < mesh.Vertices.Length / FloatsPerVertex; vertex++)
        {
            var (u, v) = VertexFieldReader.Uv(mesh.Vertices, vertex);
            maxUv = MathF.Max(maxUv, MathF.Max(u, v));
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
        var (u0, v0, u1, v1) = VertexFieldReader.Rect(mesh.Vertices, 0);
        Assert.Equal(expected.U0, u0, 5);
        Assert.Equal(expected.V0, v0, 5);
        Assert.Equal(expected.U1, u1, 5);
        Assert.Equal(expected.V1, v1, 5);
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
