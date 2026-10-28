// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 MinecraftCSharp contributors

using System.Numerics;
using MinecraftCSharp.Client.Render;
using MinecraftCSharp.Client.World;
using MinecraftCSharp.Common.World;

namespace MinecraftCSharp.Client.Tests;

/// <summary>
/// Regression for a stray oversized quad that floated above the terrain: merging must never emit
/// geometry outside the block bounds the world actually contains.
/// </summary>
public sealed class ChunkMesherBoundsTests
{
    [Fact]
    public void NoQuadLiesOutsideTheSolidBounds()
    {
        var world = BuildHillWorld();
        var mesh = ChunkMesher.Mesh(world);

        // Terrain heights are 4..10, the slab sits at y = -1.
        const float lowest = -1f;
        const float highest = 11f;

        for (var quad = 0; quad < mesh.QuadCount; quad++)
        {
            for (var corner = 0; corner < 4; corner++)
            {
                var position = Corner(mesh, quad, corner);
                Assert.InRange(position.X, 0f, 16f);
                Assert.InRange(position.Z, 0f, 16f);
                Assert.InRange(position.Y, lowest, highest);
            }
        }
    }

    private static Vector3 Corner(MeshData mesh, int quad, int corner)
    {
        var vertex = (uint)((quad * ChunkMesher.VerticesPerQuad) + corner);
        var offset = (int)vertex * ChunkMesher.FloatsPerVertex;
        return new Vector3(mesh.Vertices[offset], mesh.Vertices[offset + 1], mesh.Vertices[offset + 2]);
    }

    private static ClientWorld BuildHillWorld()
    {
        var world = new ClientWorld(minSectionY: -1, sectionCount: 3);
        var chunk = world.AddChunk(0, 0);
        var solid = new ChunkSection(0, 0);
        var stone = new ChunkSection(0, -1);
        chunk.SetSection(solid);
        chunk.SetSection(stone);
        chunk.Light.FillSkyLight(15);

        for (var x = 0; x < 16; x++)
        {
            for (var z = 0; z < 16; z++)
            {
                var height = 4 + ((x * 3 + z * 5) % 7);
                for (var y = 0; y < height; y++)
                {
                    solid.Set(x, y, z, y < 2 ? 2 : 1);
                }

                stone.Set(x, 15, z, 3);
            }
        }

        return world;
    }
}
