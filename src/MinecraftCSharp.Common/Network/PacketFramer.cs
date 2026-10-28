// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 MinecraftCSharp contributors

namespace MinecraftCSharp.Common.Network;

/// <summary>
/// Packet framing: a VarInt body length followed by the body, with the length prefix
/// capped at three bytes (docs/specs/network/primitives-varint.md).
/// </summary>
public static class PacketFramer
{
    /// <summary>Maximum bytes used by the length prefix.</summary>
    public const int MaxPrefixBytes = 3;

    /// <summary>Largest body length the three-byte prefix can describe.</summary>
    public const int MaxBodyLength = (1 << (MaxPrefixBytes * 7)) - 1;

    /// <summary>Writes the length prefix for a body of <paramref name="bodyLength"/> bytes.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The body is larger than <see cref="MaxBodyLength"/>.</exception>
    public static int WriteLengthPrefix(Span<byte> destination, int bodyLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(bodyLength);

        if (bodyLength > MaxBodyLength)
        {
            throw new ArgumentOutOfRangeException(nameof(bodyLength), bodyLength, "packet too large for a 3-byte length prefix");
        }

        return VarInt.Write(destination, bodyLength);
    }

    /// <summary>Attempts to read a length prefix; reports the body length to expect.</summary>
    public static DecodeStatus TryReadLengthPrefix(ReadOnlySpan<byte> source, out int bodyLength, out int consumed)
    {
        var status = VarInt.TryRead(source, out bodyLength, out consumed);
        if (status != DecodeStatus.Success)
        {
            return status;
        }

        if (consumed > MaxPrefixBytes || bodyLength > MaxBodyLength)
        {
            bodyLength = 0;
            consumed = 0;
            return DecodeStatus.Invalid;
        }

        return DecodeStatus.Success;
    }
}
