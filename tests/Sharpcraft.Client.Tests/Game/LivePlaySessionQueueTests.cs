// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System;
using Sharpcraft.Client.Game;
using Xunit;

namespace Sharpcraft.Client.Tests.Game;

/// <summary>
/// Bounded receive-queue and fault-surfacing tests for <see cref="LivePlaySession"/> (T-668). The queue is exercised
/// directly; the socket-backed <see cref="LivePlaySession"/> constructor needs a real <c>MinecraftConnection</c>, so the
/// end-to-end pump path is not instantiated here (only its shared message/fault formatting is covered).
/// </summary>
public sealed class LivePlaySessionQueueTests
{
    [Fact]
    public void EnqueueAndDequeueTrackBufferedBytes()
    {
        var queue = new BoundedReceiveQueue(maxBufferedBytes: 1024);

        Assert.True(queue.TryEnqueue(new byte[10]));
        Assert.True(queue.TryEnqueue(new byte[20]));
        Assert.Equal(30L, queue.BufferedBytes);

        Assert.True(queue.TryDequeue(out var first));
        Assert.Equal(10, first.Length);
        Assert.Equal(20L, queue.BufferedBytes);
        Assert.False(queue.Overflowed);
    }

    [Fact]
    public void OverflowRejectsFurtherEnqueuesAndRecordsReason()
    {
        var queue = new BoundedReceiveQueue(maxBufferedBytes: 30);

        Assert.True(queue.TryEnqueue(new byte[20]));
        Assert.False(queue.TryEnqueue(new byte[20])); // 40 > 30 cap
        Assert.True(queue.Overflowed);
        Assert.Contains("receive queue exceeded", queue.OverflowReason);

        // Sticky: further normal enqueues stay rejected and the byte count is unchanged.
        Assert.False(queue.TryEnqueue(new byte[1]));
        Assert.Equal(20L, queue.BufferedBytes);
    }

    [Fact]
    public void DrainingAllBuffersReturnsByteCountToZero()
    {
        var queue = new BoundedReceiveQueue(maxBufferedBytes: 1024);
        Assert.True(queue.TryEnqueue(new byte[5]));
        Assert.True(queue.TryEnqueue(new byte[7]));
        Assert.True(queue.TryEnqueue(new byte[9]));

        Assert.True(queue.TryDequeue(out _));
        Assert.True(queue.TryDequeue(out _));
        Assert.True(queue.TryDequeue(out _));
        Assert.Equal(0L, queue.BufferedBytes);
        Assert.False(queue.TryDequeue(out _));
    }

    [Fact]
    public void DefaultCapIsSixtyFourMiB()
    {
        Assert.Equal(64L * 1024 * 1024, BoundedReceiveQueue.MaxBufferedBytes);

        var queue = new BoundedReceiveQueue();
        Assert.Equal(BoundedReceiveQueue.MaxBufferedBytes, queue.MaxBytes);
        Assert.False(queue.Overflowed);
    }

    [Fact]
    public void OverflowMessageAndPumpFailureFormattingAreSurfaced()
    {
        Assert.Equal("receive queue exceeded 64 MiB", LivePlaySession.QueueOverflowMessage);

        var text = LivePlaySession.DescribePumpFailure(new InvalidOperationException("bad packet"));
        Assert.Contains("receive pump failed", text);
        Assert.Contains("InvalidOperationException", text);
        Assert.Contains("bad packet", text);
    }
}
