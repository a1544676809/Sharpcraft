// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 MinecraftCSharp contributors

using System.Buffers.Binary;
using System.Text;

namespace MinecraftCSharp.Common.Nbt;

/// <summary>
/// NBT writer producing the network flavour (unnamed root). Round trips with
/// <see cref="NbtReader"/> are covered by tests and used to build fixtures.
/// </summary>
public static class NbtWriter
{
    /// <summary>Writes a compound as a network root (no name).</summary>
    public static byte[] WriteNetworkRoot(NbtCompound root)
    {
        ArgumentNullException.ThrowIfNull(root);

        using var stream = new MemoryStream();
        WriteTagType(stream, NbtTagType.Compound, root);
        return stream.ToArray();
    }

    /// <summary>Writes a named root compound (on-disk flavour).</summary>
    public static byte[] WriteNamedRoot(string name, NbtCompound root)
    {
        ArgumentNullException.ThrowIfNull(root);

        using var stream = new MemoryStream();
        stream.WriteByte((byte)NbtTagType.Compound);
        WriteString(stream, name);
        WriteCompoundBody(stream, root);
        return stream.ToArray();
    }

    private static void WriteTagType(Stream stream, NbtTagType type, NbtTag tag)
    {
        switch (tag)
        {
            case NbtCompound compound:
                stream.WriteByte((byte)NbtTagType.Compound);
                WriteCompoundBody(stream, compound);
                break;

            default:
                throw new ArgumentException($"root must be a compound, got {type}", nameof(tag));
        }
    }

    private static void WriteCompoundBody(Stream stream, NbtCompound compound)
    {
        foreach (var name in compound.Names)
        {
            var child = compound.Get(name)!;
            stream.WriteByte((byte)child.Type);
            WriteString(stream, name);
            WritePayload(stream, child);
        }

        stream.WriteByte((byte)NbtTagType.End);
    }

    private static void WritePayload(Stream stream, NbtTag tag)
    {
        switch (tag)
        {
            case NbtByte value:
                stream.WriteByte(unchecked((byte)value.Value));
                break;

            case NbtShort value:
                WriteInt16(stream, value.Value);
                break;

            case NbtInt value:
                WriteInt32(stream, value.Value);
                break;

            case NbtLong value:
                WriteInt64(stream, value.Value);
                break;

            case NbtFloat value:
                WriteInt32(stream, BitConverter.SingleToInt32Bits(value.Value));
                break;

            case NbtDouble value:
                WriteInt64(stream, BitConverter.DoubleToInt64Bits(value.Value));
                break;

            case NbtString value:
                WriteString(stream, value.Value);
                break;

            case NbtByteArray value:
                WriteInt32(stream, value.Value.Length);
                stream.Write(value.Value);
                break;

            case NbtIntArray value:
                WriteInt32(stream, value.Value.Length);
                foreach (var item in value.Value)
                {
                    WriteInt32(stream, item);
                }

                break;

            case NbtLongArray value:
                WriteInt32(stream, value.Value.Length);
                foreach (var item in value.Value)
                {
                    WriteInt64(stream, item);
                }

                break;

            case NbtList value:
                stream.WriteByte((byte)value.ElementType);
                WriteInt32(stream, value.Items.Count);
                foreach (var item in value.Items)
                {
                    WritePayload(stream, item);
                }

                break;

            case NbtCompound value:
                WriteCompoundBody(stream, value);
                break;

            default:
                throw new ArgumentException($"unsupported tag {tag.Type}", nameof(tag));
        }
    }

    private static void WriteString(Stream stream, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        if (bytes.Length > ushort.MaxValue)
        {
            throw new ArgumentException($"NBT string too long ({bytes.Length} bytes)", nameof(value));
        }

        WriteUInt16(stream, (ushort)bytes.Length);
        stream.Write(bytes);
    }

    private static void WriteUInt16(Stream stream, ushort value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(ushort)];
        BinaryPrimitives.WriteUInt16BigEndian(buffer, value);
        stream.Write(buffer);
    }

    private static void WriteInt16(Stream stream, short value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(short)];
        BinaryPrimitives.WriteInt16BigEndian(buffer, value);
        stream.Write(buffer);
    }

    private static void WriteInt32(Stream stream, int value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(buffer, value);
        stream.Write(buffer);
    }

    private static void WriteInt64(Stream stream, long value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(buffer, value);
        stream.Write(buffer);
    }
}
