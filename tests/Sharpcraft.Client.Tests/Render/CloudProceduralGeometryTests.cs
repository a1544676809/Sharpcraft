// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Render;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Proves the face table plus the corner decoding written in the spec reproduces the geometry the renderer builds
/// today, corner for corner (T-827 S3).
/// </summary>
/// <remarks>
/// The shader that will replace <see cref="CloudGeometryBuilder"/> generates its corners from a vertex index, so
/// before any of that reaches the GPU this test decodes the table the same way and compares against the existing
/// builder's output. If the two agree here, the remaining risk in the rewrite is shading, not geometry; if they do
/// not, the rewrite would have been measured against a broken reference and every later pixel comparison would be
/// chasing the wrong difference.
/// </remarks>
public sealed class CloudProceduralGeometryTests
{
    /// <summary>
    /// The six faces of a cell, four corners each, in the order the reference emits them: down, up, north, south,
    /// west, east. Written out rather than packed into constants so it can be read against the spec.
    /// </summary>
    private static readonly Vector3[] Corners =
    [
        new(1, 0, 0), new(1, 0, 1), new(0, 0, 1), new(0, 0, 0),
        new(0, 1, 0), new(0, 1, 1), new(1, 1, 1), new(1, 1, 0),
        new(0, 0, 0), new(0, 1, 0), new(1, 1, 0), new(1, 0, 0),
        new(1, 0, 1), new(1, 1, 1), new(0, 1, 1), new(0, 0, 1),
        new(0, 0, 1), new(0, 1, 1), new(0, 1, 0), new(0, 0, 0),
        new(1, 0, 0), new(1, 1, 0), new(1, 1, 1), new(1, 0, 1)
    ];

    /// <summary>Floats per vertex in the cloud geometry the renderer builds today: position, brightness and fog.</summary>
    private const int FloatsPerVertex = 7;

    private const float CellSize = 12f;
    private const float Thickness = 4f;
    private const float FogEnd = 160f;

    private static readonly Vector3 Origin = new(40f, 192f, -16f);
    private static readonly Vector3 Eye = new(44f, 196f, -12f);

    [Fact]
    public void TheCornerTableMatchesTheReferenceConstants() =>
        // The reference packs this same table into three 24 bit constants. Recomputing them from the list above is a
        // cheap way to prove the table was transcribed correctly rather than eyeballed.
        Assert.Equal((0xF03CC3, 0x6666F0, 0xC3F066), PackCorners());

    private static (int X, int Y, int Z) PackCorners()
    {
        var x = 0;
        var y = 0;
        var z = 0;
        for (var index = 0; index < Corners.Length; index++)
        {
            if (Corners[index].X != 0f)
            {
                x |= 1 << index;
            }

            if (Corners[index].Y != 0f)
            {
                y |= 1 << index;
            }

            if (Corners[index].Z != 0f)
            {
                z |= 1 << index;
            }
        }

        return (x, y, z);
    }

    [Fact]
    public void DecodingTheTableReproducesTheGeometryTheRendererBuildsToday()
    {
        List<CloudQuad> quads =
        [
            new(2, 3, CloudFaceDirection.Up, InsideFace: false),
            new(2, 3, CloudFaceDirection.North, InsideFace: false),
            new(-1, 5, CloudFaceDirection.West, InsideFace: true),
            new(0, 0, CloudFaceDirection.Down, InsideFace: true),
            new(7, -2, CloudFaceDirection.East, InsideFace: false)
        ];

        var (vertices, indices) = CloudGeometryBuilder.Build(
            quads, Eye, Origin, CellSize, Thickness, FogEnd);

        var table = CloudFaceTable.Build(quads);

        // One quad is four vertices and six indices; the table is three words per quad.
        Assert.Equal(quads.Count * 4 * FloatsPerVertex, vertices.Length);
        Assert.Equal(quads.Count * 6, indices.Length);
        Assert.Equal(quads.Count * CloudFaceTable.WordsPerFace, table.Length);

        for (var quad = 0; quad < quads.Count; quad++)
        {
            for (var corner = 0; corner < 4; corner++)
            {
                var vertexIndex = (quad * 4) + corner;
                var expected = ReadPosition(vertices, vertexIndex);
                var decoded = DecodePosition(table, quad, corner);

                Assert.Equal(expected.X, decoded.X, 4);
                Assert.Equal(expected.Y, decoded.Y, 4);
                Assert.Equal(expected.Z, decoded.Z, 4);
            }
        }
    }

    private static Vector3 ReadPosition(float[] vertices, int vertex)
    {
        var offset = vertex * FloatsPerVertex;
        return new Vector3(vertices[offset], vertices[offset + 1], vertices[offset + 2]);
    }

    /// <summary>
    /// The decoding the procedural shader will perform: read three words, put the cell coordinate back together from
    /// the halved word and its flag bit, pick the corner, then scale and place it.
    /// </summary>
    private static Vector3 DecodePosition(int[] table, int quad, int corner)
    {
        var word = quad * CloudFaceTable.WordsPerFace;
        var flags = table[word + 2];

        var direction = flags & CloudFaceTable.DirectionMask;
        var insideFace = (flags & CloudFaceTable.FlagInsideFace) != 0;
        var cellX = (table[word] << 1) | ((flags & CloudFaceTable.FlagExtraX) >> 7);
        var cellZ = (table[word + 1] << 1) | ((flags & CloudFaceTable.FlagExtraZ) >> 6);

        var cornerIndex = (direction * 4) + (insideFace ? 3 - corner : corner);
        var face = Corners[cornerIndex];

        return new Vector3(
            Origin.X + (cellX * CellSize) + (face.X * CellSize),
            Origin.Y + (face.Y * Thickness),
            Origin.Z + (cellZ * CellSize) + (face.Z * CellSize));
    }
}
