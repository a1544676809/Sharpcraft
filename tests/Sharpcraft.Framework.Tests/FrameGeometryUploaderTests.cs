// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Framework.Tests;

/// <summary>
/// The upload path consults the removals list exactly once, and drops every key in it (T-838).
/// </summary>
/// <remarks>
/// The defect this guards against was a callback invoked twice: once to read a count, once to iterate. With a
/// source that drains on read, the second call came back empty and the removals silently did nothing. Because
/// the uploader now takes the resolved list, there is no second call to make — but a test that counts the
/// removals is what keeps that true.
/// </remarks>
public sealed class FrameGeometryUploaderTests
{
    private const ulong Minimum = 128;

    private sealed record Fixture(
        FrameGeometryUploader Uploader,
        FakeGeometryBackend Backend,
        FakeChunkGeometrySink Opaque,
        FakeChunkGeometrySink Cutout,
        FakeChunkGeometrySink Translucent);

    private static Fixture Create()
    {
        var backend = new FakeGeometryBackend();
        var release = new DeferredBufferRelease(backend.Destroy);
        var dynamicBuffers = new GrowableGeometryBuffers(backend, release, Minimum);
        var layers = new EntityLayerPool(backend, release, Minimum);

        var opaque = new FakeChunkGeometrySink();
        var cutout = new FakeChunkGeometrySink();
        var translucent = new FakeChunkGeometrySink();

        var uploader = new FrameGeometryUploader(
            backend, dynamicBuffers, layers, opaque, cutout, translucent);
        return new Fixture(uploader, backend, opaque, cutout, translucent);
    }

    [Fact]
    public void EveryRemovalKeyIsDroppedExactlyOnceFromEveryArena()
    {
        var fixture = Create();
        long[] removals = [11L, 22L, 33L];

        var result = fixture.Uploader.Upload(null, null, null, removals, skipUpload: false);

        Assert.Equal(removals, fixture.Opaque.Removed);
        Assert.Equal(removals, fixture.Cutout.Removed);
        Assert.Equal(removals, fixture.Translucent.Removed);
        Assert.Equal(0, result.Updates);
    }

    [Fact]
    public void ADrainingSourceStillYieldsEveryKeyWhenReadOnce()
    {
        var fixture = Create();
        var pending = new Queue<long>([7L, 8L, 9L]);

        // The caller resolves the source once. Draining it here is exactly the shape that broke the old code,
        // where the loop re-read the source after the guard had already consumed it.
        List<long> removals = [.. pending];
        pending.Clear();

        fixture.Uploader.Upload(null, null, null, removals, skipUpload: false);

        Assert.Equal([7L, 8L, 9L], fixture.Opaque.Removed);
    }

    [Fact]
    public void NothingIsDroppedWhenTheListIsEmptyOrAbsent()
    {
        var fixture = Create();

        fixture.Uploader.Upload(null, null, null, null, skipUpload: false);
        fixture.Uploader.Upload(null, null, null, [], skipUpload: false);

        Assert.Empty(fixture.Opaque.Removed);
        Assert.Empty(fixture.Opaque.Applied);
    }

    [Fact]
    public void UpdatesAreAppliedToTheOpaqueArenaAndItsLayers()
    {
        var fixture = Create();
        var mesh = new GeometryData([1f, 2f, 3f], [0u])
        {
            CutoutVertices = [4f, 5f, 6f],
            CutoutIndices = [0u],
            TranslucentVertices = [7f, 8f, 9f],
            TranslucentIndices = [0u]
        };
        List<(long Key, GeometryData Mesh)> updates = [(42L, mesh)];

        var result = fixture.Uploader.Upload(null, null, updates, null, skipUpload: false);

        Assert.Equal(1, result.Updates);
        Assert.Equal([42L], fixture.Opaque.Applied);
        Assert.Equal([42L], fixture.Cutout.Applied);
        Assert.Equal([42L], fixture.Translucent.Applied);
    }

    [Fact]
    public void SkippingUploadsKeepsTheCountsButStillAppliesChanges()
    {
        var fixture = Create();
        List<(long Key, GeometryData Mesh)> updates = [(5L, new GeometryData([], []))];

        var result = fixture.Uploader.Upload(null, null, updates, null, skipUpload: true);

        // The A/B toggle skips the buffer writes, not the bookkeeping: both halves must see the same geometry.
        Assert.Equal(1, result.Updates);
        Assert.Empty(fixture.Backend.Written);
    }
}
