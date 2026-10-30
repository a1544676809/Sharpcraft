// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Ui;
using Xunit;

namespace Sharpcraft.Client.Tests.Ui;

/// <summary>
/// The editing model behind every text field (docs/modules/ui.md 5.2). The chat prompt, an anvil name, a sign and a
/// server address field all share it, so its behaviour is asserted once here rather than per screen.
/// </summary>
public sealed class TextEditModelTests
{
    [Fact]
    public void TypingInsertsAtTheCaretAndMovesIt()
    {
        var model = new TextEditModel();
        model.Insert("ac".AsSpan());
        model.MoveCaretTo(1);
        model.Insert("b".AsSpan());

        Assert.Equal("abc", model.Value);
        Assert.Equal(2, model.CaretPos);
    }

    [Fact]
    public void BackspaceRemovesBeforeTheCaretOnly()
    {
        var model = new TextEditModel();
        model.Insert("abc".AsSpan());
        model.MoveCaretTo(1);

        Assert.True(model.Backspace());
        Assert.Equal("bc", model.Value);
        Assert.Equal(0, model.CaretPos);
    }

    [Fact]
    public void DeleteRemovesAtTheCaret()
    {
        var model = new TextEditModel();
        model.Insert("abc".AsSpan());
        model.MoveCaretTo(1);

        Assert.True(model.Delete());
        Assert.Equal("ac", model.Value);
        Assert.Equal(1, model.CaretPos);
    }

    [Fact]
    public void BackspaceAtTheStartDoesNothing()
    {
        var model = new TextEditModel();
        model.Insert("ab".AsSpan());
        model.Home();

        Assert.False(model.Backspace());
        Assert.Equal("ab", model.Value);
    }

    [Fact]
    public void ShiftExtendsTheSelectionAndTypingReplacesIt()
    {
        var model = new TextEditModel();
        model.Insert("hello".AsSpan());
        model.Home();
        model.MoveCaret(2, extendSelection: true);

        Assert.True(model.HasSelection);
        Assert.Equal("he", model.Value[model.SelectionStart..model.SelectionEnd]);

        model.Insert("HE".AsSpan());

        Assert.Equal("HEllo", model.Value);
        Assert.False(model.HasSelection);
        Assert.Equal(2, model.CaretPos);
    }

    [Fact]
    public void HomeAndEndMoveToTheEndsAndCollapseTheSelection()
    {
        var model = new TextEditModel();
        model.Insert("abc".AsSpan());
        model.Home(extendSelection: true);

        Assert.True(model.HasSelection);
        Assert.Equal(0, model.CaretPos);
        Assert.Equal(3, model.HighlightPos);

        model.End();
        Assert.False(model.HasSelection);
        Assert.Equal(3, model.CaretPos);
    }

    [Fact]
    public void TheValueStopsAtTheLimitAndNeverSplitsCharacters()
    {
        var model = new TextEditModel(maxLength: 4);
        model.Insert("abcdef".AsSpan());

        Assert.Equal("abcd", model.Value);
        Assert.Equal(4, model.Length);

        // Full: inserting more changes nothing rather than pushing the caret past the end.
        model.Insert("g".AsSpan());
        Assert.Equal("abcd", model.Value);
        Assert.Equal(4, model.CaretPos);
    }

    [Fact]
    public void ControlCharactersAreDroppedRatherThanShown()
    {
        var model = new TextEditModel();
        model.Insert("a\tb\nc".AsSpan());

        Assert.Equal("abc", model.Value);
    }

    [Fact]
    public void SelectAllCoversTheWholeValue()
    {
        var model = new TextEditModel();
        model.Insert("abc".AsSpan());
        model.SelectAll();

        Assert.Equal(0, model.SelectionStart);
        Assert.Equal(3, model.SelectionEnd);

        model.Backspace();
        Assert.Equal(string.Empty, model.Value);
    }
}
