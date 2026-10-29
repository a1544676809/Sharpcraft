// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Text;
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
    /// <summary>Reads a <c>system_chat</c> body.</summary>
    /// <param name="body">Packet body without the id.</param>
    /// <param name="text">Flattened text.</param>
    /// <param name="overlay">True when the line belongs in the action bar rather than the chat log.</param>
    public static DecodeStatus TryReadSystemChat(ReadOnlySpan<byte> body, out string text, out bool overlay)
    {
        text = string.Empty;
        overlay = false;

        var status = NbtReader.TryReadNetworkRoot(body, out var root, out var consumed);

        if (status != DecodeStatus.Success || root is null)
        {
            return status == DecodeStatus.Success ? DecodeStatus.Invalid : status;
        }

        var reader = new PayloadReader(body[consumed..]);

        if (reader.TryReadBool(out var isOverlay) != DecodeStatus.Success)
        {
            return DecodeStatus.NeedMoreData;
        }

        text = ComponentText.Flatten(root);
        overlay = isOverlay;
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
    public static string Flatten(NbtTag? tag)
    {
        var builder = new StringBuilder();
        Append(builder, tag, 0);
        return builder.ToString();
    }

    private static void Append(StringBuilder builder, NbtTag? tag, int depth)
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
        else
        {
            // Translated components carry a key and arguments. Without a language table the key is the best honest
            // rendering, and it is what makes a missing translation visible instead of silently blank.
            var translate = compound.GetString("translate");

            if (translate is not null)
            {
                builder.Append(translate);
            }
            else if (compound.GetString("fallback") is { } fallback)
            {
                builder.Append(fallback);
            }
        }

        if (compound.Get("extra") is NbtList extra)
        {
            for (var index = 0; index < extra.Items.Count; index++)
            {
                Append(builder, extra.Items[index], depth + 1);
            }
        }
    }
}
