// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Localization;
using Sharpcraft.Common.Nbt;
using Sharpcraft.Common.Network;
using Xunit;

namespace Sharpcraft.Common.Tests.Network;

/// <summary>
/// Decorating a message must not lose the click or hover it carries (docs/modules/ui.md 8.6). A decorated line is where
/// a clickable name most often appears, so dropping the interaction there made normal chat unclickable.
/// </summary>
public sealed class StyledDecorationTests
{
    [Fact]
    public void DecorationKeepsTheClickOnThePartItBelongsTo()
    {
        var language = LanguageTable.Parse("""{ "chat.type.text": "<%s> %s" }""");

        var nameClick = new ChatClick(ChatClickAction.RunCommand, "/msg Alice ");
        var sender = new StyledRuns(
            [new TextRun("Alice", 0xFFFFFFFFu, false)],
            [nameClick],
            [null]);
        var content = new StyledRuns(
            [new TextRun("hello", 0xFFFFFFFFu, false)],
            [null],
            [null]);

        var decorated = ComponentRuns.DecorateStyled(language, "chat.type.text", [sender, content]);

        Assert.Equal("<Alice> hello", string.Concat(decorated.Runs.Select(run => run.Text)));
        Assert.Equal(decorated.Runs.Length, decorated.Clicks.Length);
        Assert.Equal(decorated.Runs.Length, decorated.Hovers.Length);

        // The click sits on the run that holds "Alice" and nowhere else.
        var nameIndex = Array.FindIndex(decorated.Runs, run => run.Text == "Alice");
        Assert.True(nameIndex >= 0);
        Assert.Same(nameClick, decorated.Clicks[nameIndex]);
        Assert.All(decorated.Clicks.Where((click, index) => index != nameIndex), click => Assert.Null(click));
    }

    [Fact]
    public void AnEmptyArgumentKeepsTheArraysAligned()
    {
        var language = LanguageTable.Parse("""{ "chat.type.text": "<%s> %s" }""");
        var sender = new StyledRuns([new TextRun("Alice", 0xFFFFFFFFu, false)], [null], [null]);

        var decorated = ComponentRuns.DecorateStyled(language, "chat.type.text", [sender]);

        Assert.Equal("<Alice> ", string.Concat(decorated.Runs.Select(run => run.Text)));
        Assert.Equal(decorated.Runs.Length, decorated.Clicks.Length);
        Assert.Equal(decorated.Runs.Length, decorated.Hovers.Length);
    }

    [Fact]
    public void AStyledNameIsReadFromThePacket()
    {
        // A clickable sender name has to survive parsing, not just decoration.
        var name = new NbtCompound();
        name.Set("text", new NbtString("Alice"));

        var click = new NbtCompound();
        click.Set("action", new NbtString("run_command"));
        click.Set("command", new NbtString("/msg Alice "));
        name.Set("click_event", click);

        var body = Body(name, "hello", signed: false);

        Assert.Equal(
            DecodeStatus.Success,
            PlayerChatPackets.TryReadPlayerChatStyled(body, out var sender, out var content, out _, out _, out var type));

        Assert.Equal(0, type);
        Assert.Equal("Alice", string.Concat(sender.Runs.Select(run => run.Text)));
        Assert.Equal(ChatClickAction.RunCommand, sender.Clicks[0]!.Action);
        Assert.Equal("hello", string.Concat(content.Runs.Select(run => run.Text)));
        Assert.Null(content.Clicks[0]);
    }

    private static byte[] Body(NbtCompound name, string text, bool signed)
    {
        var stream = new MemoryStream();
        WriteVarInt(stream, 0);                       // global index
        stream.Write(new byte[16]);                   // sender uuid
        WriteVarInt(stream, 0);                       // index
        stream.WriteByte(signed ? (byte)1 : (byte)0); // no signature

        WriteString(stream, text);                    // content
        stream.Write(new byte[8]);                    // timestamp
        stream.Write(new byte[8]);                    // salt
        WriteVarInt(stream, 0);                       // last seen count

        stream.WriteByte(0);                          // no unsigned content
        WriteVarInt(stream, 0);                       // filter mask: pass through
        WriteVarInt(stream, 0);                       // chat type id

        stream.Write(NbtWriter.WriteNetworkRoot(name));
        stream.WriteByte(0);                          // no target
        return stream.ToArray();
    }

    private static void WriteString(Stream stream, string value)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(value);
        WriteVarInt(stream, bytes.Length);
        stream.Write(bytes);
    }

    private static void WriteVarInt(Stream stream, int value)
    {
        var remaining = (uint)value;

        while (remaining >= 0x80)
        {
            stream.WriteByte((byte)(remaining | 0x80));
            remaining >>= 7;
        }

        stream.WriteByte((byte)remaining);
    }
}
