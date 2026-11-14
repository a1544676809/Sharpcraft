// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.World;
using Sharpcraft.Common.Nbt;
using Sharpcraft.Common.World;

namespace Sharpcraft.Client.Tests;

/// <summary>
/// Pins the sign reader to the field names the reference block entity uses: front_text and
/// back_text, each with four messages, a colour and a glowing flag, plus is_waxed on the entity.
/// </summary>
public sealed class BlockEntityDataTests
{
    private static NbtCompound SignTag(string frontColour = "red", bool glowing = true, bool waxed = true)
    {
        var front = new NbtCompound()
            .Set("messages", new NbtList(NbtTagType.String, new List<NbtTag>
            {
                new NbtString("first"),
                new NbtString("second"),
                new NbtString("third"),
                new NbtString("fourth")
            }))
            .Set("color", new NbtString(frontColour))
            .Set("has_glowing_text", new NbtByte(glowing ? (sbyte)1 : (sbyte)0));

        var back = new NbtCompound()
            .Set("messages", new NbtList(NbtTagType.String, new List<NbtTag>
            {
                new NbtString("back one"),
                new NbtString(""),
                new NbtString(""),
                new NbtString("")
            }))
            .Set("color", new NbtString("black"));

        return new NbtCompound()
            .Set("front_text", front)
            .Set("back_text", back)
            .Set("is_waxed", new NbtByte(waxed ? (sbyte)1 : (sbyte)0));
    }

    private static BlockEntityRecord Record(NbtCompound? tag) => new(0, 64, 0, 7, tag);

    /// <summary>All four front lines come back in order with the colour and glow flag.</summary>
    [Fact]
    public void TheFrontFaceReadsBackInOrder()
    {
        Assert.True(BlockEntityData.TryReadSign(Record(SignTag()), out var data));

        Assert.Equal(["first", "second", "third", "fourth"], data.Front.Messages);
        Assert.Equal("red", data.Front.Colour);
        Assert.True(data.Front.Glowing);
        Assert.True(data.Waxed);
    }

    /// <summary>A blank back line keeps its place rather than shifting the lines above it.</summary>
    [Fact]
    public void TheBackFaceKeepsBlankLinesInPlace()
    {
        Assert.True(BlockEntityData.TryReadSign(Record(SignTag()), out var data));

        Assert.Equal("back one", data.Back.Messages[0]);
        Assert.Equal(string.Empty, data.Back.Messages[3]);
        Assert.False(data.Back.Glowing);
    }

    /// <summary>A record without a front face is not a sign.</summary>
    [Fact]
    public void ARecordWithoutAFrontFaceIsNotASign()
    {
        Assert.False(BlockEntityData.TryReadSign(Record(new NbtCompound()), out _));
        Assert.False(BlockEntityData.TryReadSign(Record(null), out _));
    }

    /// <summary>Missing optional fields fall back to the reference defaults.</summary>
    [Fact]
    public void MissingOptionalFieldsUseTheReferenceDefaults()
    {
        var front = new NbtCompound()
            .Set("messages", new NbtList(NbtTagType.String, new List<NbtTag> { new NbtString("only") }));
        var tag = new NbtCompound().Set("front_text", front);

        Assert.True(BlockEntityData.TryReadSign(Record(tag), out var data));
        Assert.Equal("black", data.Front.Colour);
        Assert.False(data.Front.Glowing);
        Assert.False(data.Waxed);
        Assert.Equal("only", data.Front.Messages[0]);
        Assert.Equal(string.Empty, data.Front.Messages[3]);
    }

    /// <summary>A component richer than plain text is reported blank and counted, not thrown.</summary>
    [Fact]
    public void RichComponentsAreCountedRatherThanThrowing()
    {
        var before = BlockEntityData.UnsupportedComponentCount;
        var front = new NbtCompound()
            .Set("messages", new NbtList(NbtTagType.Compound, new List<NbtTag>
            {
                new NbtCompound().Set("text", new NbtString("rich"))
            }));
        var tag = new NbtCompound().Set("front_text", front);

        Assert.True(BlockEntityData.TryReadSign(Record(tag), out var data));
        Assert.Equal(string.Empty, data.Front.Messages[0]);
        Assert.True(BlockEntityData.UnsupportedComponentCount > before);
    }
}
