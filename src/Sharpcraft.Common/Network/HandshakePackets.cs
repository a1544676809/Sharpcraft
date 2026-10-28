// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Common.Network;

/// <summary>
/// Handshake and status packets: encoding and decoding for the first two connection
/// phases (docs/specs/network/protocol-phases.md 3-4).
/// </summary>
/// <remarks>
/// Every body starts with the packet id as a VarInt, followed by the payload. Buffer
/// sizing is the caller's job; encoders return the number of bytes written.
/// </remarks>
public static class HandshakePackets
{
    /// <summary>Encodes the client intention packet body (id + payload).</summary>
    /// <exception cref="ArgumentException">The destination is too small or the host is too long.</exception>
    public static int WriteClientIntention(
        Span<byte> destination,
        int protocolVersion,
        string host,
        ushort port,
        int intention)
    {
        var writer = new PayloadWriter(destination);
        writer.WriteVarInt(PacketIds.HandshakeServerbound.ClientIntention);
        writer.WriteVarInt(protocolVersion);
        writer.WriteString(host, ProtocolConstants.MaxHostLength);
        writer.WriteUnsignedShort(port);
        writer.WriteVarInt(intention);
        return writer.Position;
    }

    /// <summary>Decodes a client intention packet body.</summary>
    public static DecodeStatus TryReadClientIntention(
        ReadOnlySpan<byte> body,
        out int protocolVersion,
        out string host,
        out ushort port,
        out int intention)
    {
        protocolVersion = 0;
        host = string.Empty;
        port = 0;
        intention = 0;

        var reader = new PayloadReader(body);
        if (reader.TryReadVarInt(out var id) != DecodeStatus.Success || id != PacketIds.HandshakeServerbound.ClientIntention)
        {
            return DecodeStatus.Invalid;
        }

        if (reader.TryReadVarInt(out protocolVersion) != DecodeStatus.Success ||
            reader.TryReadString(out host, ProtocolConstants.MaxHostLength) != DecodeStatus.Success ||
            reader.TryReadUnsignedShort(out port) != DecodeStatus.Success ||
            reader.TryReadVarInt(out intention) != DecodeStatus.Success)
        {
            return DecodeStatus.NeedMoreData;
        }

        return DecodeStatus.Success;
    }

    /// <summary>Encodes an empty status request body (id only).</summary>
    public static int WriteStatusRequest(Span<byte> destination)
    {
        var writer = new PayloadWriter(destination);
        writer.WriteVarInt(PacketIds.StatusServerbound.StatusRequest);
        return writer.Position;
    }

    /// <summary>Encodes a ping request body.</summary>
    public static int WritePingRequest(Span<byte> destination, long payload)
    {
        var writer = new PayloadWriter(destination);
        writer.WriteVarInt(PacketIds.StatusServerbound.PingRequest);
        writer.WriteLong(payload);
        return writer.Position;
    }

    /// <summary>Decodes a ping request (or pong) payload, ignoring the packet id.</summary>
    public static DecodeStatus TryReadPingPayload(ReadOnlySpan<byte> body, out long payload)
    {
        payload = 0;

        var reader = new PayloadReader(body);
        if (reader.TryReadVarInt(out _) != DecodeStatus.Success)
        {
            return DecodeStatus.NeedMoreData;
        }

        return reader.TryReadLong(out payload) == DecodeStatus.Success
            ? DecodeStatus.Success
            : DecodeStatus.NeedMoreData;
    }

    /// <summary>Encodes a status response body with its JSON document.</summary>
    public static int WriteStatusResponse(Span<byte> destination, string json)
    {
        var writer = new PayloadWriter(destination);
        writer.WriteVarInt(PacketIds.StatusClientbound.StatusResponse);
        writer.WriteString(json, 32767);
        return writer.Position;
    }

    /// <summary>Decodes a status response body into its JSON document.</summary>
    public static DecodeStatus TryReadStatusResponse(ReadOnlySpan<byte> body, out string json)
    {
        json = string.Empty;

        var reader = new PayloadReader(body);
        if (reader.TryReadVarInt(out var id) != DecodeStatus.Success ||
            id != PacketIds.StatusClientbound.StatusResponse)
        {
            return DecodeStatus.Invalid;
        }

        return reader.TryReadString(out json, 32767);
    }
}
