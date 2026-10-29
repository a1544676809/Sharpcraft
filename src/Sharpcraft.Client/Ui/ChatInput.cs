// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Ui;

/// <summary>
/// The chat prompt: whether it is open, what has been typed, and what submitting it means
/// (docs/modules/ui.md 8.4).
/// </summary>
/// <remarks>
/// Two details follow the reference rather than taste. Opening with the slash key leaves the slash in the box, so
/// the player can keep typing a command without pressing it again. And a line that begins with a slash is sent as a
/// command, not as a chat message, which is a different packet: the server treats them differently and a command
/// sent as chat is simply shown to everyone as text.
/// </remarks>
public sealed class ChatInput
{
    /// <summary>Longest line the reference accepts.</summary>
    public const int MaxLength = 256;

    private readonly System.Text.StringBuilder _text = new();

    /// <summary>True while the prompt is open and taking keys.</summary>
    public bool IsOpen { get; private set; }

    /// <summary>Characters typed so far.</summary>
    public int Length => _text.Length;

    /// <summary>The typed line, for drawing and for sending.</summary>
    public string Text => _text.ToString();

    /// <summary>Opens the prompt.</summary>
    /// <param name="prefix">Text to start with; a slash for a command.</param>
    public void Open(string prefix = "")
    {
        ArgumentNullException.ThrowIfNull(prefix);

        IsOpen = true;
        _text.Clear();

        if (prefix.Length > 0)
        {
            _text.Append(prefix);
        }
    }

    /// <summary>Appends typed characters, stopping at the reference's limit.</summary>
    /// <param name="typed">Characters from the character callback.</param>
    public void Append(ReadOnlySpan<char> typed)
    {
        if (!IsOpen)
        {
            return;
        }

        for (var index = 0; index < typed.Length && _text.Length < MaxLength; index++)
        {
            // Control characters never reach the box: the reference drops them rather than showing glyphs for them.
            if (!char.IsControl(typed[index]))
            {
                _text.Append(typed[index]);
            }
        }
    }

    /// <summary>Removes the last character, if any.</summary>
    public void Backspace()
    {
        if (IsOpen && _text.Length > 0)
        {
            _text.Length--;
        }
    }

    /// <summary>Closes the prompt and forgets the line.</summary>
    public void Close()
    {
        IsOpen = false;
        _text.Clear();
    }

    /// <summary>
    /// Closes the prompt and hands back what should be sent.
    /// </summary>
    /// <param name="message">The line, with no trailing whitespace removed.</param>
    /// <param name="isCommand">True when the line starts with a slash.</param>
    /// <returns>False when the prompt was closed or the line was blank, in which case nothing is sent.</returns>
    public bool TrySubmit(out string message, out bool isCommand)
    {
        message = string.Empty;
        isCommand = false;

        if (!IsOpen)
        {
            return false;
        }

        var line = _text.ToString();
        IsOpen = false;
        _text.Clear();

        if (line.Length == 0)
        {
            return false;
        }

        message = line;
        isCommand = line[0] == '/';
        return true;
    }
}
