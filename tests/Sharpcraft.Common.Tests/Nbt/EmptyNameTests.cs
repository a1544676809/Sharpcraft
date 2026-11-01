// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Nbt;
using Sharpcraft.Common.Network;
using Xunit;

namespace Sharpcraft.Common.Tests.NbtParsing;

/// <summary>
/// An empty key is legal NBT: the reference reads keys with readUtf into a plain map and never rejects the empty string,
/// and a translated chat component carries its arguments as a list of compounds, which have no name of their own.
/// </summary>
public sealed class EmptyNameTests
{
    private static void U16(List<byte> bytes, int value)
    {
        bytes.Add((byte)(value >> 8));
        bytes.Add((byte)value);
    }

    private static void Text(List<byte> bytes, string value)
    {
        U16(bytes, value.Length);
        foreach (var character in value)
        {
            bytes.Add((byte)character);
        }
    }

    [Fact]
    public void AnEntryWithAnEmptyKeyIsRead()
    {
        var bytes = new List<byte> { 10 };   // root TAG_Compound
        bytes.Add(8);                        // TAG_String
        Text(bytes, string.Empty);           // empty key
        Text(bytes, string.Empty);           // empty value
        bytes.Add(0);                        // TAG_End

        Assert.Equal(DecodeStatus.Success, NbtReader.TryReadNetworkRoot(bytes.ToArray(), out var root, out _));
        Assert.NotNull(root);
        Assert.Equal(1, root!.Count);
        Assert.Equal(string.Empty, root.Names[0]);
    }

    [Fact]
    public void AListOfCompoundsIsReadWithoutNames()
    {
        var bytes = new List<byte> { 10 };   // root TAG_Compound
        bytes.Add(9);                        // TAG_List
        Text(bytes, "with");
        bytes.Add(10);                       // elements are compounds
        bytes.AddRange([0, 0, 0, 2]);        // two of them

        for (var index = 0; index < 2; index++)
        {
            bytes.Add(8);                    // TAG_String
            Text(bytes, "text");
            Text(bytes, "a");
            bytes.Add(0);                    // end of the element compound
        }

        bytes.Add(0);                        // end of the root

        Assert.Equal(DecodeStatus.Success, NbtReader.TryReadNetworkRoot(bytes.ToArray(), out var root, out _));
        var list = Assert.IsType<NbtList>(root!.Get("with"));
        Assert.Equal(2, list.Items.Count);
        Assert.Equal("a", ((NbtCompound)list.Items[0]).GetString("text"));
    }
}
