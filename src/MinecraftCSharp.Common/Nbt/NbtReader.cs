// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 MinecraftCSharp contributors

using System.Buffers.Binary;
using System.Text;

namespace MinecraftCSharp.Common.Nbt;

/// <summary>
/// NBT reader for the network flavour: the root tag carries no name
/// (docs/modules/nbt.md).
/// </summary>
/// <remarks>
/// Limits are enforced while reading, not after: a hostile length must fail before any
/// allocation proportional to it happens.
/// </remarks>
public static class NbtReader
{
    /// <summary>Maximum nesting depth vanilla accepts.</summary>
    public const int MaxDepth = 512;

    /// <summary>Largest array or list element count we accept.</summary>
    public const int MaxArrayLength = 1 << 24;

    /// <summary>Parses a network NBT root compound.</summary>
    /// <exception cref="InvalidDataException">The data is malformed or exceeds a limit.</exception>
    public static NbtCompound ReadNetworkRoot(ReadOnlySpan<byte> source)
    {
        var offset = 0;
        var type = (NbtTagType)source[offset];
        offset++;

        if (type != NbtTagType.Compound)
        {
            throw new InvalidDataException($"root tag must be a compound, got {type}");
        }

        var root = ReadCompound(source, ref offset, depth: 1);
        return root;
    }

    /// <summary>Parses a named root compound (the on-disk flavour).</summary>
    public static NbtCompound ReadNamedRoot(ReadOnlySpan<byte> source, out string name)
    {
        var offset = 0;
        var type = (NbtTagType)source[offset];
        offset++;

        if (type != NbtTagType.Compound)
        {
            throw new InvalidDataException($"root tag must be a compound, got {type}");
        }

        name = ReadString(source, ref offset);
        return ReadCompound(source, ref offset, depth: 1);
    }

    private static NbtCompound ReadCompound(ReadOnlySpan<byte> source, ref int offset, int depth)
    {
        if (depth > MaxDepth)
        {
            throw new InvalidDataException($"NBT nesting deeper than {MaxDepth}");
        }

        var compound = new NbtCompound();
        while (true)
        {
            var type = (NbtTagType)ReadByte(source, ref offset);
            if (type == NbtTagType.End)
            {
                return compound;
            }

            if (type is < NbtTagType.Byte or > NbtTagType.LongArray)
            {
                throw new InvalidDataException($"unknown tag type {(byte)type} at depth {depth}");
            }

            var name = ReadString(source, ref offset);
            compound.Set(name, ReadPayload(source, ref offset, type, depth));
        }
    }

    private static NbtTag ReadPayload(ReadOnlySpan<byte> source, ref int offset, NbtTagType type, int depth) =>
        type switch
        {
            NbtTagType.Byte => new NbtByte((sbyte)ReadByte(source, ref offset)),
            NbtTagType.Short => new NbtShort(ReadInt16(source, ref offset)),
            NbtTagType.Int => new NbtInt(ReadInt32(source, ref offset)),
            NbtTagType.Long => new NbtLong(ReadInt64(source, ref offset)),
            NbtTagType.Float => new NbtFloat(ReadSingle(source, ref offset)),
            NbtTagType.Double => new NbtDouble(ReadDouble(source, ref offset)),
            NbtTagType.String => new NbtString(ReadString(source, ref offset)),
            NbtTagType.ByteArray => ReadByteArray(source, ref offset),
            NbtTagType.IntArray => ReadIntArray(source, ref offset),
            NbtTagType.LongArray => ReadLongArray(source, ref offset),
            NbtTagType.List => ReadList(source, ref offset, depth),
            NbtTagType.Compound => ReadCompound(source, ref offset, depth + 1),
            _ => throw new InvalidDataException($"unexpected tag type {type}")
        };

    private static NbtList ReadList(ReadOnlySpan<byte> source, ref int offset, int depth)
    {
        var elementType = (NbtTagType)ReadByte(source, ref offset);
        var count = ReadInt32(source, ref offset);

        if (count < 0 || count > MaxArrayLength)
        {
            throw new InvalidDataException($"list length {count} is out of range");
        }

        if (elementType == NbtTagType.End && count > 0)
        {
            throw new InvalidDataException("non-empty list with end element type");
        }

        var items = new List<NbtTag>(Math.Min(count, 1024));
        for (var i = 0; i < count; i++)
        {
            items.Add(ReadPayload(source, ref offset, elementType, depth + 1));
        }

        return new NbtList(elementType, items);
    }

    private static NbtByteArray ReadByteArray(ReadOnlySpan<byte> source, ref int offset)
    {
        var count = ReadInt32(source, ref offset);
        var bytes = ReadBlock(source, ref offset, count);
        return new NbtByteArray(bytes);
    }

    private static NbtIntArray ReadIntArray(ReadOnlySpan<byte> source, ref int offset)
    {
        var count = ReadInt32(source, ref offset);
        var bytes = ReadBlock(source, ref offset, checked(count * sizeof(int)));
        var values = new int[count];
        for (var i = 0; i < count; i++)
        {
            values[i] = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(i * sizeof(int)));
        }

        return new NbtIntArray(values);
    }

    private static NbtLongArray ReadLongArray(ReadOnlySpan<byte> source, ref int offset)
    {
        var count = ReadInt32(source, ref offset);
        var bytes = ReadBlock(source, ref offset, checked(count * sizeof(long)));
        var values = new long[count];
        for (var i = 0; i < count; i++)
        {
            values[i] = BinaryPrimitives.ReadInt64BigEndian(bytes.AsSpan(i * sizeof(long)));
        }

        return new NbtLongArray(values);
    }

    private static byte[] ReadBlock(ReadOnlySpan<byte> source, ref int offset, int count)
    {
        if (count < 0 || count > MaxArrayLength * sizeof(long))
        {
            throw new InvalidDataException($"array length {count} is out of range");
        }

        if (offset + count > source.Length)
        {
            throw new InvalidDataException("truncated NBT array");
        }

        var result = source.Slice(offset, count).ToArray();
        offset += count;
        return result;
    }

    private static string ReadString(ReadOnlySpan<byte> source, ref int offset)
    {
        var length = ReadUInt16(source, ref offset);
        if (offset + length > source.Length)
        {
            throw new InvalidDataException("truncated NBT string");
        }

        var text = Encoding.UTF8.GetString(source.Slice(offset, length));
        offset += length;
        return text;
    }

    private static byte ReadByte(ReadOnlySpan<byte> source, ref int offset)
    {
        if (offset + 1 > source.Length)
        {
            throw new InvalidDataException("truncated NBT payload");
        }

        return source[offset++];
    }

    private static ushort ReadUInt16(ReadOnlySpan<byte> source, ref int offset)
    {
        Ensure(source, offset, sizeof(ushort));
        var value = BinaryPrimitives.ReadUInt16BigEndian(source[offset..]);
        offset += sizeof(ushort);
        return value;
    }

    private static short ReadInt16(ReadOnlySpan<byte> source, ref int offset)
    {
        Ensure(source, offset, sizeof(short));
        var value = BinaryPrimitives.ReadInt16BigEndian(source[offset..]);
        offset += sizeof(short);
        return value;
    }

    private static int ReadInt32(ReadOnlySpan<byte> source, ref int offset)
    {
        Ensure(source, offset, sizeof(int));
        var value = BinaryPrimitives.ReadInt32BigEndian(source[offset..]);
        offset += sizeof(int);
        return value;
    }

    private static long ReadInt64(ReadOnlySpan<byte> source, ref int offset)
    {
        Ensure(source, offset, sizeof(long));
        var value = BinaryPrimitives.ReadInt64BigEndian(source[offset..]);
        offset += sizeof(long);
        return value;
    }

    private static float ReadSingle(ReadOnlySpan<byte> source, ref int offset)
    {
        Ensure(source, offset, sizeof(float));
        var bits = BinaryPrimitives.ReadInt32BigEndian(source[offset..]);
        offset += sizeof(float);
        return BitConverter.Int32BitsToSingle(bits);
    }

    private static double ReadDouble(ReadOnlySpan<byte> source, ref int offset)
    {
        Ensure(source, offset, sizeof(double));
        var bits = BinaryPrimitives.ReadInt64BigEndian(source[offset..]);
        offset += sizeof(double);
        return BitConverter.Int64BitsToDouble(bits);
    }

    private static void Ensure(ReadOnlySpan<byte> source, int offset, int size)
    {
        if (offset + size > source.Length)
        {
            throw new InvalidDataException("truncated NBT payload");
        }
    }
}
