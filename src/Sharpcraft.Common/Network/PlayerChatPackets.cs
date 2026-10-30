// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Localization;
using Sharpcraft.Common.Nbt;

namespace Sharpcraft.Common.Network;

/// <summary>
/// Reads <c>player_chat</c>, the packet that carries a message from another player (docs/modules/ui.md 8.4).
/// </summary>
/// <remarks>
/// The packet is mostly bookkeeping for signed chat: a global index, a per sender index, an optional signature, a
/// signed body with its last seen acknowledgements, an optional filter mask and the chat type binding. Only two of
/// those matter for showing the message: the body's content, and the display name carried by the chat type. The rest
/// still has to be measured exactly, because a single wrong length leaves the reader misaligned for every following
/// packet on the connection.
/// </remarks>
public static class PlayerChatPackets
{
    /// <summary>Chat type binding that needs a target name.</summary>
    public const int MaxChatTypes = 256;

    /// <summary>Upper bound on the acknowledged signatures a body may carry.</summary>
    public const int MaxLastSeen = 20;

    /// <summary>Reads a <c>player_chat</c> body.</summary>
    /// <param name="body">Packet body without the id.</param>
    /// <param name="senderName">Display name of the sender, already flattened.</param>
    /// <param name="text">Message text.</param>
    /// <param name="language">Translation table, or null to leave keys unresolved.</param>
    /// <param name="signed">
    /// True when the message carried a signature. An unsigned message comes from a server that does not sign chat, and
    /// the reference marks those lines as not secure, which is what decides whether they show an indicator bar.
    /// </param>
    public static DecodeStatus TryReadPlayerChat(
        ReadOnlySpan<byte> body,
        out TextRun[] senderName,
        out TextRun[] text,
        out bool signed,
        out int chatTypeId,
        out TextRun[] chatTarget,
        LanguageTable? language = null)
    {
        senderName = [];
        text = [];
        signed = false;
        chatTypeId = -1;
        chatTarget = [];

        // Temporary field tracing (docs/modules/ui.md 8.4): the chat type id came out one higher than the reference
        // resolved for the same message, so the field offsets are printed to find which read is off.
        var tracing = Environment.GetEnvironmentVariable("MC_DIAG") == "1";
        var trace = new System.Text.StringBuilder();
        var reader = new PayloadReader(body);

        if (reader.TryReadVarInt(out _) != DecodeStatus.Success ||          // global index
            reader.TryReadUuid(out _) != DecodeStatus.Success ||            // sender
            reader.TryReadVarInt(out _) != DecodeStatus.Success)            // per sender index
        {
            return DecodeStatus.NeedMoreData;
        }

        trace.Append($"head={reader.Position} ");

        if (!TryReadSignature(ref reader, out signed) || !TryReadBody(ref reader, out var content, out _))
        {
            return DecodeStatus.NeedMoreData;
        }

        trace.Append($"sig={signed} body={reader.Position} content='{content}' ");

        // An unsigned content overrides the body: it is what the server wants shown when it rewrote or filtered the
        // message, so preferring the body would show text the server deliberately replaced.
        if (reader.TryReadBool(out var hasUnsignedContent) != DecodeStatus.Success)
        {
            return DecodeStatus.NeedMoreData;
        }

        TextRun[]? unsignedContent = null;

        if (hasUnsignedContent)
        {
            var status = NbtReader.TryReadNetworkRoot(body[reader.Position..], out var component, out var consumed);

            if (status != DecodeStatus.Success || component is null)
            {
                return DecodeStatus.NeedMoreData;
            }

            unsignedContent = ComponentRuns.Flatten(component, language);
            reader.Skip(consumed);
        }

        trace.Append($"unsigned={hasUnsignedContent} afterUnsigned={reader.Position} ");

        if (!SkipFilterMask(ref reader))
        {
            return DecodeStatus.NeedMoreData;
        }

        if (reader.TryReadVarInt(out var chatType) != DecodeStatus.Success ||
            chatType < 0 || chatType > MaxChatTypes ||
            NbtReader.TryReadNetworkRoot(body[reader.Position..], out var nameComponent, out var nameLength) !=
                DecodeStatus.Success ||
            nameComponent is null)
        {
            return DecodeStatus.NeedMoreData;
        }

        trace.Append($"afterFilter={reader.Position} type={chatType} ");

        if (tracing)
        {
            Console.WriteLine("player chat fields: " + trace.ToString());
        }

        chatTypeId = chatType;
        senderName = ComponentRuns.Flatten(nameComponent, language);
        reader.Skip(nameLength);

        // The binding ends with an optional target name, which matters only for messages aimed at one player.
        if (reader.TryReadBool(out var hasTarget) != DecodeStatus.Success)
        {
            return DecodeStatus.NeedMoreData;
        }

        if (hasTarget)
        {
            if (NbtReader.TryReadNetworkRoot(body[reader.Position..], out var targetComponent, out var targetLength) !=
                DecodeStatus.Success)
            {
                return DecodeStatus.NeedMoreData;
            }

            chatTarget = targetComponent is null ? [] : ComponentRuns.Flatten(targetComponent, language);
            reader.Skip(targetLength);
        }

        text = unsignedContent ?? [new TextRun(content, ComponentRuns.DefaultColour, Bold: false)];
        return DecodeStatus.Success;
    }

    private static bool TryReadBody(ref PayloadReader reader, out string content, out long timestamp)
    {
        content = string.Empty;
        timestamp = 0;

        if (reader.TryReadString(out content, maxLength: 256) != DecodeStatus.Success ||
            reader.TryReadLong(out timestamp) != DecodeStatus.Success)
        {
            return false;
        }

        if (reader.TryReadLong(out _) != DecodeStatus.Success)             // salt
        {
            return false;
        }

        if (reader.TryReadVarInt(out var acknowledged) != DecodeStatus.Success ||
            acknowledged < 0 || acknowledged > MaxLastSeen)
        {
            return false;
        }

        for (var index = 0; index < acknowledged; index++)
        {
            if (reader.TryReadVarInt(out _) != DecodeStatus.Success ||      // packed signature id
                !TryReadSignature(ref reader, out _))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryReadSignature(ref PayloadReader reader, out bool present)
    {
        present = false;

        if (reader.TryReadBool(out var hasSignature) != DecodeStatus.Success)
        {
            return false;
        }

        present = hasSignature;

        return !hasSignature || reader.TryReadByteArray(out _, maxLength: 512) == DecodeStatus.Success;
    }

    private static bool SkipFilterMask(ref PayloadReader reader)
    {
        if (reader.TryReadVarInt(out var type) != DecodeStatus.Success || type < 0 || type > 2)
        {
            return false;
        }

        if (type != 2)
        {
            // Pass through and fully filtered carry nothing further.
            return true;
        }

        if (reader.TryReadVarInt(out var words) != DecodeStatus.Success || words < 0 || words > 8)
        {
            return false;
        }

        for (var index = 0; index < words; index++)
        {
            if (reader.TryReadLong(out _) != DecodeStatus.Success)
            {
                return false;
            }
        }

        return true;
    }
}
