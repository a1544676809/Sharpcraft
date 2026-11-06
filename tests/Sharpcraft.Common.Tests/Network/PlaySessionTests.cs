// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Buffers.Binary;
using Sharpcraft.Common.Network;
using Xunit;

namespace Sharpcraft.Common.Tests.Network;

/// <summary>
/// Verifies the shared transport-reply path answers keep-alive, ping and chunk-batch-finished and bumps the
/// matching counters (docs/specs/network/protocol-phases.md; AGENTS.md principle 14 single dispatcher).
/// </summary>
public sealed class PlaySessionTests
{
    [Fact]
    public void TryHandleTransportAnswersKeepAlivePingAndChunkBatchFinished()
    {
        var sent = new List<byte[]>();
        var keepAlives = 0;
        var pings = 0;
        var batches = 0;

        // KeepAlive payload: the server's id as a big-endian long after the packet id.
        Span<byte> keepAliveBody = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(keepAliveBody, 0x0102030405060708L);
        var keepAliveReader = new PayloadReader(keepAliveBody);
        Assert.True(PlaySession.TryHandleTransport(
            PacketIds.PlayClientbound.KeepAlive,
            ref keepAliveReader,
            sent.Add,
            ref keepAlives,
            ref pings,
            ref batches));

        // Ping payload: the server's id as a big-endian int.
        Span<byte> pingBody = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(pingBody, 4242);
        var pingReader = new PayloadReader(pingBody);
        Assert.True(PlaySession.TryHandleTransport(
            PacketIds.PlayClientbound.Ping,
            ref pingReader,
            sent.Add,
            ref keepAlives,
            ref pings,
            ref batches));

        // ChunkBatchFinished has no payload; it only asks for another batch.
        var batchReader = new PayloadReader(ReadOnlySpan<byte>.Empty);
        Assert.True(PlaySession.TryHandleTransport(
            PacketIds.PlayClientbound.ChunkBatchFinished,
            ref batchReader,
            sent.Add,
            ref keepAlives,
            ref pings,
            ref batches));

        Assert.Equal(1, keepAlives);
        Assert.Equal(1, pings);
        Assert.Equal(1, batches);
        Assert.Equal(3, sent.Count);

        var keepAliveReply = new PayloadReader(sent[0]);
        Assert.Equal(DecodeStatus.Success, keepAliveReply.TryReadVarInt(out var keepAliveReplyId));
        Assert.Equal(PacketIds.PlayServerbound.KeepAlive, keepAliveReplyId);
        Assert.Equal(DecodeStatus.Success, keepAliveReply.TryReadLong(out var echoedKeepAlive));
        Assert.Equal(0x0102030405060708L, echoedKeepAlive);

        var pongReply = new PayloadReader(sent[1]);
        Assert.Equal(DecodeStatus.Success, pongReply.TryReadVarInt(out var pongReplyId));
        Assert.Equal(PacketIds.PlayServerbound.Pong, pongReplyId);
        Assert.Equal(DecodeStatus.Success, pongReply.TryReadInt(out var echoedPing));
        Assert.Equal(4242, echoedPing);

        var batchReply = new PayloadReader(sent[2]);
        Assert.Equal(DecodeStatus.Success, batchReply.TryReadVarInt(out var batchReplyId));
        Assert.Equal(PacketIds.PlayServerbound.ChunkBatchReceived, batchReplyId);
        Assert.Equal(DecodeStatus.Success, batchReply.TryReadFloat(out var desiredChunks));
        Assert.Equal(PlaySession.DesiredChunksPerTick, desiredChunks);

        // An unrelated id is reported as not handled so the caller's own switch keeps it.
        var otherReader = new PayloadReader(ReadOnlySpan<byte>.Empty);
        Assert.False(PlaySession.TryHandleTransport(
            PacketIds.PlayClientbound.SetTime,
            ref otherReader,
            sent.Add,
            ref keepAlives,
            ref pings,
            ref batches));
        Assert.Equal(3, sent.Count);
    }
}
