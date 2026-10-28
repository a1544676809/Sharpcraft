// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Common.World;

namespace Sharpcraft.Client.Tests;

/// <summary>
/// Structural invariants of greedy output: every quad must be an axis-aligned rectangle that lies
/// in one plane, with area equal to its two spans. A merge bug shows up here as a twisted or
/// oversized quad long before it is visible in a render.
/// </summary>
public sealed class ChunkMesherQuadTests
{
    [Fact]
    public void EveryQuadIsAnAxisAlignedRectangle()
    {
        var world = BuildTerrainWorld();
        var mesh = ChunkMesher.Mesh(world);

        Assert.True(mesh.QuadCount > 0);

        for (var quad = 0; quad < mesh.QuadCount; quad++)
        {
            var corners = Corners(mesh, quad);
            var planeAxis = -1;

            for (var axis = 0; axis < 3 && planeAxis < 0; axis++)
            {
                if (corners.All(corner => corner[axis] == corners[0][axis]))
                {
                    planeAxis = axis;
                }
            }

            Assert.True(planeAxis >= 0, $"quad {quad} is not flat: {Describe(corners)}");

            var otherAxes = Enumerable.Range(0, 3).Where(axis => axis != planeAxis).ToArray();
            foreach (var axis in otherAxes)
            {
                var distinct = corners.Select(corner => corner[axis]).Distinct().Count();
                Assert.True(distinct == 2, $"quad {quad} spans {distinct} values on axis {axis}: {Describe(corners)}");
            }

            var area = (corners.Max(corner => corner[otherAxes[0]]) - corners.Min(corner => corner[otherAxes[0]])) *
                       (corners.Max(corner => corner[otherAxes[1]]) - corners.Min(corner => corner[otherAxes[1]]));

            Assert.True(area > 0, $"quad {quad} has zero area: {Describe(corners)}");
            Assert.True(area <= 256, $"quad {quad} covers {area} blocks, larger than a section face: {Describe(corners)}");
        }
    }

    [Fact]
    public void SurfaceAreaMatchesThePerBlockFaceCount()
    {
        var world = BuildTerrainWorld();
        var mesh = ChunkMesher.Mesh(world);

        // Reference: count visible faces the naive way, one block at a time.
        var expected = 0;
        foreach (var chunk in world.LoadedChunks)
        {
            for (var x = 0; x < 16; x++)
            {
                for (var z = 0; z < 16; z++)
                {
                    for (var y = -16; y < 48; y++)
                    {
                        var state = chunk.GetBlockState(x, y, z);
                        if (state == 0)
                        {
                            continue;
                        }

                        var worldX = chunk.ChunkX * 16 + x;
                        var worldZ = chunk.ChunkZ * 16 + z;
                        expected += world.GetBlockState(worldX - 1, y, worldZ) == 0 ? 1 : 0;
                        expected += world.GetBlockState(worldX + 1, y, worldZ) == 0 ? 1 : 0;
                        expected += world.GetBlockState(worldX, y - 1, worldZ) == 0 ? 1 : 0;
                        expected += world.GetBlockState(worldX, y + 1, worldZ) == 0 ? 1 : 0;
                        expected += world.GetBlockState(worldX, y, worldZ - 1) == 0 ? 1 : 0;
                        expected += world.GetBlockState(worldX, y, worldZ + 1) == 0 ? 1 : 0;
                    }
                }
            }
        }

        Assert.Equal(expected, mesh.SurfaceArea, 2);
        Assert.True(mesh.QuadCount < expected, $"greedy meshing emitted {mesh.QuadCount} quads for {expected} faces");
    }

    private static Vector3[] Corners(MeshData mesh, int quad)
    {
        var corners = new Vector3[4];
        for (var corner = 0; corner < 4; corner++)
        {
            var vertex = (uint)((quad * ChunkMesher.VerticesPerQuad) + corner);
            var offset = (int)vertex * ChunkMesher.FloatsPerVertex;
            corners[corner] = new Vector3(mesh.Vertices[offset], mesh.Vertices[offset + 1], mesh.Vertices[offset + 2]);
        }

        return corners;
    }

    private static string Describe(Vector3[] corners) =>
        string.Join(" ", corners.Select(corner => $"({corner.X},{corner.Y},{corner.Z})"));

    private static ClientWorld BuildTerrainWorld()
    {
        var world = new ClientWorld(minSectionY: -1, sectionCount: 3);

        for (var chunkX = 0; chunkX < 2; chunkX++)
        {
            for (var chunkZ = 0; chunkZ < 2; chunkZ++)
            {
                var chunk = world.AddChunk(chunkX, chunkZ);
                for (var sectionY = -1; sectionY < 2; sectionY++)
                {
                    var section = new ChunkSection(0, sectionY);
                    chunk.SetSection(section);

                    if (sectionY == -1)
                    {
                        for (var x = 0; x < 16; x++)
                        {
                            for (var z = 0; z < 16; z++)
                            {
                                section.Set(x, 15, z, 4);
                            }
                        }
                    }
                }

                for (var x = 0; x < 16; x++)
                {
                    for (var z = 0; z < 16; z++)
                    {
                        var height = 2 + ((x + z) % 5);
                        for (var y = 0; y < height; y++)
                        {
                            chunk.GetSection(0)!.Set(x, y, z, y + 1 < height ? 1 : 2);
                        }
                    }
                }
            }
        }

        return world;
    }
}
