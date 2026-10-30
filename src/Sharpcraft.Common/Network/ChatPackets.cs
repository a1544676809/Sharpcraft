// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Text;
using Sharpcraft.Common.Localization;
using Sharpcraft.Common.Nbt;

namespace Sharpcraft.Common.Network;

/// <summary>
/// Chat the server sends, decoded to plain text (docs/modules/ui.md 8.4).
/// </summary>
/// <remarks>
/// Chat arrives as a component, which is a tree of text runs rather than a string. The interface draws plain text for
/// now, so the tree is flattened here: the visible text of a node, then each of its extras in order. Styling,
/// translation with arguments and hover events are not represented yet and are recorded as such.
/// </remarks>
public static class ChatPackets
{
    /// <summary>Reads a <c>system_chat</c> body, keeping its styling.</summary>
    /// <param name="body">Packet body without the id.</param>
    /// <param name="runs">Styled runs of the line.</param>
    /// <param name="overlay">True when the line belongs in the action bar rather than the chat log.</param>
    /// <param name="language">Translation table, or null to leave keys unresolved.</param>
    public static DecodeStatus TryReadSystemChat(
        ReadOnlySpan<byte> body,
        out TextRun[] runs,
        out bool overlay,
        LanguageTable? language = null)
    {
        runs = [];
        overlay = false;

        var styledStatus = NbtReader.TryReadNetworkRoot(body, out var styledRoot, out var styledConsumed);

        if (styledStatus != DecodeStatus.Success || styledRoot is null)
        {
            return styledStatus == DecodeStatus.Success ? DecodeStatus.Invalid : styledStatus;
        }

        var styledReader = new PayloadReader(body[styledConsumed..]);

        if (styledReader.TryReadBool(out var styledOverlay) != DecodeStatus.Success)
        {
            return DecodeStatus.NeedMoreData;
        }

        runs = ComponentRuns.Flatten(styledRoot, language);
        overlay = styledOverlay;
        return DecodeStatus.Success;
    }

    /// <summary>Reads a <c>system_chat</c> body.</summary>
    /// <param name="body">Packet body without the id.</param>
    /// <param name="text">Flattened text.</param>
    /// <param name="overlay">True when the line belongs in the action bar rather than the chat log.</param>
    /// <param name="language">Translation table, or null to leave keys unresolved.</param>
    public static DecodeStatus TryReadSystemChat(
        ReadOnlySpan<byte> body,
        out string text,
        out bool overlay,
        LanguageTable? language = null)
    {
        var status = TryReadSystemChat(body, out TextRun[] runs, out overlay, language);
        text = string.Empty;

        if (status != DecodeStatus.Success)
        {
            return status;
        }

        var builder = new System.Text.StringBuilder();

        for (var index = 0; index < runs.Length; index++)
        {
            builder.Append(runs[index].Text);
        }

        text = builder.ToString();
        return DecodeStatus.Success;
    }
}

/// <summary>Flattens a chat component tree into the text it displays.</summary>
public static class ComponentText
{
    /// <summary>How deep the tree is followed before giving up.</summary>
    public const int MaxDepth = 16;

    /// <summary>Flattens a component into plain text.</summary>
    /// <param name="tag">Component node, usually a compound.</param>
    /// <param name="language">Translation table, or null to leave keys unresolved.</param>
    public static string Flatten(NbtTag? tag, LanguageTable? language = null)
    {
        var builder = new StringBuilder();
        Append(builder, tag, 0, language);
        return builder.ToString();
    }

    /// <summary>Flattens the <c>with</c> arguments of a translated component.</summary>
    private static string[] FlattenArguments(NbtCompound compound, int depth, LanguageTable? language)
    {
        if (compound.Get("with") is not NbtList arguments || arguments.Items.Count == 0)
        {
            return [];
        }

        var flattened = new string[arguments.Items.Count];

        for (var index = 0; index < flattened.Length; index++)
        {
            flattened[index] = Flatten(arguments.Items[index], language);
        }

        return flattened;
    }

    private static void Append(StringBuilder builder, NbtTag? tag, int depth, LanguageTable? language)
    {
        if (tag is null || depth > MaxDepth)
        {
            return;
        }

        if (tag is NbtString literal)
        {
            // A bare string is a plain text component.
            builder.Append(literal.Value);
            return;
        }

        if (tag is not NbtCompound compound)
        {
            return;
        }

        var text = compound.GetString("text");

        if (text is not null)
        {
            builder.Append(text);
        }
        else if (compound.GetString("translate") is { } translate)
        {
            // A translated node carries a key and, usually, the arguments to fill in. Each argument is itself a
            // component, so it is flattened first and the result substituted into the translated sentence.
            var arguments = FlattenArguments(compound, depth, language);

            if (language is not null)
            {
                builder.Append(language.Format(translate, arguments));
            }
            else
            {
                // Without a table the key is the honest rendering: it makes a missing translation visible instead of
                // silently blank.
                builder.Append(translate);
            }
        }
        else if (compound.GetString("fallback") is { } fallback)
        {
            builder.Append(fallback);
        }

        if (compound.Get("extra") is NbtList extra)
        {
            for (var index = 0; index < extra.Items.Count; index++)
            {
                Append(builder, extra.Items[index], depth + 1, language);
            }
        }
    }
}
