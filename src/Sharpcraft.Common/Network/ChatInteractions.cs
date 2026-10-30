// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Nbt;

namespace Sharpcraft.Common.Network;

/// <summary>What a click on a chat run does (docs/modules/ui.md 8.6).</summary>
public enum ChatClickAction
{
    /// <summary>The run does nothing when clicked.</summary>
    None,

    /// <summary>Opens a URL in the browser.</summary>
    OpenUrl,

    /// <summary>Runs a command as if typed.</summary>
    RunCommand,

    /// <summary>Puts text in the prompt without sending it.</summary>
    SuggestCommand,

    /// <summary>Copies text to the clipboard.</summary>
    CopyToClipboard
}

/// <summary>A click attached to a run of text.</summary>
/// <param name="Action">What the click does.</param>
/// <param name="Value">URL, command, or text, depending on the action.</param>
public sealed record ChatClick(ChatClickAction Action, string Value);

/// <summary>
/// Reads the click and hover behaviour a component's style carries.
/// </summary>
/// <remarks>
/// Both the current spelling (<c>click_event</c> with <c>action</c> and <c>value</c>) and the older one
/// (<c>clickEvent</c> with <c>action</c> and <c>value</c>) are accepted, because servers still send the older form.
/// An action this client does not implement resolves to <see cref="ChatClickAction.None"/> rather than failing, so one
/// unsupported click does not throw the whole message away.
/// </remarks>
public static class ChatInteractions
{
    /// <summary>Reads the click a style attaches to its text.</summary>
    /// <param name="style">Style compound, or null.</param>
    public static ChatClick? ReadClick(NbtCompound? style)
    {
        if (style is null)
        {
            return null;
        }

        var click = style.GetCompound("click_event") ?? style.GetCompound("clickEvent");

        if (click is null)
        {
            return null;
        }

        var action = click.GetString("action");
        var value = click.GetString("value") ?? string.Empty;

        var kind = action switch
        {
            "open_url" => ChatClickAction.OpenUrl,
            "run_command" => ChatClickAction.RunCommand,
            "suggest_command" => ChatClickAction.SuggestCommand,
            "copy_to_clipboard" => ChatClickAction.CopyToClipboard,
            _ => ChatClickAction.None
        };

        return kind == ChatClickAction.None ? null : new ChatClick(kind, value);
    }

    /// <summary>Reads the hover text a style attaches to its text.</summary>
    /// <param name="style">Style compound, or null.</param>
    /// <param name="language">Translation table for a translated hover text, or null.</param>
    /// <returns>The hover runs, or null when there is nothing to show.</returns>
    public static TextRun[]? ReadHover(NbtCompound? style, Localization.LanguageTable? language = null)
    {
        if (style is null)
        {
            return null;
        }

        var hover = style.GetCompound("hover_event") ?? style.GetCompound("hoverEvent");

        if (hover is null)
        {
            return null;
        }

        // Only the text form is shown; an item or entity hover needs a renderer this client does not have yet, and
        // showing nothing is better than showing the wrong thing.
        if (hover.GetString("action") is not ("show_text" or null))
        {
            return null;
        }

        var contents = hover.Get("contents") ?? hover.Get("value");

        var runs = ComponentRuns.Flatten(contents, language);
        return runs.Length == 0 ? null : runs;
    }
}
