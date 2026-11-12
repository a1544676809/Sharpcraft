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
/// Greedy merging must not flatten smooth lighting or ambient occlusion. The reference never merges (one quad per block
/// face), so a merged quad is only allowed where the whole rectangle is lit uniformly; everywhere else the mesher falls
/// back to per-face quads. This pins that guarantee: any merged quad covering more than one block must have all four
/// corner colours identical, which is exactly the condition under which bilinear corner shading equals per-face shading.
/// </summary>
public sealed class ChunkMesherMergeLightingTests
{
    private const int Stone = 1;
    private const int FloatsPerVertex = ChunkMesher.FloatsPerVertex;

    /// <summary>A full stone section, optionally with one block carved out to create an AO gradient on the top surface.</summary>
    private static ClientWorld SolidWorld(bool withHole, out LevelChunk chunk)
    {
        var world = new ClientWorld(minSectionY: 0, sectionCount: 1);
        chunk = world.AddChunk(0, 0);
        chunk.SetSection(new ChunkSection(singleState: Stone, sectionY: 0));

        if (withHole)
        {
            // Removing one surface block leaves a one-deep pit whose walls darken the neighbouring top corners.
            chunk.SetBlockState(8, 15, 8, 0);
        }

        chunk.ApplyLight(LightData.FullSky(chunk.Light.LightSectionCount));
        chunk.MarkLoaded();
        return world;
    }

    private static MeshData MeshChunk(ClientWorld world, LevelChunk chunk, bool merge)
    {
        var (sprites, layout) = Resources();
        using var buffer = new MeshBuffer();
        ChunkMesher.MeshChunk(world, chunk, airState: 0, buffer, maxBlocks: 2_000_000, sprites: sprites, layout: layout, merge: merge);
        return buffer.ToMeshData(0);
    }

    private static (BlockSpriteMap Sprites, BlockStateLayout Layout) Resources()
    {
        var atlas = SpriteAtlasBuilder.Stitch(
        [
            (new Identifier("minecraft", "block/stone"), Solid(), 1),
            (SpriteAtlas.MissingSprite, Transparent(), 1)
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

    /// <summary>Fully transparent sprite, so the unknown/air fallback never counts as an occluder.</summary>
    private static RgbaImage Transparent() => new(16, 16, new byte[16 * 16 * RgbaImage.BytesPerPixel]);

    [Fact]
    public void MergedQuadsNeverInterpolateVaryingLighting()
    {
        var world = SolidWorld(withHole: true, out var chunk);
        var mesh = MeshChunk(world, chunk, merge: true);

        var multiBlockQuads = 0;

        for (var quad = 0; quad * 4 < mesh.Vertices.Length / FloatsPerVertex; quad++)
        {
            var baseVertex = quad * 4;

            if (QuadArea(mesh.Vertices, baseVertex) > 1.001f)
            {
                multiBlockQuads++;
                var (r, g, b) = Colour(mesh.Vertices, baseVertex);

                for (var corner = 1; corner < 4; corner++)
                {
                    var (cr, cg, cb) = Colour(mesh.Vertices, baseVertex + corner);
                    Assert.True(
                        MathF.Abs(cr - r) < 1e-4f && MathF.Abs(cg - g) < 1e-4f && MathF.Abs(cb - b) < 1e-4f,
                        $"merged quad at vertex {baseVertex} has non-uniform corners; lighting would be smeared");
                }
            }
        }

        Assert.True(multiBlockQuads > 0, "no quad merged, so the test exercised nothing");
    }

    [Fact]
    public void FlatOpenSurfaceMergesFewerQuadsThanUnmerged()
    {
        var merged = MeshChunk(SolidWorld(withHole: false, out var c1), c1, merge: true);
        var unmerged = MeshChunk(SolidWorld(withHole: false, out var c2), c2, merge: false);

        var mergedQuads = merged.Indices.Length / ChunkMesher.IndicesPerQuad;
        var unmergedQuads = unmerged.Indices.Length / ChunkMesher.IndicesPerQuad;

        Assert.True(mergedQuads < unmergedQuads, $"merged {mergedQuads} should be fewer than unmerged {unmergedQuads}");
    }

    [Fact]
    public void TheCarvedSceneActuallyProducesAnOcclusionGradient()
    {
        // Guard so the first test is not vacuous: an unmerged mesh of the pit scene must contain at least one quad whose
        // four corners differ, which is the ambient-occlusion gradient that merging is forbidden to flatten.
        var world = SolidWorld(withHole: true, out var chunk);
        var mesh = MeshChunk(world, chunk, merge: false);

        var varied = false;

        for (var quad = 0; quad * 4 < mesh.Vertices.Length / FloatsPerVertex && !varied; quad++)
        {
            var baseVertex = quad * 4;
            var (r, g, b) = Colour(mesh.Vertices, baseVertex);

            for (var corner = 1; corner < 4; corner++)
            {
                var (cr, cg, cb) = Colour(mesh.Vertices, baseVertex + corner);

                if (MathF.Abs(cr - r) > 1e-4f || MathF.Abs(cg - g) > 1e-4f || MathF.Abs(cb - b) > 1e-4f)
                {
                    varied = true;
                    break;
                }
            }
        }

        Assert.True(varied, "the pit scene produced no AO gradient; the merge-equivalence test is hollow");
    }

    private static (float R, float G, float B) Colour(float[] vertices, int vertex)
    {
        return VertexFieldReader.Colour(vertices, vertex);
    }

    private static float QuadArea(float[] vertices, int baseVertex)
    {
        Span<float> min = stackalloc float[3] { float.MaxValue, float.MaxValue, float.MaxValue };
        Span<float> max = stackalloc float[3] { float.MinValue, float.MinValue, float.MinValue };

        for (var corner = 0; corner < 4; corner++)
        {
            var offset = (baseVertex + corner) * FloatsPerVertex;

            for (var axis = 0; axis < 3; axis++)
            {
                min[axis] = MathF.Min(min[axis], vertices[offset + axis]);
                max[axis] = MathF.Max(max[axis], vertices[offset + axis]);
            }
        }

        var spans = new[] { max[0] - min[0], max[1] - min[1], max[2] - min[2] };
        Array.Sort(spans);
        return spans[1] * spans[2];
    }
}
