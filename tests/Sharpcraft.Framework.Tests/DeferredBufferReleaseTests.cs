// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Framework.Tests;

/// <summary>
/// Buffers replaced mid-frame outlive the frames that could still be reading them (T-837).
/// </summary>
public sealed class DeferredBufferReleaseTests
{
    [Fact]
    public void AReleaseWaitsForTheFramesInFlightToFinish()
    {
        var destroyed = 0;
        var release = new DeferredBufferRelease(_ => destroyed++);

        release.BeginFrame();
        release.Release(default);
        Assert.Equal(1, release.PendingCount);

        // The frame that asked for it has not finished yet.
        release.BeginFrame();
        release.Collect();
        Assert.Equal(1, release.PendingCount);
        Assert.Equal(0, destroyed);

        // One more frame means the submission that could still read it has completed.
        release.BeginFrame();
        release.Collect();
        Assert.Equal(0, release.PendingCount);
        Assert.Equal(1, destroyed);
    }

    [Fact]
    public void CollectingDestroysOnlyWhatIsFinished()
    {
        var destroyed = 0;
        var release = new DeferredBufferRelease(_ => destroyed++);

        release.BeginFrame();
        release.Release(default);

        release.BeginFrame();
        release.Release(default);
        release.Release(default);

        release.BeginFrame();
        release.Collect();

        Assert.Equal(1, destroyed);
        Assert.Equal(2, release.PendingCount);
    }

    [Fact]
    public void NothingIsDestroyedWhileOnlyOneFrameHasBegun()
    {
        var destroyed = 0;
        var release = new DeferredBufferRelease(_ => destroyed++);

        release.BeginFrame();
        release.Release(default);
        release.Collect();

        Assert.Equal(0, destroyed);
        Assert.Equal(1, release.PendingCount);
    }
}
