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
    private static readonly (Vector3 Normal, int FacesAtStart)[] FaceOrder =
    [
        (new Vector3(-1, 0, 0), 0),
        (new Vector3(1, 0, 0), 1),
        (new Vector3(0, -1, 0), 2),
        (new Vector3(0, 1, 0), 3),
        (new Vector3(0, 0, -1), 4),
        (new Vector3(0, 0, 1), 5)
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

        // Each quad contributes two triangles; both must agree with the outward normal.
        for (var quad = 0; quad < 6; quad++)
        {
            var normal = NewellNormal(mesh, quad * 4);
            var expected = FaceOrder.Single(face => Vector3.Dot(normal, face.Normal) > 0.5f).Normal;

            Assert.True(
                Vector3.Dot(normal, expected) > 0.5f,
                $"quad {quad} points {normal} but the face should point {expected}");

            // Both triangles of the quad must wind the same way as the quad itself.
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
