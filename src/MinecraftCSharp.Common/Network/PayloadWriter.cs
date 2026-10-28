// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 MinecraftCSharp contributors

using System.Buffers.Binary;

namespace MinecraftCSharp.Common.Network;

/// <summary>
/// Writes primitive payload fields into a caller-owned buffer
/// (docs/specs/network/primitives-varint.md, primitives-strings.md).
/// </summary>
public ref struct PayloadWriter
{
    private readonly Span<byte> _buffer;

    /// <summary>Creates a writer over <paramref name="buffer"/>.</summary>
    public PayloadWriter(Span<byte> buffer)
    {
        _buffer = buffer;
        Position = 0;
    }

    /// <summary>Bytes written so far.</summary>
    public int Position { get; private set; }

    private Span<byte> Remaining => _buffer[Position..];

    /// <summary>Writes a VarInt.</summary>
    public void WriteVarInt(int value) => Position += VarInt.Write(Remaining, value);

    /// <summary>Writes a VarLong.</summary>
    public void WriteVarLong(long value) => Position += VarLong.Write(Remaining, value);

    /// <summary>Writes a length-prefixed UTF-8 string.</summary>
    public void WriteString(string value, int maxLength = Utf8StringCodec.DefaultMaxLength) =>
        Position += Utf8StringCodec.Write(Remaining, value, maxLength);

    /// <summary>Writes a big-endian unsigned short.</summary>
    public void WriteUnsignedShort(ushort value)
    {
        BinaryPrimitives.WriteUInt16BigEndian(Remaining, value);
        Position += sizeof(ushort);
    }

    /// <summary>Writes a big-endian signed 64-bit value.</summary>
    public void WriteLong(long value)
    {
        BinaryPrimitives.WriteInt64BigEndian(Remaining, value);
        Position += sizeof(long);
    }

    /// <summary>Writes a single byte boolean.</summary>
    public void WriteBool(bool value)
    {
        Remaining[0] = value ? (byte)1 : (byte)0;
        Position++;
    }
}
