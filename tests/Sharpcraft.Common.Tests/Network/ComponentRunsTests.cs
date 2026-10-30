// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Nbt;
using Sharpcraft.Common.Network;
using Xunit;

namespace Sharpcraft.Common.Tests.Network;

/// <summary>
/// Styled runs from a chat component (docs/modules/ui.md 8.4). Appearance is inherited down the tree, and the colours
/// are the reference's own table; flattening a message to one colour loses the difference between a team name, an
/// ordinary line and an error.
/// </summary>
public sealed class ComponentRunsTests
{
    [Theory]
    [InlineData("black", 0xFF000000u)]
    [InlineData("dark_red", 0xFFAA0000u)]
    [InlineData("gold", 0xFFFFAA00u)]
    [InlineData("dark_gray", 0xFF555555u)]
    [InlineData("white", 0xFFFFFFFFu)]
    [InlineData("#12AB34", 0xFF12AB34u)]
    public void NamedAndHexColoursMatchTheReferenceTable(string value, uint expected)
    {
        Assert.True(ComponentRuns.TryParseColour(value, out var colour));
        Assert.Equal(expected, colour);
    }

    [Fact]
    public void AnUnknownColourIsIgnoredRatherThanTurningTheTextBlack()
    {
        Assert.False(ComponentRuns.TryParseColour("chartreuse", out _));
        Assert.False(ComponentRuns.TryParseColour("#12345", out _));
    }

    [Fact]
    public void AChildInheritsItsParentsAppearance()
    {
        var parent = new NbtCompound();
        parent.Set("text", new NbtString("outer "));
        parent.Set("color", new NbtString("gold"));

        var child = new NbtCompound();
        child.Set("text", new NbtString("inner"));
        parent.Set("extra", new NbtList(NbtTagType.Compound, [child]));

        var runs = ComponentRuns.Flatten(parent);

        Assert.Equal(2, runs.Length);
        Assert.Equal("outer ", runs[0].Text);
        Assert.Equal("inner", runs[1].Text);
        Assert.All(runs, run => Assert.Equal(0xFFFFAA00u, run.Colour));
    }

    [Fact]
    public void AChildCanOverrideTheColourAndBoldness()
    {
        var parent = new NbtCompound();
        parent.Set("text", new NbtString("a"));
        parent.Set("color", new NbtString("red"));
        parent.Set("bold", new NbtByte(1));

        var child = new NbtCompound();
        child.Set("text", new NbtString("b"));
        child.Set("color", new NbtString("aqua"));
        child.Set("bold", new NbtByte(0));
        parent.Set("extra", new NbtList(NbtTagType.Compound, [child]));

        var runs = ComponentRuns.Flatten(parent);

        Assert.Equal(0xFFFF5555u, runs[0].Colour);
        Assert.True(runs[0].Bold);
        Assert.Equal(0xFF55FFFFu, runs[1].Colour);
        Assert.False(runs[1].Bold);
    }

    [Fact]
    public void TextWithoutAStyleIsWhiteAndNotBold()
    {
        var compound = new NbtCompound();
        compound.Set("text", new NbtString("plain"));

        var runs = ComponentRuns.Flatten(compound);

        Assert.Single(runs);
        Assert.Equal(ComponentRuns.DefaultColour, runs[0].Colour);
        Assert.False(runs[0].Bold);
    }

    [Fact]
    public void ATranslatedNodeKeepsItsOwnColour()
    {
        var table = Sharpcraft.Common.Localization.LanguageTable.Parse(
            """{ "chat.type.text": "<%s> %s" }""");

        var sender = new NbtCompound();
        sender.Set("text", new NbtString("Alice"));

        var body = new NbtCompound();
        body.Set("text", new NbtString("hi"));

        var node = new NbtCompound();
        node.Set("translate", new NbtString("chat.type.text"));
        node.Set("color", new NbtString("yellow"));
        node.Set("with", new NbtList(NbtTagType.Compound, [sender, body]));

        var runs = ComponentRuns.Flatten(node, table);

        Assert.Single(runs);
        Assert.Equal("<Alice> hi", runs[0].Text);
        Assert.Equal(0xFFFFFF55u, runs[0].Colour);
    }
}
