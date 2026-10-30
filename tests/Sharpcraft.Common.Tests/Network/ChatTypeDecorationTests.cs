// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Localization;
using Sharpcraft.Common.Nbt;
using Sharpcraft.Common.Network;
using Xunit;

namespace Sharpcraft.Common.Tests.Network;

/// <summary>
/// The decoration a chat type applies (docs/decisions/0012-chat-type-registry.md). A player message is
/// <c>&lt;name&gt; text</c> only because the chat type says so; the same packet renders differently under another type,
/// which is why the shape cannot be assumed.
/// </summary>
public sealed class ChatTypeDecorationTests
{
    [Fact]
    public void ADecorationIsReadFromTheChatTypePayload()
    {
        var chat = new NbtCompound();
        chat.Set("translation_key", new NbtString("chat.type.text"));
        chat.Set("parameters", new NbtList(NbtTagType.String, [new NbtString("sender"), new NbtString("content")]));

        var payload = new NbtCompound();
        payload.Set("chat", chat);

        var decoration = ChatTypeDecoration.TryRead(payload);

        Assert.NotNull(decoration);
        Assert.Equal("chat.type.text", decoration!.TranslationKey);
        Assert.Equal(["sender", "content"], decoration.Parameters);
    }

    [Fact]
    public void APayloadWithoutADecorationYieldsNothing()
    {
        Assert.Null(ChatTypeDecoration.TryRead(null));
        Assert.Null(ChatTypeDecoration.TryRead(new NbtCompound()));
    }

    [Fact]
    public void TheDecorationWrapsTheNameAndKeepsItsColour()
    {
        var language = LanguageTable.Parse("""{ "chat.type.text": "<%s> %s" }""");
        var sender = new[] { new TextRun("Alice", 0xFFFF5555u, Bold: false) };
        var content = new[] { new TextRun("hello", 0xFFFFFFFFu, Bold: false) };

        var decorated = ComponentRuns.Decorate(language, "chat.type.text", [sender, content]);

        Assert.Equal("<Alice> hello", string.Concat(decorated.Select(run => run.Text)));

        // The sender keeps the colour it arrived with; substitution happens on runs, not on text.
        Assert.Contains(decorated, run => run.Text == "Alice" && run.Colour == 0xFFFF5555u);
    }

    [Fact]
    public void AMissingArgumentIsDroppedRatherThanShownAsAPlaceholder()
    {
        var language = LanguageTable.Parse("""{ "chat.type.text": "<%s> %s" }""");
        var sender = new[] { new TextRun("Alice", 0xFFFFFFFFu, false) };

        var decorated = ComponentRuns.Decorate(language, "chat.type.text", [sender]);

        Assert.Equal("<Alice> ", string.Concat(decorated.Select(run => run.Text)));
    }

    [Fact]
    public void APerCentSignSurvivesAndAnUnknownKeyIsLeftVisible()
    {
        var language = LanguageTable.Parse("""{ "test.percent": "100%% %s" }""");
        var value = new[] { new TextRun("done", 0xFFFFFFFFu, false) };

        Assert.Equal("100% done", string.Concat(ComponentRuns.Decorate(language, "test.percent", [value]).Select(r => r.Text)));
        Assert.Equal("missing.key", string.Concat(ComponentRuns.Decorate(language, "missing.key", []).Select(r => r.Text)));
    }

    [Fact]
    public void TheDefaultDecorationMatchesTheOrdinaryChatShape()
    {
        Assert.Equal("chat.type.text", ChatTypeDecoration.Default.TranslationKey);
        Assert.Equal([ChatTypeDecoration.Sender, ChatTypeDecoration.Content], ChatTypeDecoration.Default.Parameters);
    }
}
