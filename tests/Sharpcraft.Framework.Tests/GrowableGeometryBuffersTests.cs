// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Framework.Tests;

/// <summary>
/// The growable buffers only replace a buffer when the frame genuinely needs more room, and hand the replaced
/// one to the release queue rather than destroying it on the spot (T-837).
/// </summary>
public sealed class GrowableGeometryBuffersTests
{
    private const ulong Minimum = 256;

    private static (GrowableGeometryBuffers Buffers, FakeGeometryBackend Backend, DeferredBufferRelease Release) Create()
    {
        var backend = new FakeGeometryBackend();
        var release = new DeferredBufferRelease(backend.Destroy);
        return (new GrowableGeometryBuffers(backend, release, Minimum), backend, release);
    }

    [Fact]
    public void CreationAllocatesOneVertexAndOneIndexBuffer()
    {
        var (buffers, backend, _) = Create();

        Assert.Equal(2, backend.Created.Count);
        Assert.Equal(Minimum, buffers.VertexCapacity);
        Assert.Equal(Minimum, buffers.IndexCapacity);
        Assert.NotSame(buffers.Vertex.Value, buffers.Index.Value);
    }

    [Fact]
    public void AskingForLessThanTheCapacityKeepsTheSameBuffers()
    {
        var (buffers, backend, _) = Create();
        var vertex = buffers.Vertex;

        buffers.EnsureVertexCapacity(Minimum);
        buffers.EnsureVertexCapacity(Minimum - 1);
        buffers.EnsureIndexCapacity(Minimum);

        Assert.Same(vertex.Value, buffers.Vertex.Value);
        Assert.Equal(2, backend.Created.Count);
    }

    [Fact]
    public void AskingForMoreReplacesTheBufferAndQueuesTheOldOne()
    {
        var (buffers, backend, release) = Create();
        var original = buffers.Vertex;

        buffers.EnsureVertexCapacity(Minimum * 4);

        Assert.NotSame(original.Value, buffers.Vertex.Value);
        Assert.Equal(3, backend.Created.Count);
        Assert.Equal(Minimum * 8, buffers.VertexCapacity);

        // The old buffer is queued, not destroyed: a frame may still be reading it.
        Assert.Equal(1, release.PendingCount);
        Assert.Empty(backend.Destroyed);
    }

    [Fact]
    public void TheQueuedBufferIsDestroyedOnlyAfterTwoFrames()
    {
        var (buffers, backend, release) = Create();
        buffers.EnsureVertexCapacity(Minimum * 4);

        release.BeginFrame();
        release.Collect();
        Assert.Empty(backend.Destroyed);

        release.BeginFrame();
        release.Collect();
        Assert.Single(backend.Destroyed);
    }

    [Fact]
    public void ShutdownDestroysBothLiveBuffers()
    {
        var (buffers, backend, _) = Create();

        buffers.Destroy();

        Assert.Equal(2, backend.Destroyed.Count);
        Assert.Contains(backend.Destroyed, handle => handle.Value == buffers.Vertex.Value);
        Assert.Contains(backend.Destroyed, handle => handle.Value == buffers.Index.Value);
    }
}
