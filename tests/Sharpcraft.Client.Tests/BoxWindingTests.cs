// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Common.World;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Tests;

/// <summary>
/// Measures the triangle winding of the boxes the chest and placeholder meshers emit. Every face of
/// every box must wind so that the cross product points out of the box; a face that winds inwards is
/// invisible the moment the pipeline enables back face culling.
/// </summary>
public sealed class BoxWindingTests
{
    /// <summary>The order AppendBox writes its faces in.</summary>
    private static readonly Vector3[] FaceNormals =
    [
        new(1f, 0f, 0f),
        new(-1f, 0f, 0f),
        new(0f, 1f, 0f),
        new(0f, -1f, 0f),
        new(0f, 0f, 1f),
        new(0f, 0f, -1f)
    ];

    private static List<(Vector3 Normal, string Face)> FaceNormalsOf(DynamicGeometry geometry)
    {
        var result = new List<(Vector3, string)>();
        var stride = ChunkMesher.FloatsPerVertex;

        for (var face = 0; face < geometry.IndexCount / 6; face++)
        {
            var i0 = (int)geometry.Indices[face * 6];
            var i1 = (int)geometry.Indices[face * 6 + 1];
            var i2 = (int)geometry.Indices[face * 6 + 2];

            var a = new Vector3(geometry.Vertices[i0 * stride], geometry.Vertices[i0 * stride + 1], geometry.Vertices[i0 * stride + 2]);
            var b = new Vector3(geometry.Vertices[i1 * stride], geometry.Vertices[i1 * stride + 1], geometry.Vertices[i1 * stride + 2]);
            var c = new Vector3(geometry.Vertices[i2 * stride], geometry.Vertices[i2 * stride + 1], geometry.Vertices[i2 * stride + 2]);

            var normal = Vector3.Cross(b - a, c - a);
            result.Add((normal.LengthSquared() > 1e-12f ? Vector3.Normalize(normal) : Vector3.Zero, $"face {face}"));
        }

        return result;
    }

    /// <summary>Every face of the three chest boxes winds outwards.</summary>
    [Fact]
    public void EveryChestFaceWindsOutwards()
    {
        var mesher = new ChestMesher();
        BlockEntityRenderState[] entities = [new(0f, 0f, 0f, VanillaBlockEntityTypes.Chest)];
        var geometry = mesher.Build(entities, new SpriteRect(0, 0, 16, 16, 1));

        var normals = FaceNormalsOf(geometry);
        Assert.Equal(18, normals.Count);

        var wrong = new List<string>();
        for (var face = 0; face < normals.Count; face++)
        {
            var expected = FaceNormals[face % 6];
            if (Vector3.Dot(normals[face].Normal, expected) < 0.5f)
            {
                wrong.Add($"box {face / 6} {normals[face].Face}: got {normals[face].Normal}, expected {expected}");
            }
        }

        Assert.True(wrong.Count == 0, "faces winding inwards:\n  " + string.Join("\n  ", wrong));
    }
}
