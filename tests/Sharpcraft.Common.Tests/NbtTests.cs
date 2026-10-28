// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Nbt;
using Xunit;

namespace Sharpcraft.Common.Tests;

/// <summary>
/// NBT round trips, limits and malformed input (docs/modules/nbt.md).
/// </summary>
public sealed class NbtTests
{
    [Fact]
    public void AllPrimitiveTypesRoundTrip()
    {
        var root = new NbtCompound()
            .Set("byte", new NbtByte(-42))
            .Set("short", new NbtShort(-1234))
            .Set("int", new NbtInt(int.MinValue))
            .Set("long", new NbtLong(long.MaxValue))
            .Set("float", new NbtFloat(1.5f))
            .Set("double", new NbtDouble(-2.25))
            .Set("string", new NbtString("hello \u4e16\u754c"));

        var parsed = NbtReader.ReadNetworkRoot(NbtWriter.WriteNetworkRoot(root));

        Assert.Equal(-42, ((NbtByte)parsed.Get("byte")!).Value);
        Assert.Equal(-1234, ((NbtShort)parsed.Get("short")!).Value);
        Assert.Equal(int.MinValue, parsed.GetInt("int"));
        Assert.Equal(long.MaxValue, parsed.GetLong("long"));
        Assert.Equal(1.5f, ((NbtFloat)parsed.Get("float")!).Value);
        Assert.Equal(-2.25, ((NbtDouble)parsed.Get("double")!).Value);
        Assert.Equal("hello \u4e16\u754c", parsed.GetString("string"));
    }

    [Fact]
    public void ArraysRoundTrip()
    {
        var root = new NbtCompound()
            .Set("bytes", new NbtByteArray([1, 2, 3, 255]))
            .Set("ints", new NbtIntArray([0, -1, int.MaxValue]))
            .Set("longs", new NbtLongArray([long.MinValue, 0, long.MaxValue]));

        var parsed = NbtReader.ReadNetworkRoot(NbtWriter.WriteNetworkRoot(root));

        Assert.Equal([1, 2, 3, 255], ((NbtByteArray)parsed.Get("bytes")!).Value);
        Assert.Equal([0, -1, int.MaxValue], ((NbtIntArray)parsed.Get("ints")!).Value);
        Assert.Equal([long.MinValue, 0, long.MaxValue], ((NbtLongArray)parsed.Get("longs")!).Value);
    }

    [Fact]
    public void NestedCompoundsAndListsRoundTrip()
    {
        var inner = new NbtCompound().Set("name", new NbtString("stone")).Set("count", new NbtInt(64));
        var list = new NbtList(NbtTagType.Compound, [inner, new NbtCompound().Set("name", new NbtString("dirt"))]);
        var root = new NbtCompound().Set("items", list);

        var parsed = NbtReader.ReadNetworkRoot(NbtWriter.WriteNetworkRoot(root));
        var items = parsed.GetList("items")!;

        Assert.Equal(NbtTagType.Compound, items.ElementType);
        Assert.Equal(2, items.Items.Count);
        Assert.Equal("stone", ((NbtCompound)items.Items[0]).GetString("name"));
        Assert.Equal(64, ((NbtCompound)items.Items[0]).GetInt("count"));
        Assert.Equal("dirt", ((NbtCompound)items.Items[1]).GetString("name"));
    }

    [Fact]
    public void EmptyCompoundAndEmptyListRoundTrip()
    {
        var root = new NbtCompound()
            .Set("empty", new NbtCompound())
            .Set("none", new NbtList(NbtTagType.End, []));

        var parsed = NbtReader.ReadNetworkRoot(NbtWriter.WriteNetworkRoot(root));

        Assert.Equal(0, parsed.GetCompound("empty")!.Count);
        Assert.Empty(parsed.GetList("none")!.Items);
    }

    [Fact]
    public void NamedRootKeepsItsName()
    {
        var root = new NbtCompound().Set("v", new NbtInt(7));

        var parsed = NbtReader.ReadNamedRoot(NbtWriter.WriteNamedRoot("root", root), out var name);

        Assert.Equal("root", name);
        Assert.Equal(7, parsed.GetInt("v"));
    }

    [Fact]
    public void TypedAccessorsReturnFallbacksForWrongTypes()
    {
        var root = new NbtCompound().Set("v", new NbtString("not a number"));

        Assert.Equal(5, root.GetInt("v", fallback: 5));
        Assert.Equal(0, root.GetLong("missing"));
        Assert.Null(root.GetString("missing"));
        Assert.Null(root.GetCompound("v"));
    }

    [Fact]
    public void TruncatedDataIsRejected()
    {
        var root = new NbtCompound().Set("v", new NbtInt(7));
        var bytes = NbtWriter.WriteNetworkRoot(root);

        Assert.Throws<InvalidDataException>(() => NbtReader.ReadNetworkRoot(bytes.AsSpan(0, bytes.Length - 2)));
    }

    [Fact]
    public void NonCompoundRootIsRejected()
    {
        byte[] bytes = [(byte)NbtTagType.Int, 0, 0, 0, 1];

        Assert.Throws<InvalidDataException>(() => NbtReader.ReadNetworkRoot(bytes));
    }

    [Fact]
    public void UnknownTagTypeIsRejected()
    {
        byte[] bytes = [(byte)NbtTagType.Compound, 0x63, 0x00, 0x01, 0x61];

        Assert.Throws<InvalidDataException>(() => NbtReader.ReadNetworkRoot(bytes));
    }

    [Fact]
    public void AbsurdListLengthIsRejectedBeforeAllocating()
    {
        // Compound -> list named "a" with element type Int and a count of 2 billion.
        var bytes = new List<byte> { (byte)NbtTagType.Compound, (byte)NbtTagType.List, 0x00, 0x01, (byte)'a' };
        bytes.Add((byte)NbtTagType.Int);
        bytes.AddRange([0x7F, 0xFF, 0xFF, 0xFF]);
        bytes.Add((byte)NbtTagType.End);
        bytes.Add(0x00);

        Assert.Throws<InvalidDataException>(() => NbtReader.ReadNetworkRoot(bytes.ToArray()));
    }

    [Fact]
    public void DeepNestingIsRejected()
    {
        // Build (MaxDepth + 8) nested compounds, which must trip the depth guard.
        var root = new NbtCompound();
        var current = root;
        for (var i = 0; i < NbtReader.MaxDepth + 8; i++)
        {
            var child = new NbtCompound();
            current.Set("c", child);
            current = child;
        }

        var bytes = NbtWriter.WriteNetworkRoot(root);

        Assert.Throws<InvalidDataException>(() => NbtReader.ReadNetworkRoot(bytes));
    }

    [Fact]
    public void NonEmptyListOfEndTypeIsRejected()
    {
        var bytes = new List<byte> { (byte)NbtTagType.Compound, (byte)NbtTagType.List, 0x00, 0x01, (byte)'a' };
        bytes.Add((byte)NbtTagType.End);
        bytes.AddRange([0x00, 0x00, 0x00, 0x02]);
        bytes.Add((byte)NbtTagType.End);

        Assert.Throws<InvalidDataException>(() => NbtReader.ReadNetworkRoot(bytes.ToArray()));
    }

    [Fact]
    public void CompoundPreservesInsertionOrder()
    {
        var root = new NbtCompound().Set("z", new NbtInt(1)).Set("a", new NbtInt(2)).Set("m", new NbtInt(3));

        Assert.Equal(["z", "a", "m"], root.Names);
    }
}
