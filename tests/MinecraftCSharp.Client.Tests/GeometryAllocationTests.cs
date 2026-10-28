// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 MinecraftCSharp contributors

using System.Numerics;
using MinecraftCSharp.Client.Render;
using MinecraftCSharp.Client.World;
using MinecraftCSharp.Common.World;
using MinecraftCSharp.Framework.Rendering;

namespace MinecraftCSharp.Client.Tests;

/// <summary>
/// Steady-state allocation of the geometry path (AGENTS.md 3.6). Meshing into a warm
/// <see cref="MeshBuffer"/> and transforming into a caller-provided span must not allocate; this is
/// what keeps the frame budget predictable (docs/specs/performance-baseline.md §7).
/// </summary>
public sealed class GeometryAllocationTests
{
    [Fact]
    public void MeshingIntoAWarmBufferDoesNotAllocate()
    {
        var world = BuildWorld();
        using var buffer = new MeshBuffer(expectedBlocks: 4096);

        // Warm up: rent, grow to the final size, and fill every code path.
        Assert.True(ChunkMesher.Mesh(world, buffer) > 0);

        var before = GC.GetAllocatedBytesForCurrentThread();
        var blocks = ChunkMesher.Mesh(world, buffer);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(blocks > 0);
        Assert.Equal(0, allocated);
    }

    [Fact]
    public void BufferReuseProducesTheSameGeometry()
    {
        var world = BuildWorld();
        using var buffer = new MeshBuffer(expectedBlocks: 4096);

        ChunkMesher.Mesh(world, buffer);
        var first = buffer.ToMeshData(blockCount: 1);

        ChunkMesher.Mesh(world, buffer);
        var second = buffer.ToMeshData(blockCount: 1);

        Assert.Equal(first.Vertices.Length, second.Vertices.Length);
        Assert.Equal(first.Indices.Length, second.Indices.Length);
        Assert.Equal(
            ChunkMesherGoldenTests.Fingerprint(first),
            ChunkMesherGoldenTests.Fingerprint(second));
    }

    [Fact]
    public void MeshConvenienceOverloadMatchesTheBufferOverload()
    {
        var world = BuildWorld();
        using var buffer = new MeshBuffer(expectedBlocks: 4096);

        var blocks = ChunkMesher.Mesh(world, buffer);
        var fromBuffer = buffer.ToMeshData(blocks);
        var fromConvenience = ChunkMesher.Mesh(world);

        Assert.Equal(fromConvenience.BlockCount, fromBuffer.BlockCount);
        Assert.Equal(
            ChunkMesherGoldenTests.Fingerprint(fromConvenience),
            ChunkMesherGoldenTests.Fingerprint(fromBuffer));
    }

    [Fact]
    public void TransformingIntoAProvidedSpanDoesNotAllocate()
    {
        var request = BuildRequest();
        var matrix = CameraMath.BuildViewProjection(
            request.CameraPosition,
            request.CameraTarget,
            60f,
            request.Width,
            request.Height);
        var destination = new float[ClipSpaceTransform.RequiredFloats(request)];

        ClipSpaceTransform.Build(request, matrix, destination);

        var before = GC.GetAllocatedBytesForCurrentThread();
        ClipSpaceTransform.Build(request, matrix, destination);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
    }

    [Fact]
    public void SpanTransformMatchesTheAllocatingOverload()
    {
        var request = BuildRequest();
        var matrix = CameraMath.BuildViewProjection(
            request.CameraPosition,
            request.CameraTarget,
            60f,
            request.Width,
            request.Height);

        var expected = ClipSpaceTransform.Build(request, matrix);
        var actual = new float[expected.Length];
        ClipSpaceTransform.Build(request, matrix, actual);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void TooSmallADestinationIsRejected()
    {
        var request = BuildRequest();
        var matrix = Matrix4x4.Identity;

        Assert.Throws<ArgumentException>(
            () => ClipSpaceTransform.Build(request, matrix, new float[3]));
    }

    private static ClientWorld BuildWorld()
    {
        var world = new ClientWorld(minSectionY: 0, sectionCount: 2);

        for (var chunkX = 0; chunkX < 2; chunkX++)
        {
            for (var chunkZ = 0; chunkZ < 2; chunkZ++)
            {
                var chunk = world.AddChunk(chunkX, chunkZ);
                for (var sectionY = 0; sectionY < 2; sectionY++)
                {
                    var section = new ChunkSection(0, sectionY);
                    chunk.SetSection(section);

                    for (var x = 0; x < 16; x++)
                    {
                        for (var z = 0; z < 16; z++)
                        {
                            var height = 3 + ((x + z + chunkX * 3 + chunkZ * 5) % 9);
                            for (var y = 0; y < height; y++)
                            {
                                section.Set(x, y, z, 1 + (y % 4));
                            }
                        }
                    }
                }
            }
        }

        return world;
    }

    private static FrameRequest BuildRequest()
    {
        var world = BuildWorld();
        var mesh = ChunkMesher.Mesh(world);
        return new FrameRequest(
            Width: 640,
            Height: 360,
            Vertices: mesh.Vertices,
            Indices: mesh.Indices,
            ClearColour: Vector4.One,
            CameraPosition: new Vector3(30, 40, 30),
            CameraTarget: new Vector3(8, 4, 8));
    }
}
