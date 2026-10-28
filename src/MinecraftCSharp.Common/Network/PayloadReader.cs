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
