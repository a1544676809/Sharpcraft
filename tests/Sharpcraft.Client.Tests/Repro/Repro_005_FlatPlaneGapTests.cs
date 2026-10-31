// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Common.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Repro;

/// <summary>
/// Repro-005：平坦地面上出现 1~2 像素的纯黑方格线。纯黑说明该处没有被任何三角形覆盖，
/// 所以先在 CPU 侧证明"合并后的面是否严格铺满整个平面"，再去怀疑 GPU。
/// 关联：docs/modules/render.md §6.1；`ChunkMesher.MergeSectionFaces`。
/// </summary>
public sealed class Repro_005_FlatPlaneFacesTileWithoutGapsTests
{
    [Fact]
    public void AFlatPlaneIsCoveredExactlyOnceByItsFaces()
    {
        const int size = 16;
        var world = new ClientWorld(minSectionY: 0, sectionCount: 1);
        var chunk = world.AddChunk(0, 0);
        chunk.SetSection(new ChunkSection(singleState: 0, sectionY: 0));
        chunk.Light.FillSkyLight(15);

        for (var z = 0; z < size; z++)
        {
            for (var x = 0; x < size; x++)
            {
                chunk.SetBlockState(x, 0, z, 1);
            }
        }

        var mesh = ChunkMesher.Mesh(world, airState: 0, maxBlocks: 100_000, sprites: null);

        var topArea = 0d;
        var bottomArea = 0d;
        var topTriangles = 0;

        // Sum over triangles through the index buffer, so the vertex layout cannot blur the measurement.
        for (var index = 0; index + 2 < mesh.Indices.Length; index += 3)
        {
            var a = VertexAt(mesh, mesh.Indices[index]);
            var b = VertexAt(mesh, mesh.Indices[index + 1]);
            var c = VertexAt(mesh, mesh.Indices[index + 2]);

            if (Math.Abs(a.Y - b.Y) > 0.001f || Math.Abs(a.Y - c.Y) > 0.001f)
            {
                continue;
            }

            var area = 0.5d * Math.Abs(((b.X - a.X) * (c.Z - a.Z)) - ((c.X - a.X) * (b.Z - a.Z)));

            if (a.Y is > 0.5f and < 1.5f)
            {
                topArea += area;
                topTriangles++;
            }
            else if (a.Y is > -0.5f and < 0.5f)
            {
                bottomArea += area;
            }
        }

        Console.WriteLine($"diag: vertices={mesh.Vertices.Length / ChunkMesher.FloatsPerVertex} indices={mesh.Indices.Length} topTris={topTriangles} topArea={topArea} bottomArea={bottomArea}");

        Assert.Equal((double)(size * size), topArea, 3);
        Assert.Equal((double)(size * size), bottomArea, 3);
    }

    private static Vector3 VertexAt(MeshData mesh, uint index)
    {
        var offset = (int)index * ChunkMesher.FloatsPerVertex;
        return new Vector3(mesh.Vertices[offset], mesh.Vertices[offset + 1], mesh.Vertices[offset + 2]);
    }
}
