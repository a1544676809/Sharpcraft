// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 MinecraftCSharp contributors

using System.Numerics;
using MinecraftCSharp.Client.Render;
using MinecraftCSharp.Client.World;
using MinecraftCSharp.Common.World;

namespace MinecraftCSharp.Client.Tests;

/// <summary>
/// Triangle winding per cube face. GPU back-face culling only looks correct if every quad is wound
/// counter-clockwise when seen from outside the block, for both of its triangles.
/// </summary>
public sealed class ChunkMesherWindingTests
{
    private static readonly Vector3[] FaceOrder =
    [
        new Vector3(-1, 0, 0),
        new Vector3(1, 0, 0),
        new Vector3(0, -1, 0),
        new Vector3(0, 1, 0),
        new Vector3(0, 0, -1),
        new Vector3(0, 0, 1)
    ];

    [Fact]
    public void EveryFaceIsWoundCounterClockwiseFromOutside()
    {
        var world = new ClientWorld(minSectionY: 0, sectionCount: 1);
        var chunk = world.AddChunk(0, 0);
        chunk.SetSection(new ChunkSection(singleState: 0, sectionY: 0));
        chunk.SetBlockState(8, 8, 8, state: 1);

        var mesh = ChunkMesher.Mesh(world);

        Assert.Equal(6 * ChunkMesher.IndicesPerQuad, mesh.Indices.Length);

        // Quads are emitted in the classic order -X, +X, -Y, +Y, -Z, +Z; each quad's Newell normal
        // must point along that face's outward direction, and both of its triangles must agree.
        for (var quad = 0; quad < 6; quad++)
        {
            var expected = FaceOrder[quad];
            var normal = NewellNormal(mesh, quad * 4);

            Assert.True(
                Vector3.Dot(normal, expected) > 0.5f,
                $"quad {quad} points {normal} but the face should point {expected}");

            for (var triangle = 0; triangle < 2; triangle++)
            {
                var offset = (quad * ChunkMesher.IndicesPerQuad) + (triangle * 3);
                var winding = TriangleNormal(mesh, offset);
                Assert.True(
                    Vector3.Dot(winding, expected) > 0.5f,
                    $"quad {quad} triangle {triangle} winds towards {winding}, expected {expected}");
            }
        }
    }

    [Fact]
    public void MergedQuadsKeepTheSameWinding()
    {
        // A 4x4 slab merges its top and bottom faces into one quad each; winding must survive
        // the merge, which the single-block test cannot prove.
        var world = new ClientWorld(minSectionY: 0, sectionCount: 1);
        var chunk = world.AddChunk(0, 0);
        var section = new ChunkSection(singleState: 0, sectionY: 0);
        chunk.SetSection(section);
        for (var x = 0; x < 4; x++)
        {
            for (var z = 0; z < 4; z++)
            {
                section.Set(x, 0, z, 1);
            }
        }

        var mesh = ChunkMesher.Mesh(world);

        // Top and bottom are single merged quads; the four side strips are one quad each.
        Assert.Equal(6, mesh.QuadCount);
        Assert.Equal(16f + 16f + (4f * 4f), mesh.SurfaceArea, 3);

        for (var quad = 0; quad < mesh.QuadCount; quad++)
        {
            var expected = FaceOrder[quad];
            var normal = NewellNormal(mesh, quad * 4);
            Assert.True(
                Vector3.Dot(normal, expected) > 0.5f,
                $"merged quad {quad} points {normal}, expected {expected}");
        }
    }

    private static Vector3 TriangleNormal(MeshData mesh, int indexOffset)
    {
        var a = Position(mesh, mesh.Indices[indexOffset]);
        var b = Position(mesh, mesh.Indices[indexOffset + 1]);
        var c = Position(mesh, mesh.Indices[indexOffset + 2]);
        return Vector3.Normalize(Vector3.Cross(b - a, c - a));
    }

    /// <summary>Newell's method over a quad's four corners, giving a stable outward normal.</summary>
    private static Vector3 NewellNormal(MeshData mesh, int firstVertex)
    {
        var normal = Vector3.Zero;
        for (var i = 0; i < 4; i++)
        {
            var current = Position(mesh, (uint)(firstVertex + i));
            var next = Position(mesh, (uint)(firstVertex + ((i + 1) % 4)));
            normal.X += (current.Y - next.Y) * (current.Z + next.Z);
            normal.Y += (current.Z - next.Z) * (current.X + next.X);
            normal.Z += (current.X - next.X) * (current.Y + next.Y);
        }

        return Vector3.Normalize(normal);
    }

    private static Vector3 Position(MeshData mesh, uint vertex)
    {
        var offset = (int)vertex * ChunkMesher.FloatsPerVertex;
        return new Vector3(mesh.Vertices[offset], mesh.Vertices[offset + 1], mesh.Vertices[offset + 2]);
    }
}
