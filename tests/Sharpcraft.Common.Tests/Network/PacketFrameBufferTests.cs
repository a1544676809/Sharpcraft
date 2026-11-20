// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Network;
using Xunit;

namespace Sharpcraft.Common.Tests.Network;

public sealed class PacketFrameBufferTests
{
    [Fact]
    public void SingleFrameSplitAcrossMultipleAppendsDecodesOnceComplete()
    {
        var buffer = new PacketFrameBuffer(initialCapacity: 16);
        var payload = new byte[] { 1, 2, 3, 4, 5, 6, 7 };
        var frame = PacketFrameBuffer.Wrap(payload);

        buffer.Append(frame.AsSpan(0, 2));
        Assert.Equal(DecodeStatus.NeedMoreData, buffer.TryReadFrame(out _));

        buffer.Append(frame.AsSpan(2, 3));
        Assert.Equal(DecodeStatus.NeedMoreData, buffer.TryReadFrame(out _));

        buffer.Append(frame.AsSpan(5));
        Assert.Equal(DecodeStatus.Success, buffer.TryReadFrame(out var decoded));
        Assert.Equal(payload, decoded);
        Assert.Equal(0, buffer.BufferedBytes);
    }

    [Fact]
    public void EmptyFrameDecodesCleanly()
    {
        var buffer = new PacketFrameBuffer(initialCapacity: 8);
        var frame = PacketFrameBuffer.Wrap(ReadOnlySpan<byte>.Empty);

        buffer.Append(frame);
        Assert.Equal(DecodeStatus.Success, buffer.TryReadFrame(out var decoded));
        Assert.Empty(decoded);
        Assert.Equal(0, buffer.BufferedBytes);
    }

    [Fact]
    public void MultipleFramesInOneAppendAndSlidingCompactionPreserveAllBodies()
    {
        var buffer = new PacketFrameBuffer(initialCapacity: 12);
        var first = new byte[] { 10, 20, 30 };
        var second = new byte[] { 40, 50, 60, 70 };
        var third = new byte[200];
        for (var i = 0; i < third.Length; i++)
        {
            third[i] = (byte)(i & 0xFF);
        }

        var f1 = PacketFrameBuffer.Wrap(first);
        var f2 = PacketFrameBuffer.Wrap(second);
        var f3 = PacketFrameBuffer.Wrap(third);

        var combined = new byte[f1.Length + f2.Length];
        f1.CopyTo(combined, 0);
        f2.CopyTo(combined, f1.Length);

        buffer.Append(combined);
        Assert.Equal(DecodeStatus.Success, buffer.TryReadFrame(out var d1));
        Assert.Equal(first, d1);

        // Append a larger frame after advancing _readOffset to trigger compaction + growth.
        buffer.Append(f3);
        Assert.Equal(DecodeStatus.Success, buffer.TryReadFrame(out var d2));
        Assert.Equal(second, d2);

        Assert.Equal(DecodeStatus.Success, buffer.TryReadFrame(out var d3));
        Assert.Equal(third, d3);
        Assert.Equal(0, buffer.BufferedBytes);
    }
}
