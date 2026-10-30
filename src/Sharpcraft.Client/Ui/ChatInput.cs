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
/// <para>
/// Editing lives in <see cref="TextEditModel"/>, shared with every other text field, so the prompt only adds the two
/// things that are about chat: opening with a prefix, and deciding what a submitted line means.
/// </para>
/// </remarks>
public sealed class ChatInput
{
    /// <summary>Longest line the reference accepts.</summary>
    public const int MaxLength = 256;

    private readonly TextEditModel _model = new(MaxLength);

    /// <summary>True while the prompt is open and taking keys.</summary>
    public bool IsOpen { get; private set; }

    /// <summary>The editing state, for the renderer and for key handling.</summary>
    public TextEditModel Model => _model;

    /// <summary>Characters typed so far.</summary>
    public int Length => _model.Length;

    /// <summary>The typed line, for drawing and for sending.</summary>
    public string Text => _model.Value;

    /// <summary>Opens the prompt.</summary>
    /// <param name="prefix">Text to start with; a slash for a command.</param>
    public void Open(string prefix = "")
    {
        ArgumentNullException.ThrowIfNull(prefix);

        IsOpen = true;
        _model.Clear();

        if (prefix.Length > 0)
        {
            _model.Insert(prefix.AsSpan());
        }
    }

    /// <summary>Appends typed characters at the caret, stopping at the reference's limit.</summary>
    /// <param name="typed">Characters from the character callback.</param>
    public void Append(ReadOnlySpan<char> typed)
    {
        if (IsOpen)
        {
            _model.Insert(typed);
        }
    }

    /// <summary>Removes the character before the caret.</summary>
    public void Backspace()
    {
        if (IsOpen)
        {
            _model.Backspace();
        }
    }

    /// <summary>Closes the prompt and forgets the line.</summary>
    public void Close()
    {
        IsOpen = false;
        _model.Clear();
    }

    /// <summary>
    /// Closes the prompt and hands back what should be sent.
    /// </summary>
    /// <param name="message">The line, unchanged.</param>
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

        var line = _model.Value;
        IsOpen = false;
        _model.Clear();

        if (line.Length == 0)
        {
            return false;
        }

        message = line;
        isCommand = line[0] == '/';
        return true;
    }
}
