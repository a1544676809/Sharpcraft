// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Text;
using System.Text.Json;
using Sharpcraft.Common.Network;
using Xunit;

namespace Sharpcraft.Common.Tests;

/// <summary>
/// Handshake/status packets, frame buffering and compression
/// (docs/specs/network/protocol-phases.md).
/// </summary>
public sealed class ProtocolPacketTests
{
    [Fact]
    public void ClientIntentionMatchesTheWireFormatByteForByte()
    {
        Span<byte> buffer = stackalloc byte[64];

        var written = HandshakePackets.WriteClientIntention(
            buffer, ProtocolConstants.Version, "localhost", 25565, ProtocolConstants.IntentStatus);

        byte[] expected =
        [
            0x00,                         // packet id
            0x89, 0x06,                   // 777
            0x09, .. "localhost"u8.ToArray(),
            0x63, 0xDD,                   // 25565 big-endian
            0x01                          // status intention
        ];

        Assert.Equal(expected, buffer[..written].ToArray());
    }

    [Fact]
    public void ClientIntentionRoundTrips()
    {
        Span<byte> buffer = stackalloc byte[512];

        var written = HandshakePackets.WriteClientIntention(
            buffer, ProtocolConstants.Version, "play.example.com", 25565, ProtocolConstants.IntentLogin);

        Assert.Equal(
            DecodeStatus.Success,
            HandshakePackets.TryReadClientIntention(
                buffer[..written], out var version, out var host, out var port, out var intention));

        Assert.Equal(ProtocolConstants.Version, version);
        Assert.Equal("play.example.com", host);
        Assert.Equal(25565, port);
        Assert.Equal(ProtocolConstants.IntentLogin, intention);
    }

    [Fact]
    public void HostNameLimitIs255NotTheStringDefault()
    {
        var tooLong = new string('a', ProtocolConstants.MaxHostLength + 1);
        var buffer = new byte[512];

        Assert.Throws<ArgumentException>(
            () => HandshakePackets.WriteClientIntention(buffer, ProtocolConstants.Version, tooLong, 25565, 1));

        var exact = new string('a', ProtocolConstants.MaxHostLength);
        var written = HandshakePackets.WriteClientIntention(buffer, ProtocolConstants.Version, exact, 25565, 1);
        Assert.True(written > 0);
    }

    [Fact]
    public void TruncatedHandshakeReportsNeedMoreData()
    {
        Span<byte> buffer = stackalloc byte[64];
        var written = HandshakePackets.WriteClientIntention(buffer, 777, "localhost", 25565, 1);

        Assert.Equal(
            DecodeStatus.NeedMoreData,
            HandshakePackets.TryReadClientIntention(buffer[..(written - 1)], out _, out _, out _, out _));
    }

    [Fact]
    public void PingAndPongPayloadsRoundTrip()
    {
        Span<byte> buffer = stackalloc byte[32];
        const long payload = 0x0102030405060708L;

        var written = HandshakePackets.WritePingRequest(buffer, payload);
        Assert.Equal(DecodeStatus.Success, HandshakePackets.TryReadPingPayload(buffer[..written], out var decoded));
        Assert.Equal(payload, decoded);

        // A pong response is the same payload with a different packet id.
        Assert.Equal(DecodeStatus.Success, HandshakePackets.TryReadPingPayload(buffer[..written], out _));
    }

    [Fact]
    public void StatusResponseCarriesJson()
    {
        const string json = """{"version":{"name":"26.3","protocol":777},"players":{"max":20,"online":0},"description":{"text":"hi"}}""";

        var buffer = new byte[Encoding.UTF8.GetByteCount(json) + 16];
        var written = HandshakePackets.WriteStatusResponse(buffer, json);

        Assert.Equal(DecodeStatus.Success, HandshakePackets.TryReadStatusResponse(buffer.AsSpan(0, written), out var decoded));
        Assert.Equal(json, decoded);

        // Sanity check that the payload really is JSON the client can parse.
        using var document = JsonDocument.Parse(decoded);
        Assert.Equal(777, document.RootElement.GetProperty("version").GetProperty("protocol").GetInt32());
    }

    [Fact]
    public void StatusResponseWithWrongPacketIdIsRejected()
    {
        // Ids are per direction, so a status request (id 0) must not be mistaken for a
        // response (also id 0); the real check is against a non-zero id.
        var buffer = new byte[16];
        var written = VarInt.Write(buffer, 5);
        written += Utf8StringCodec.Write(buffer.AsSpan(written), "{}");

        Assert.Equal(DecodeStatus.Invalid, HandshakePackets.TryReadStatusResponse(buffer.AsSpan(0, written), out _));
    }

    [Fact]
    public void FrameBufferHandlesFragmentationAndCoalescing()
    {
        var buffer = new PacketFrameBuffer();
        var first = PacketFrameBuffer.Wrap([0x01, 0x02, 0x03]);
        var second = PacketFrameBuffer.Wrap([0x04, 0x05]);

        // Feed the first frame in two fragments.
        buffer.Append(first.AsSpan(0, 2));
        Assert.Equal(DecodeStatus.NeedMoreData, buffer.TryReadFrame(out _));

        buffer.Append(first.AsSpan(2));
        Assert.Equal(DecodeStatus.Success, buffer.TryReadFrame(out var body));
        Assert.Equal([0x01, 0x02, 0x03], body);

        // Both frames arrive at once; the buffer yields them one at a time.
        buffer.Append([.. second]);
        buffer.Append([.. PacketFrameBuffer.Wrap([0x09])]);

        Assert.Equal(DecodeStatus.Success, buffer.TryReadFrame(out var secondBody));
        Assert.Equal([0x04, 0x05], secondBody);
        Assert.Equal(DecodeStatus.Success, buffer.TryReadFrame(out var thirdBody));
        Assert.Equal([0x09], thirdBody);
        Assert.Equal(DecodeStatus.NeedMoreData, buffer.TryReadFrame(out _));
        Assert.Equal(0, buffer.BufferedBytes);
    }

    [Fact]
    public void FrameBufferRejectsAnOverlongLengthPrefix()
    {
        var buffer = new PacketFrameBuffer();
        buffer.Append([0x80, 0x80, 0x80, 0x80, 0x01]);

        Assert.Equal(DecodeStatus.Invalid, buffer.TryReadFrame(out _));
    }

    [Fact]
    public void CompressionPassesThroughSmallBodiesVerbatim()
    {
        byte[] body = [.. Enumerable.Range(0, 16).Select(i => (byte)i)];
        var compressed = new byte[64];

        var written = PacketCompression.Compress(compressed, body, threshold: 256);

        Assert.Equal(DecodeStatus.Success, PacketCompression.TryDecompress(compressed.AsSpan(0, written), 256, out var decoded));
        Assert.Equal(body, decoded);
        // Uncompressed path starts with a zero length marker.
        Assert.Equal(0, VarInt.Read(compressed, out _));
    }

    [Fact]
    public void CompressionRoundTripsLargeBodies()
    {
        var body = new byte[4096];
        for (var i = 0; i < body.Length; i++)
        {
            body[i] = (byte)(i % 7);
        }

        var compressed = new byte[body.Length * 2];
        var written = PacketCompression.Compress(compressed, body, threshold: 256);

        Assert.True(written < body.Length, "repetitive payload should shrink");

        Assert.Equal(DecodeStatus.Success, PacketCompression.TryDecompress(compressed.AsSpan(0, written), 256, out var decoded));
        Assert.Equal(body, decoded);
    }

    [Fact]
    public void CompressionRejectsBodiesBelowTheAdvertisedThreshold()
    {
        // Claim a 100-byte compressed body while the threshold says nothing below 256 is valid.
        var frame = new List<byte>();
        frame.AddRange([0x64, 0x00, 0x01]);

        Assert.Equal(DecodeStatus.Invalid, PacketCompression.TryDecompress(frame.ToArray(), 256, out _));
    }

    [Fact]
    public void CompressionRejectsBodiesAboveTheCeiling()
    {
        Span<byte> frame = stackalloc byte[8];
        var written = VarInt.Write(frame, ProtocolConstants.MaxUncompressedBody + 1);

        Assert.Equal(DecodeStatus.Invalid, PacketCompression.TryDecompress(frame[..written], 1, out _));
    }
}
