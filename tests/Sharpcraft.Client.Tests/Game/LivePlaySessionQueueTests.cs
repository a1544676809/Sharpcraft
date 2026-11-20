// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sharpcraft.Client.Game;
using Sharpcraft.Common.Network;
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

    [Fact]
    public async Task OutboundPacketChannelPreservesOrderAndNeverTearsFramesAcrossConcurrentWriters()
    {
        var channel = new OutboundPacketChannel();
        var writtenFrames = new ConcurrentQueue<byte[]>();
        var flushCount = 0;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var drainTask = channel.DrainAsync(
            frame => writtenFrames.Enqueue(PacketFrameBuffer.Wrap(frame)),
            () => Interlocked.Increment(ref flushCount),
            cts.Token);

        // Two concurrent producers (simulating game thread + pump keep-alive replies).
        var gameThreadTask = Task.Run(() =>
        {
            for (var i = 0; i < 50; i++)
            {
                Assert.True(channel.TryEnqueue([(byte)1, (byte)i]));
            }
        });

        var pumpThreadTask = Task.Run(() =>
        {
            for (var i = 0; i < 50; i++)
            {
                Assert.True(channel.TryEnqueue([(byte)2, (byte)i]));
            }
        });

        await Task.WhenAll(gameThreadTask, pumpThreadTask);
        channel.Complete();
        await drainTask;

        Assert.Equal(100, writtenFrames.Count);
        Assert.True(flushCount >= 1);

        // Every frame must decode cleanly without tearing, and each producer's sequence must be monotonic.
        var lastGameSeq = -1;
        var lastPumpSeq = -1;
        var frameBuffer = new PacketFrameBuffer();
        foreach (var raw in writtenFrames)
        {
            frameBuffer.Append(raw);
            Assert.Equal(DecodeStatus.Success, frameBuffer.TryReadFrame(out var body));
            Assert.Equal(2, body.Length);
            if (body[0] == 1)
            {
                Assert.Equal(lastGameSeq + 1, body[1]);
                lastGameSeq = body[1];
            }
            else
            {
                Assert.Equal(2, body[0]);
                Assert.Equal(lastPumpSeq + 1, body[1]);
                lastPumpSeq = body[1];
            }
        }

        Assert.Equal(49, lastGameSeq);
        Assert.Equal(49, lastPumpSeq);
    }
}
