// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Imaging;
using Sharpcraft.Common.Network;
using Sharpcraft.Common.Registries;
using Sharpcraft.Common.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Ambient-occlusion equivalence (docs/specs/render/lighting.md 4, 8): the per-vertex coefficient is the vanilla 4-cell
/// average, and air must never count as an occluder (that bug collapsed the average to 0.2 everywhere and darkened the
/// whole world, which is why turning AO off looked correct).
/// </summary>
public sealed class AmbientOcclusionTests
{
    private const int Stone = 1;
    private const int FloatsPerVertex = ChunkMesher.FloatsPerVertex;

    [Fact]
    public void OpenCornerIsFullyBright()
    {
        Assert.Equal(1f, ChunkMesher.AmbientOcclusion(false, false, false, false), 5);
    }

    [Fact]
    public void SingleSolidNeighbourTakesAQuarterStepDown()
    {
        Assert.Equal(0.8f, ChunkMesher.AmbientOcclusion(true, false, false, false), 5);
        Assert.Equal(0.8f, ChunkMesher.AmbientOcclusion(false, true, false, false), 5);
        Assert.Equal(0.8f, ChunkMesher.AmbientOcclusion(false, false, false, true), 5); // open edges -> diagonal counts
    }

    [Fact]
    public void FullyEnclosedNookIsTheDarkest()
    {
        Assert.Equal(0.2f, ChunkMesher.AmbientOcclusion(true, true, true, true), 5);
    }

    [Fact]
    public void CornerSofteningIgnoresAnOpenDiagonalWhenBothEdgesAreSolid()
    {
        // Both edge cells solid, diagonal open: the reference treats the diagonal as occluded by the edges, so this
        // enclosed corner is 0.4, not 0.6. This is the corner-softening rule.
        Assert.Equal(0.4f, ChunkMesher.AmbientOcclusion(false, true, true, false), 5);

        // Contrast: with one edge open, the (solid) diagonal is counted normally -> 0.4 too, but via a different cell.
        Assert.Equal(0.4f, ChunkMesher.AmbientOcclusion(true, true, false, true), 5);
    }

    [Fact]
    public void AirIsNotAnOccluder()
    {
        // An isolated stone block under full sky: every cell around its top face is air, so the top face must stay fully
        // lit. The atlas' missing-sprite is deliberately opaque, so this only passes because air (state 0) is
        // short-circuited in Occludes; before that guard the top face was multiplied by the 0.2 AO floor.
        var world = new ClientWorld(minSectionY: 0, sectionCount: 1);
        var chunk = world.AddChunk(0, 0);
        chunk.SetSection(new ChunkSection(singleState: 0, sectionY: 0));
        chunk.SetBlockState(8, 8, 8, Stone);
        chunk.ApplyLight(LightData.FullSky(chunk.Light.LightSectionCount));
        chunk.MarkLoaded();

        var (sprites, layout) = Resources();
        using var buffer = new MeshBuffer();
        ChunkMesher.MeshChunk(world, chunk, airState: 0, buffer, maxBlocks: 1000, sprites: sprites, layout: layout);
        var mesh = buffer.ToMeshData(0);

        var brightest = 0f;
        for (var vertex = 0; vertex < mesh.Vertices.Length / FloatsPerVertex; vertex++)
        {
            var (red, green, blue) = VertexFieldReader.Colour(mesh.Vertices, vertex);
            brightest = MathF.Max(brightest, MathF.Max(red, MathF.Max(green, blue)));
        }

        Assert.True(brightest > 0.99f, $"open top face should be fully lit but brightest channel was {brightest}");
    }

    private static (BlockSpriteMap Sprites, BlockStateLayout Layout) Resources()
    {
        var atlas = SpriteAtlasBuilder.Stitch(
        [
            (new Identifier("minecraft", "block/stone"), Solid(), 1),
            (SpriteAtlas.MissingSprite, Solid(), 1)
        ]);

        var layout = new BlockStateLayout(
        [
            (new Identifier("minecraft", "air"), 1),
            (new Identifier("minecraft", "stone"), 1)
        ]);

        return (new BlockSpriteMap(atlas, layout), layout);
    }

    private static RgbaImage Solid()
    {
        var pixels = new byte[16 * 16 * RgbaImage.BytesPerPixel];

        for (var index = 0; index < pixels.Length; index += RgbaImage.BytesPerPixel)
        {
            pixels[index] = 143;
            pixels[index + 1] = 143;
            pixels[index + 2] = 143;
            pixels[index + 3] = 255;
        }

        return new RgbaImage(16, 16, pixels);
    }
}
