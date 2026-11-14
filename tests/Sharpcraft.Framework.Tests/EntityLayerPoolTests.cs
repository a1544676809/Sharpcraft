// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Framework.Tests;

/// <summary>
/// The entity layer pool keeps seven parallel lists the same length and only rebuilds a layer's buffers when
/// the frame genuinely needs more room (T-837).
/// </summary>
public sealed class EntityLayerPoolTests
{
    private const ulong Minimum = 128;

    private static (EntityLayerPool Pool, FakeGeometryBackend Backend, DeferredBufferRelease Release) Create()
    {
        var backend = new FakeGeometryBackend();
        var release = new DeferredBufferRelease(backend.Destroy);
        return (new EntityLayerPool(backend, release, Minimum), backend, release);
    }

    [Fact]
    public void GrowingToThreeLayersAllocatesAVertexAndIndexEach()
    {
        var (pool, backend, _) = Create();

        pool.EnsureCapacity(3);

        Assert.Equal(3, pool.Count);
        Assert.Equal(6, backend.Created.Count);
        for (var layer = 0; layer < 3; layer++)
        {
            Assert.Equal(Minimum, pool.VertexCapacity(layer));
            Assert.Equal(Minimum, pool.IndexCapacity(layer));
            Assert.Equal(0u, pool.IndexCount(layer));
        }
    }

    [Fact]
    public void AskingForFewerLayersThanExistAllocatesNothing()
    {
        var (pool, backend, _) = Create();
        pool.EnsureCapacity(3);

        pool.EnsureCapacity(2);

        Assert.Equal(3, pool.Count);
        Assert.Equal(6, backend.Created.Count);
    }

    [Fact]
    public void GrowingALayerThatAlreadyFitsReplacesNothing()
    {
        var (pool, backend, release) = Create();
        pool.EnsureCapacity(1);
        var vertex = pool.Vertex(0);

        pool.GrowVertex(0, Minimum);
        pool.GrowVertex(0, Minimum - 1);
        pool.GrowIndex(0, Minimum);

        Assert.Same(vertex.Value, pool.Vertex(0).Value);
        Assert.Equal(2, backend.Created.Count);
        Assert.Equal(0, release.PendingCount);
    }

    [Fact]
    public void GrowingPastTheCapacityReplacesAndQueuesTheOldBuffer()
    {
        var (pool, backend, release) = Create();
        pool.EnsureCapacity(1);
        var vertex = pool.Vertex(0);

        pool.GrowVertex(0, Minimum * 4);

        Assert.NotSame(vertex.Value, pool.Vertex(0).Value);
        Assert.Equal(3, backend.Created.Count);
        Assert.Equal(Minimum * 8, pool.VertexCapacity(0));
        Assert.Equal(1, release.PendingCount);
        Assert.Empty(backend.Destroyed);
    }

    [Fact]
    public void PerLayerBookkeepingIsWrittenAndReadBack()
    {
        var (pool, _, _) = Create();
        pool.EnsureCapacity(2);

        pool.SetKind(1, EntityGeometryLayerKind.Elytra);
        pool.SetUsesItemAtlas(1, true);
        pool.SetIndexCount(1, 42);

        Assert.Equal(EntityGeometryLayerKind.Elytra, pool.Kind(1));
        Assert.True(pool.UsesItemAtlas(1));
        Assert.Equal(42u, pool.IndexCount(1));
        Assert.Equal(uint.MinValue, pool.IndexCount(0));
    }
}
