// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Localization;
using Sharpcraft.Common.Nbt;

namespace Sharpcraft.Common.Network;

/// <summary>
/// Reads the packets that carry chat which is not a signed player message: a server-disguised message and the
/// instruction to delete one (docs/specs/network/protocol-phases.md).
/// </summary>
/// <remarks>
/// Both carry the same pieces the player chat packet does — a component and a chat type binding — so they reuse the
/// same decoration path rather than inventing a second one. The difference is only what the message <em>is</em>: a
/// disguised message comes from the server on a player's behalf, which is what <c>/me</c> produces.
/// </remarks>
public static class DisguisedChatPackets
{
    /// <summary>Upper bound on a chat type id, used to reject nonsense before the registry is consulted.</summary>
    public const int MaxChatTypes = 256;

    /// <summary>
    /// Reads a disguised chat message.
    /// </summary>
    /// <param name="body">Packet body.</param>
    /// <param name="message">Message contents, already flattened, or empty on failure.</param>
    /// <param name="chatTypeId">Chat type id, or -1 on failure.</param>
    /// <param name="senderName">Name the chat type binding carries.</param>
    /// <param name="target">Target of a direct message, empty when there is none.</param>
    /// <param name="language">Translation table, or null to leave keys unresolved.</param>
    public static DecodeStatus TryRead(
        ReadOnlySpan<byte> body,
        out TextRun[] message,
        out int chatTypeId,
        out TextRun[] senderName,
        out TextRun[] target,
        LanguageTable? language = null)
    {
        message = [];
        chatTypeId = -1;
        senderName = [];
        target = [];

        var reader = new PayloadReader(body);

        if (NbtReader.TryReadNetworkRoot(body[reader.Position..], out var content, out var contentLength) !=
                DecodeStatus.Success ||
            content is null)
        {
            return DecodeStatus.NeedMoreData;
        }

        message = ComponentRuns.Flatten(content, language);
        reader.Skip(contentLength);

        if (!ChatTypeBinding.TryRead(ref reader, body, out chatTypeId, out senderName, out target, language))
        {
            return DecodeStatus.NeedMoreData;
        }

        return DecodeStatus.Success;
    }
}

/// <summary>
/// Reads the binding that names a chat type and carries the names its decoration may use.
/// </summary>
/// <remarks>
/// The same shape ends a player chat packet, a disguised chat packet and a message deletion is only different in what
/// it matches on, so the reading lives here rather than being copied three times.
/// </remarks>
public static class ChatTypeBinding
{
    /// <summary>Reads the binding at the reader's position.</summary>
    /// <param name="reader">Reader positioned at the binding.</param>
    /// <param name="body">Body the reader reads from, needed for the nested components.</param>
    /// <param name="chatTypeId">Chat type id.</param>
    /// <param name="senderName">Name component, flattened.</param>
    /// <param name="target">Optional target component, flattened, or empty.</param>
    /// <param name="language">Translation table, or null.</param>
    public static bool TryRead(
        ref PayloadReader reader,
        ReadOnlySpan<byte> body,
        out int chatTypeId,
        out TextRun[] senderName,
        out TextRun[] target,
        LanguageTable? language = null)
    {
        chatTypeId = -1;
        senderName = [];
        target = [];

        if (reader.TryReadVarInt(out var id) != DecodeStatus.Success || id < 0 || id > DisguisedChatPackets.MaxChatTypes)
        {
            return false;
        }

        if (NbtReader.TryReadNetworkRoot(body[reader.Position..], out var nameComponent, out var nameLength) !=
                DecodeStatus.Success ||
            nameComponent is null)
        {
            return false;
        }

        chatTypeId = id;
        senderName = ComponentRuns.Flatten(nameComponent, language);
        reader.Skip(nameLength);

        if (reader.TryReadBool(out var hasTarget) != DecodeStatus.Success)
        {
            return false;
        }

        if (!hasTarget)
        {
            return true;
        }

        if (NbtReader.TryReadNetworkRoot(body[reader.Position..], out var targetComponent, out var targetLength) !=
            DecodeStatus.Success)
        {
            return false;
        }

        target = targetComponent is null ? [] : ComponentRuns.Flatten(targetComponent, language);
        reader.Skip(targetLength);
        return true;
    }
}

/// <summary>
/// Reads the packet that asks the client to delete a message it was shown.
/// </summary>
/// <remarks>
/// The instruction names the deleted message by its signature. Nothing is signed on a server that runs in offline
/// mode, so this packet cannot match anything there; the signature is still read so the packet is consumed correctly
/// and the gap is visible in the session log rather than being a silent no-op.
/// </remarks>
public static class DeleteChatPackets
{
    /// <summary>Longest signature accepted, in bytes.</summary>
    public const int MaxSignatureLength = 512;

    /// <summary>Reads the signature of the message to delete.</summary>
    /// <param name="body">Packet body.</param>
    /// <param name="signature">Signature bytes, or empty on failure.</param>
    public static DecodeStatus TryRead(ReadOnlySpan<byte> body, out byte[] signature)
    {
        signature = [];

        var reader = new PayloadReader(body);

        if (reader.TryReadByteArray(out var bytes, MaxSignatureLength) != DecodeStatus.Success)
        {
            return DecodeStatus.NeedMoreData;
        }

        signature = bytes;
        return DecodeStatus.Success;
    }
}
