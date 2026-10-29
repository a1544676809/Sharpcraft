// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Imaging;
using Sharpcraft.Common.Registries;
using Sharpcraft.Common.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Texture orientation on every face, checked against the reference model baker
/// (ref://mc-26.3-analysis/src/net/minecraft/client/renderer/block/model/FaceBakery.java).
/// </summary>
/// <remarks>
/// The reference derives each face's texture coordinates from world axes with a fixed flip per direction:
/// <list type="bullet">
/// <item>Horizontal axis: south +X, north −X, east −Z, west +Z, up +X, down +X.</item>
/// <item>Vertical axis, expressed as where v = 0 samples: up is −Z, down is +Z, and every side face has v = 0 at
/// the top, so its texture is upright.</item>
/// </list>
/// Stone hides every one of these mistakes, which is exactly why they are measured instead of eyeballed.
/// </remarks>
public sealed class MeshTextureOrientationTests
{
    private const int FloatsPerVertex = ChunkMesher.FloatsPerVertex;

    [Theory]
    [InlineData("+Z", 1f, 0f, 0f, 0f, 1f, 0f)]   // south: u +X, v = 0 at the top (+Y)
    [InlineData("-Z", -1f, 0f, 0f, 0f, 1f, 0f)]  // north: u −X
    [InlineData("+X", 0f, 0f, -1f, 0f, 1f, 0f)]  // east: u −Z
    [InlineData("-X", 0f, 0f, 1f, 0f, 1f, 0f)]   // west: u +Z
    [InlineData("+Y", 1f, 0f, 0f, 0f, 0f, -1f)]  // up: u +X, v = 0 towards −Z
    [InlineData("-Y", 1f, 0f, 0f, 0f, 0f, 1f)]   // down: u +X, v = 0 towards +Z
    public void EachFaceCarriesTheReferenceTextureAxes(
        string face,
        float uX,
        float uY,
        float uZ,
        float vX,
        float vY,
        float vZ)
    {
        var corners = FaceCorners(face);

        var uAxis = Vector3.Normalize(corners.UAxis);

        // The image's own up is the direction of its top row, which is where v = 0 samples.
        var imageUp = Vector3.Normalize(corners.ImageUp);

        var expectedU = Vector3.Normalize(new Vector3(uX, uY, uZ));
        var expectedUp = Vector3.Normalize(new Vector3(vX, vY, vZ));

        Assert.True(
            Vector3.Dot(uAxis, expectedU) > 0.99f,
            $"face {face}: horizontal axis is {Describe(uAxis)}, expected {Describe(expectedU)}");

        Assert.True(
            Vector3.Dot(imageUp, expectedUp) > 0.99f,
            $"face {face}: texture up is {Describe(imageUp)}, expected {Describe(expectedUp)}");
    }

    /// <summary>The four corners, identified by the pair of coordinates rather than by one alone.</summary>
    private readonly record struct Corners(Vector3 AtU0V0, Vector3 AtU1V0, Vector3 AtU0V1, Vector3 AtU1V1)
    {
        public Vector3 UAxis => AtU1V0 - AtU0V0;

        public Vector3 ImageUp => AtU0V0 - AtU0V1;
    }

    private static Corners FaceCorners(string face)
    {
        var world = new ClientWorld(minSectionY: 0, sectionCount: 1);
        var chunk = world.AddChunk(0, 0);
        chunk.SetSection(new ChunkSection(singleState: 0, sectionY: 0));
        chunk.Light.FillSkyLight(15);

        // One isolated block, so all six faces are emitted.
        chunk.SetBlockState(1, 1, 1, state: 1);

        var mesh = ChunkMesher.Mesh(world, airState: 0, maxBlocks: 100, sprites: SpriteMap());
        var vertices = mesh.Vertices;

        for (var vertex = 0; vertex + (4 * FloatsPerVertex) <= vertices.Length; vertex += 4 * FloatsPerVertex)
        {
            var corners = ReadCorners(vertices, vertex);
            var centre = (corners.AtU0V0 + corners.AtU1V0 + corners.AtU0V1 + corners.AtU1V1) / 4f;
            var normal = Vector3.Normalize(centre - new Vector3(1.5f, 1.5f, 1.5f));

            if (Describe(normal) == face)
            {
                return corners;
            }
        }

        throw new Xunit.Sdk.XunitException($"the mesh had no face {face}");
    }

    private static Corners ReadCorners(ReadOnlySpan<float> vertices, int firstVertex)
    {
        var atU0V0 = Vector3.Zero;
        var atU1V0 = Vector3.Zero;
        var atU0V1 = Vector3.Zero;
        var atU1V1 = Vector3.Zero;

        for (var corner = 0; corner < 4; corner++)
        {
            var offset = firstVertex + (corner * FloatsPerVertex);
            var position = new Vector3(vertices[offset], vertices[offset + 1], vertices[offset + 2]);
            var lowU = vertices[offset + 6] < 0.001f;
            var highU = vertices[offset + 6] > 0.999f;
            var lowV = vertices[offset + 7] < 0.001f;

            // Two corners share each u value and two share each v value, so the pair is what identifies a corner.
            if (lowU && lowV)
            {
                atU0V0 = position;
            }
            else if (highU && lowV)
            {
                atU1V0 = position;
            }
            else if (lowU)
            {
                atU0V1 = position;
            }
            else
            {
                atU1V1 = position;
            }
        }

        return new Corners(atU0V0, atU1V0, atU0V1, atU1V1);
    }

    private static string Describe(Vector3 axis) =>
        (MathF.Abs(axis.X) > 0.5f ? (axis.X > 0 ? "+X" : "-X") : string.Empty)
        + (MathF.Abs(axis.Y) > 0.5f ? (axis.Y > 0 ? "+Y" : "-Y") : string.Empty)
        + (MathF.Abs(axis.Z) > 0.5f ? (axis.Z > 0 ? "+Z" : "-Z") : string.Empty);

    private static BlockSpriteMap SpriteMap()
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

        return new BlockSpriteMap(atlas, layout);
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
