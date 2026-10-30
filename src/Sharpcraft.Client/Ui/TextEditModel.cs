// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Text;

namespace Sharpcraft.Client.Ui;

/// <summary>
/// Editing state for a single line of text: the value, a caret, and a selection (docs/modules/ui.md 5.2).
/// </summary>
/// <remarks>
/// This is infrastructure rather than a chat detail: the chat prompt, an anvil name, a sign, a book page and a server
/// address field all need the same behaviour, so the model keeps no knowledge of any of them. Positions are indices
/// into the value, and the caret and the highlight are separate so a selection is simply "they differ".
/// </remarks>
public sealed class TextEditModel
{
    private readonly StringBuilder _value = new();
    private readonly int _maxLength;

    /// <summary>Creates a model.</summary>
    /// <param name="maxLength">Longest value accepted, in characters.</param>
    public TextEditModel(int maxLength = 256) => _maxLength = Math.Max(1, maxLength);

    /// <summary>The value.</summary>
    public string Value => _value.ToString();

    /// <summary>Value length in characters.</summary>
    public int Length => _value.Length;

    /// <summary>Longest value accepted.</summary>
    public int MaxLength => _maxLength;

    /// <summary>Caret position, from zero to the length.</summary>
    public int CaretPos { get; private set; }

    /// <summary>Other end of the selection; equal to the caret when there is none.</summary>
    public int HighlightPos { get; private set; }

    /// <summary>True when something is selected.</summary>
    public bool HasSelection => CaretPos != HighlightPos;

    /// <summary>Lower end of the selection.</summary>
    public int SelectionStart => Math.Min(CaretPos, HighlightPos);

    /// <summary>Upper end of the selection.</summary>
    public int SelectionEnd => Math.Max(CaretPos, HighlightPos);

    /// <summary>Forgets the value and both positions.</summary>
    public void Clear()
    {
        _value.Clear();
        CaretPos = 0;
        HighlightPos = 0;
    }

    /// <summary>Replaces the value and puts the caret at the end.</summary>
    /// <param name="text">New value; longer values are truncated.</param>
    public void Set(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        _value.Clear();
        _value.Append(text.Length > _maxLength ? text[.._maxLength] : text);
        CaretPos = _value.Length;
        HighlightPos = CaretPos;
    }

    /// <summary>Inserts text at the caret, replacing any selection.</summary>
    /// <param name="text">Characters to insert; control characters are dropped.</param>
    public void Insert(ReadOnlySpan<char> text)
    {
        var start = SelectionStart;
        var end = SelectionEnd;

        if (start != end)
        {
            _value.Remove(start, end - start);
            CaretPos = start;
            HighlightPos = start;
        }

        var room = _maxLength - _value.Length;
        var inserted = 0;

        for (var index = 0; index < text.Length && inserted < room; index++)
        {
            if (char.IsControl(text[index]))
            {
                continue;
            }

            _value.Insert(CaretPos + inserted, text[index]);
            inserted++;
        }

        CaretPos += inserted;
        HighlightPos = CaretPos;
    }

    /// <summary>Removes the selection, or the character before the caret, and moves the caret back.</summary>
    /// <returns>True when something was removed.</returns>
    public bool Backspace()
    {
        if (HasSelection)
        {
            return RemoveSelection();
        }

        if (CaretPos == 0)
        {
            return false;
        }

        _value.Remove(CaretPos - 1, 1);
        CaretPos--;
        HighlightPos = CaretPos;
        return true;
    }

    /// <summary>Removes the selection, or the character at the caret.</summary>
    /// <returns>True when something was removed.</returns>
    public bool Delete()
    {
        if (HasSelection)
        {
            return RemoveSelection();
        }

        if (CaretPos >= _value.Length)
        {
            return false;
        }

        _value.Remove(CaretPos, 1);
        return true;
    }

    /// <summary>Moves the caret, optionally extending the selection.</summary>
    /// <param name="delta">Positions to move; negative goes left.</param>
    /// <param name="extendSelection">True to keep the far end where it is.</param>
    public void MoveCaret(int delta, bool extendSelection = false) =>
        MoveCaretTo(CaretPos + delta, extendSelection);

    /// <summary>Moves the caret to a position, clamped to the value.</summary>
    /// <param name="position">Target position.</param>
    /// <param name="extendSelection">True to keep the far end where it is.</param>
    public void MoveCaretTo(int position, bool extendSelection = false)
    {
        CaretPos = Math.Clamp(position, 0, _value.Length);

        if (!extendSelection)
        {
            HighlightPos = CaretPos;
        }
    }

    /// <summary>Moves the caret to the start of the value.</summary>
    /// <param name="extendSelection">True to keep the far end where it is.</param>
    public void Home(bool extendSelection = false) => MoveCaretTo(0, extendSelection);

    /// <summary>Moves the caret to the end of the value.</summary>
    /// <param name="extendSelection">True to keep the far end where it is.</param>
    public void End(bool extendSelection = false) => MoveCaretTo(_value.Length, extendSelection);

    /// <summary>Selects everything.</summary>
    public void SelectAll()
    {
        HighlightPos = 0;
        CaretPos = _value.Length;
    }

    private bool RemoveSelection()
    {
        var start = SelectionStart;
        var length = SelectionEnd - start;
        _value.Remove(start, length);
        CaretPos = start;
        HighlightPos = start;
        return true;
    }
}
