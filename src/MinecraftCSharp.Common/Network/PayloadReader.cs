// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 MinecraftCSharp contributors

using System.Buffers.Binary;

namespace MinecraftCSharp.Common.Network;

/// <summary>
/// Reads primitive payload fields from a buffer, reporting truncation as
/// <see cref="DecodeStatus.NeedMoreData"/> instead of throwing.
/// </summary>
public ref struct PayloadReader
{
    private readonly ReadOnlySpan<byte> _buffer;

    /// <summary>Creates a reader over <paramref name="buffer"/>.</summary>
    public PayloadReader(ReadOnlySpan<byte> buffer)
    {
        _buffer = buffer;
        Position = 0;
    }

    /// <summary>Bytes consumed so far.</summary>
    public int Position { get; private set; }

    private ReadOnlySpan<byte> Remaining => _buffer[Position..];

    /// <summary>Reads a VarInt.</summary>
    public DecodeStatus TryReadVarInt(out int value)
    {
        var status = VarInt.TryRead(Remaining, out value, out var consumed);
        if (status == DecodeStatus.Success)
        {
            Position += consumed;
        }

        return status;
    }

    /// <summary>Reads a VarLong.</summary>
    public DecodeStatus TryReadVarLong(out long value)
    {
        var status = VarLong.TryRead(Remaining, out value, out var consumed);
        if (status == DecodeStatus.Success)
        {
            Position += consumed;
        }

        return status;
    }

    /// <summary>Reads a length-prefixed UTF-8 string.</summary>
    public DecodeStatus TryReadString(out string value, int maxLength = Utf8StringCodec.DefaultMaxLength)
    {
        var status = Utf8StringCodec.TryRead(Remaining, out value, out var consumed, maxLength);
        if (status == DecodeStatus.Success)
        {
            Position += consumed;
        }

        return status;
    }

    /// <summary>Reads a VarInt-prefixed byte array, rejecting lengths above <paramref name="maxLength"/>.</summary>
    public DecodeStatus TryReadByteArray(out byte[] value, int maxLength)
    {
        value = [];

        var status = VarInt.TryRead(Remaining, out var length, out var prefixLength);
        if (status != DecodeStatus.Success)
        {
            return status;
        }

        if (length < 0 || length > maxLength)
        {
            return DecodeStatus.Invalid;
        }

        if (Remaining.Length - prefixLength < length)
        {
            return DecodeStatus.NeedMoreData;
        }

        value = Remaining.Slice(prefixLength, length).ToArray();
        Position += prefixLength + length;
        return DecodeStatus.Success;
    }

    /// <summary>Reads a UUID encoded as 16 raw big-endian bytes (no length prefix).</summary>
    public DecodeStatus TryReadUuid(out Guid value)
    {
        value = Guid.Empty;

        if (Remaining.Length < 16)
        {
            return DecodeStatus.NeedMoreData;
        }

        value = new Guid(Remaining[..16], bigEndian: true);
        Position += 16;
        return DecodeStatus.Success;
    }

    /// <summary>Reads a big-endian unsigned short.</summary>
    public DecodeStatus TryReadUnsignedShort(out ushort value)
    {
        if (Remaining.Length < sizeof(ushort))
        {
            value = 0;
            return DecodeStatus.NeedMoreData;
        }

        value = BinaryPrimitives.ReadUInt16BigEndian(Remaining);
        Position += sizeof(ushort);
        return DecodeStatus.Success;
    }

    /// <summary>Reads a big-endian signed 64-bit value.</summary>
    public DecodeStatus TryReadLong(out long value)
    {
        if (Remaining.Length < sizeof(long))
        {
            value = 0;
            return DecodeStatus.NeedMoreData;
        }

        value = BinaryPrimitives.ReadInt64BigEndian(Remaining);
        Position += sizeof(long);
        return DecodeStatus.Success;
    }

    /// <summary>Reads a single byte boolean.</summary>
    public DecodeStatus TryReadBool(out bool value)
    {
        if (Remaining.Length < 1)
        {
            value = false;
            return DecodeStatus.NeedMoreData;
        }

        value = Remaining[0] != 0;
        Position++;
        return DecodeStatus.Success;
    }
}
