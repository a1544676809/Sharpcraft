// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 MinecraftCSharp contributors

namespace MinecraftCSharp.Common.Nbt;

/// <summary>Base of every NBT value.</summary>
public abstract class NbtTag
{
    /// <summary>Wire type of this value.</summary>
    public abstract NbtTagType Type { get; }
}

/// <summary>Signed 8-bit value.</summary>
public sealed class NbtByte(sbyte value) : NbtTag
{
    /// <summary>Payload.</summary>
    public sbyte Value { get; } = value;

    /// <inheritdoc />
    public override NbtTagType Type => NbtTagType.Byte;
}

/// <summary>Signed 16-bit value.</summary>
public sealed class NbtShort(short value) : NbtTag
{
    /// <summary>Payload.</summary>
    public short Value { get; } = value;

    /// <inheritdoc />
    public override NbtTagType Type => NbtTagType.Short;
}

/// <summary>Signed 32-bit value.</summary>
public sealed class NbtInt(int value) : NbtTag
{
    /// <summary>Payload.</summary>
    public int Value { get; } = value;

    /// <inheritdoc />
    public override NbtTagType Type => NbtTagType.Int;
}

/// <summary>Signed 64-bit value.</summary>
public sealed class NbtLong(long value) : NbtTag
{
    /// <summary>Payload.</summary>
    public long Value { get; } = value;

    /// <inheritdoc />
    public override NbtTagType Type => NbtTagType.Long;
}

/// <summary>32-bit float value.</summary>
public sealed class NbtFloat(float value) : NbtTag
{
    /// <summary>Payload.</summary>
    public float Value { get; } = value;

    /// <inheritdoc />
    public override NbtTagType Type => NbtTagType.Float;
}

/// <summary>64-bit float value.</summary>
public sealed class NbtDouble(double value) : NbtTag
{
    /// <summary>Payload.</summary>
    public double Value { get; } = value;

    /// <inheritdoc />
    public override NbtTagType Type => NbtTagType.Double;
}

/// <summary>UTF-8 string value.</summary>
public sealed class NbtString(string value) : NbtTag
{
    /// <summary>Payload.</summary>
    public string Value { get; } = value;

    /// <inheritdoc />
    public override NbtTagType Type => NbtTagType.String;
}

/// <summary>Byte array value.</summary>
public sealed class NbtByteArray(byte[] value) : NbtTag
{
    /// <summary>Payload.</summary>
    public byte[] Value { get; } = value;

    /// <inheritdoc />
    public override NbtTagType Type => NbtTagType.ByteArray;
}

/// <summary>Int array value.</summary>
public sealed class NbtIntArray(int[] value) : NbtTag
{
    /// <summary>Payload.</summary>
    public int[] Value { get; } = value;

    /// <inheritdoc />
    public override NbtTagType Type => NbtTagType.IntArray;
}

/// <summary>Long array value.</summary>
public sealed class NbtLongArray(long[] value) : NbtTag
{
    /// <summary>Payload.</summary>
    public long[] Value { get; } = value;

    /// <inheritdoc />
    public override NbtTagType Type => NbtTagType.LongArray;
}

/// <summary>Homogeneous list of tags.</summary>
public sealed class NbtList(NbtTagType elementType, List<NbtTag> items) : NbtTag
{
    /// <summary>Type shared by every element.</summary>
    public NbtTagType ElementType { get; } = elementType;

    /// <summary>Elements.</summary>
    public IReadOnlyList<NbtTag> Items { get; } = items;

    /// <inheritdoc />
    public override NbtTagType Type => NbtTagType.List;
}

/// <summary>Named tag map.</summary>
public sealed class NbtCompound : NbtTag
{
    private readonly Dictionary<string, NbtTag> _children = new(StringComparer.Ordinal);
    private readonly List<string> _order = [];

    /// <inheritdoc />
    public override NbtTagType Type => NbtTagType.Compound;

    /// <summary>Number of children.</summary>
    public int Count => _order.Count;

    /// <summary>Child names in insertion order.</summary>
    public IReadOnlyList<string> Names => _order;

    /// <summary>Adds or replaces a child.</summary>
    public NbtCompound Set(string name, NbtTag value)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(value);

        if (!_children.ContainsKey(name))
        {
            _order.Add(name);
        }

        _children[name] = value;
        return this;
    }

    /// <summary>Child by name, or null.</summary>
    public NbtTag? Get(string name) => _children.TryGetValue(name, out var tag) ? tag : null;

    /// <summary>Whether a child exists.</summary>
    public bool Contains(string name) => _children.ContainsKey(name);

    /// <summary>Typed read; returns the default when absent or of another type.</summary>
    public int GetInt(string name, int fallback = 0) =>
        Get(name) is NbtInt tag ? tag.Value : fallback;

    /// <summary>Typed read; returns the default when absent or of another type.</summary>
    public long GetLong(string name, long fallback = 0) =>
        Get(name) is NbtLong tag ? tag.Value : fallback;

    /// <summary>Typed read; returns the default when absent or of another type.</summary>
    public string? GetString(string name) => Get(name) is NbtString tag ? tag.Value : null;

    /// <summary>Typed read; returns null when absent or of another type.</summary>
    public NbtCompound? GetCompound(string name) => Get(name) as NbtCompound;

    /// <summary>Typed read; returns null when absent or of another type.</summary>
    public NbtList? GetList(string name) => Get(name) as NbtList;

    /// <summary>Typed read; returns null when absent or of another type.</summary>
    public long[]? GetLongArray(string name) => (Get(name) as NbtLongArray)?.Value;
}
