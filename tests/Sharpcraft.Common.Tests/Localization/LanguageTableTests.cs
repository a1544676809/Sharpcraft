// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Localization;
using Sharpcraft.Common.Nbt;
using Sharpcraft.Common.Network;
using Xunit;

namespace Sharpcraft.Common.Tests.Localization;

/// <summary>
/// Translation lookup and argument substitution (docs/modules/ui.md 8.4). A server sends keys rather than sentences,
/// so a client without this shows "command.unknown.command" where the original shows a sentence.
/// </summary>
public sealed class LanguageTableTests
{
    private const string Sample = """
{
  "chat.type.text": "<%s> %s",
  "command.unknown.command": "Unknown or incomplete command, see below for error",
  "multi.player.list": "%1$s and %2$s",
  "percent.example": "100%% sure",
  "not.text": 42
}
""";

    [Fact]
    public void KeysResolveAndUnknownKeysComeBackAsThemselves()
    {
        var table = LanguageTable.Parse(Sample);

        Assert.Equal("Unknown or incomplete command, see below for error", table.Resolve("command.unknown.command"));
        Assert.Equal("missing.key", table.Resolve("missing.key"));
        Assert.Equal(4, table.Count);
    }

    [Fact]
    public void SequentialArgumentsFillInOrder()
    {
        var table = LanguageTable.Parse(Sample);

        Assert.Equal("<Alice> hello", table.Format("chat.type.text", ["Alice", "hello"]));
    }

    [Fact]
    public void NumberedArgumentsFillInTheirOwnPlace()
    {
        var table = LanguageTable.Parse(Sample);

        Assert.Equal("Alice and Bob", table.Format("multi.player.list", ["Alice", "Bob"]));

        // Reversing the arguments reverses the sentence, which is the point of numbering them.
        Assert.Equal("Bob and Alice", table.Format("multi.player.list", ["Bob", "Alice"]));
    }

    [Fact]
    public void ADoublePercentIsALiteralPercent()
    {
        var table = LanguageTable.Parse(Sample);

        Assert.Equal("100% sure", table.Format("percent.example", []));
    }

    [Fact]
    public void AMissingArgumentLeavesNothingBehind()
    {
        var table = LanguageTable.Parse(Sample);

        // One argument for a two argument sentence: the second slot simply stays empty rather than printing a
        // placeholder at the player.
        Assert.Equal("<Alice> ", table.Format("chat.type.text", ["Alice"]));
    }

    [Fact]
    public void ValuesThatAreNotTextAreSkippedRatherThanBreakingTheTable()
    {
        var table = LanguageTable.Parse(Sample);

        Assert.False(table.TryGet("not.text", out _));
        Assert.Equal("command.unknown.command", LanguageTable.Empty.Resolve("command.unknown.command"));
    }

    [Fact]
    public void ATranslatedComponentWithArgumentsIsFilledIn()
    {
        var table = LanguageTable.Parse(Sample);

        var sender = new NbtCompound();
        sender.Set("text", new NbtString("Alice"));

        var body = new NbtCompound();
        body.Set("text", new NbtString("hello"));

        var message = new NbtCompound();
        message.Set("translate", new NbtString("chat.type.text"));
        message.Set("with", new NbtList(NbtTagType.Compound, [sender, body]));

        Assert.Equal("<Alice> hello", ComponentText.Flatten(message, table));

        // Without a table the key is shown, which is honest about the missing translation.
        Assert.Equal("chat.type.text", ComponentText.Flatten(message));
    }

    [Fact]
    public void ExtraRunsAreAppendedAfterTheTranslatedText()
    {
        var table = LanguageTable.Parse(Sample);

        var head = new NbtCompound();
        head.Set("translate", new NbtString("command.unknown.command"));

        var tail = new NbtCompound();
        tail.Set("text", new NbtString(" [here]"));

        head.Set("extra", new NbtList(NbtTagType.Compound, [tail]));

        Assert.Equal("Unknown or incomplete command, see below for error [here]", ComponentText.Flatten(head, table));
    }
}
