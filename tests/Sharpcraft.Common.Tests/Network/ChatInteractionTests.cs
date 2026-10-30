// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Nbt;
using Sharpcraft.Common.Network;
using Xunit;

namespace Sharpcraft.Common.Tests.Network;

/// <summary>
/// The click and hover behaviour a chat run carries (docs/modules/ui.md 8.6). Keeping the reading separate from the
/// hit testing means the parsing can be trusted before anything is clicked.
/// </summary>
public sealed class ChatInteractionTests
{
    [Fact]
    public void AClickIsReadFromTheCurrentSpelling()
    {
        var click = Style(ClickEvent("open_url", "url", "https://example.invalid/a"));

        var parsed = ChatInteractions.ReadClick(click);

        Assert.NotNull(parsed);
        Assert.Equal(ChatClickAction.OpenUrl, parsed!.Action);
        Assert.Equal("https://example.invalid/a", parsed.Value);
    }

    [Fact]
    public void AClickIsReadFromTheOlderSpellingToo()
    {
        var style = new NbtCompound();
        style.Set("clickEvent", ClickEvent("run_command", "command", "/spawn"));

        Assert.Equal(ChatClickAction.RunCommand, ChatInteractions.ReadClick(style)!.Action);
    }

    [Theory]
    [InlineData("suggest_command", ChatClickAction.SuggestCommand)]
    [InlineData("copy_to_clipboard", ChatClickAction.CopyToClipboard)]
    public void TheOtherActionsAreRecognised(string action, ChatClickAction expected) =>
        Assert.Equal(expected, ChatInteractions.ReadClick(Style(ClickEvent(action, action == "copy_to_clipboard" ? "value" : "command", "x")))!.Action);

    [Fact]
    public void AnUnsupportedActionIsIgnoredRatherThanBreakingTheMessage()
    {
        // An action this client does not implement must not throw the message away.
        Assert.Null(ChatInteractions.ReadClick(Style(ClickEvent("change_page", "page", "2"))));
        Assert.Null(ChatInteractions.ReadClick(new NbtCompound()));
        Assert.Null(ChatInteractions.ReadClick(null));
    }

    [Fact]
    public void HoverTextIsReadAndFlattened()
    {
        var hover = new NbtCompound();
        hover.Set("action", new NbtString("show_text"));

        var text = new NbtCompound();
        text.Set("text", new NbtString("a tip"));
        hover.Set("contents", text);

        var style = new NbtCompound();
        style.Set("hover_event", hover);

        var runs = ChatInteractions.ReadHover(style);

        Assert.NotNull(runs);
        Assert.Equal("a tip", string.Concat(runs!.Select(run => run.Text)));
    }

    [Fact]
    public void AnItemHoverShowsNothingRatherThanSomethingWrong()
    {
        var hover = new NbtCompound();
        hover.Set("action", new NbtString("show_item"));
        hover.Set("contents", new NbtCompound());

        var style = new NbtCompound();
        style.Set("hover_event", hover);

        Assert.Null(ChatInteractions.ReadHover(style));
    }

    private static NbtCompound Style(NbtCompound click)
    {
        var style = new NbtCompound();
        style.Set("click_event", click);
        return style;
    }

    private static NbtCompound ClickEvent(string action, string field, string value)
    {
        var click = new NbtCompound();
        click.Set("action", new NbtString(action));
        click.Set("fieldName", new NbtString(field));
        click.Set(field, new NbtString(value));
        return click;
    }

    [Fact]
    public void FlattenStyledKeepsInteractionsBesideTheRuns()
    {
        var click = ClickEvent("run_command", "command", "/spawn");

        var hover = new NbtCompound();
        hover.Set("action", new NbtString("show_text"));
        var tip = new NbtCompound();
        tip.Set("text", new NbtString("a tip"));
        hover.Set("value", tip);

        var styled = new NbtCompound();
        styled.Set("text", new NbtString("clickable"));
        styled.Set("click_event", click);
        styled.Set("hover_event", hover);

        var root = new NbtCompound();
        root.Set("text", new NbtString("plain "));
        root.Set("extra", new NbtList(NbtTagType.Compound, [styled]));

        var (runs, clicks, hovers) = ComponentRuns.FlattenStyled(root);

        Assert.Equal(2, runs.Length);
        Assert.Equal(2, clicks.Length);
        Assert.Equal(2, hovers.Length);
        Assert.Null(clicks[0]);
        Assert.Equal(ChatClickAction.RunCommand, clicks[1]!.Action);
        Assert.Null(hovers[0]);
        Assert.Equal("a tip", string.Concat(hovers[1]!.Select(run => run.Text)));
    }
}
