// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Framework.Tests;

/// <summary>
/// The static world's buffers are read through properties because a rebuild can replace them. A consumer that
/// captured the handle earlier would keep drawing from the retired buffer, which is then destroyed — this is
/// the regression test for that (T-839).
/// </summary>
public sealed class StaticWorldBuffersTests
{
    private const ulong Minimum = 64;

    private static FrameRequest Request(int vertexFloats, int indices) =>
        new(
            1,
            1,
            new float[vertexFloats],
            new uint[indices],
            Vector4.Zero,
            Vector3.Zero,
            Vector3.UnitZ);

    private static (StaticWorldBuffers Buffers, FakeGeometryBackend Backend, List<BufferHandle> RetiredVertex,
        List<BufferHandle> RetiredIndex) Create(int vertexFloats = 8, int indices = 2)
    {
        var backend = new FakeGeometryBackend();
        var retiredVertex = new List<BufferHandle>();
        var retiredIndex = new List<BufferHandle>();
        var buffers = new StaticWorldBuffers(
            backend,
            Request(vertexFloats, indices),
            Minimum,
            retiredVertex,
            retiredIndex);
        return (buffers, backend, retiredVertex, retiredIndex);
    }

    [Fact]
    public void CreationAllocatesAndUploadsBothBuffers()
    {
        var (_, backend, _, _) = Create();

        Assert.Equal(2, backend.Created.Count);
        Assert.Equal(2, backend.Written.Count);
    }

    [Fact]
    public void RebuildingWithinCapacityKeepsTheSameHandles()
    {
        var (buffers, backend, retiredVertex, _) = Create(vertexFloats: 8, indices: 2);
        var vertex = buffers.Vertex;
        var index = buffers.Index;

        buffers.Upload(Request(8, 2));

        Assert.Same(vertex.Value, buffers.Vertex.Value);
        Assert.Same(index.Value, buffers.Index.Value);
        Assert.Equal(2, backend.Created.Count);
        Assert.Empty(retiredVertex);
    }

    [Fact]
    public void RebuildingPastCapacityMovesTheHandlesAndRetiresTheOldOnes()
    {
        var (buffers, backend, retiredVertex, retiredIndex) = Create(vertexFloats: 8, indices: 2);
        var vertexBefore = buffers.Vertex;
        var indexBefore = buffers.Index;

        buffers.Upload(Request(4096, 1024));

        // The point of the fix: reading the property gives the new buffer, not the captured one.
        Assert.NotSame(vertexBefore.Value, buffers.Vertex.Value);
        Assert.NotSame(indexBefore.Value, buffers.Index.Value);
        Assert.Equal(4, backend.Created.Count);
        Assert.Single(retiredVertex);
        Assert.Single(retiredIndex);
        Assert.Same(vertexBefore.Value, retiredVertex[0].Value);
        Assert.Same(indexBefore.Value, retiredIndex[0].Value);
    }

    [Fact]
    public void ShutdownDestroysTheLiveBuffersAndEverythingRetired()
    {
        var (buffers, backend, _, _) = Create(vertexFloats: 8, indices: 2);
        buffers.Upload(Request(4096, 1024));

        buffers.Destroy();

        // Two live plus two retired, each released exactly once — no double free.
        Assert.Equal(4, backend.Destroyed.Count);
        Assert.Equal(4, backend.Destroyed.Distinct().Count());
    }
}
