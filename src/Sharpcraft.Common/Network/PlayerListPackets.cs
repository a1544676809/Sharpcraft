// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Common.Network;

/// <summary>Which fields a player info update carries.</summary>
/// <remarks>
/// The flags arrive as one number whose bits are the ordinal of the action, and each entry then carries exactly
/// the payloads for the bits that are set, in ordinal order. Reading them in the wrong order desynchronises the
/// rest of the packet, so the order here mirrors the reference exactly.
/// </remarks>
[Flags]
public enum PlayerListActions
{
    /// <summary>Nothing.</summary>
    None = 0,

    /// <summary>Name and profile properties.</summary>
    AddPlayer = 1 << 0,

    /// <summary>Chat session key; parsed only to advance the reader.</summary>
    InitializeChat = 1 << 1,

    /// <summary>Game mode.</summary>
    UpdateGameMode = 1 << 2,

    /// <summary>Whether the player appears in the list.</summary>
    UpdateListed = 1 << 3,

    /// <summary>Latency in milliseconds.</summary>
    UpdateLatency = 1 << 4,

    /// <summary>Display name; parsed only to advance the reader.</summary>
    UpdateDisplayName = 1 << 5,

    /// <summary>Sort order in the list.</summary>
    UpdateListOrder = 1 << 6,

    /// <summary>Whether the hat layer is shown.</summary>
    UpdateHat = 1 << 7
}

/// <summary>One player's update, with only the fields the packet actually carried.</summary>
/// <param name="Id">Profile id.</param>
/// <param name="Name">Name when the packet carried one.</param>
/// <param name="GameMode">Game mode when carried.</param>
/// <param name="Listed">Listed flag when carried.</param>
/// <param name="Latency">Latency in milliseconds when carried.</param>
/// <param name="ListOrder">Sort order when carried.</param>
/// <param name="ShowHat">Hat flag when carried.</param>
/// <param name="HasDisplayName">True when a display name was present, which needs component text to render.</param>
public readonly record struct PlayerInfoUpdate(
    Guid Id,
    string? Name,
    int? GameMode,
    bool? Listed,
    int? Latency,
    int? ListOrder,
    bool? ShowHat,
    bool HasDisplayName);

/// <summary>
/// Parsers for the player list packets (docs/modules/ui.md 13, M5 exit criteria).
/// </summary>
public static class PlayerListPackets
{
    /// <summary>Upper bound on entries per packet; a runaway count must not size an allocation.</summary>
    public const int MaxEntries = 1024;

    /// <summary>Reads a <c>player_info_update</c> body.</summary>
    /// <param name="body">Packet body without the id.</param>
    /// <param name="actions">Which fields the packet carries.</param>
    /// <param name="entries">One entry per player named in the packet.</param>
    public static DecodeStatus TryReadPlayerInfoUpdate(
        ReadOnlySpan<byte> body,
        out PlayerListActions actions,
        out PlayerInfoUpdate[] entries)
    {
        actions = PlayerListActions.None;
        entries = [];

        var reader = new PayloadReader(body);

        if (reader.TryReadVarInt(out var mask) != DecodeStatus.Success)
        {
            return DecodeStatus.NeedMoreData;
        }

        actions = (PlayerListActions)mask;

        if (reader.TryReadVarInt(out var count) != DecodeStatus.Success)
        {
            return DecodeStatus.NeedMoreData;
        }

        if (count < 0 || count > MaxEntries)
        {
            return DecodeStatus.Invalid;
        }

        var result = new PlayerInfoUpdate[count];

        for (var index = 0; index < count; index++)
        {
            if (!TryReadEntry(ref reader, body, actions, out result[index]))
            {
                return DecodeStatus.NeedMoreData;
            }
        }

        entries = result;
        return DecodeStatus.Success;
    }

    /// <summary>Reads a <c>player_info_remove</c> body.</summary>
    /// <param name="body">Packet body without the id.</param>
    /// <param name="ids">Profile ids the packet removes.</param>
    public static DecodeStatus TryReadPlayerInfoRemove(ReadOnlySpan<byte> body, out Guid[] ids)
    {
        ids = [];
        var reader = new PayloadReader(body);

        if (reader.TryReadVarInt(out var count) != DecodeStatus.Success)
        {
            return DecodeStatus.NeedMoreData;
        }

        if (count < 0 || count > MaxEntries)
        {
            return DecodeStatus.Invalid;
        }

        var result = new Guid[count];

        for (var index = 0; index < count; index++)
        {
            if (reader.TryReadUuid(out result[index]) != DecodeStatus.Success)
            {
                return DecodeStatus.NeedMoreData;
            }
        }

        ids = result;
        return DecodeStatus.Success;
    }

    private static bool TryReadEntry(
        ref PayloadReader reader,
        ReadOnlySpan<byte> body,
        PlayerListActions actions,
        out PlayerInfoUpdate entry)
    {
        entry = default;

        if (reader.TryReadUuid(out var id) != DecodeStatus.Success)
        {
            return false;
        }

        string? name = null;
        var hasDisplayName = false;
        int? gameMode = null;
        bool? listed = null;
        int? latency = null;
        int? listOrder = null;
        bool? showHat = null;

        if (actions.HasFlag(PlayerListActions.AddPlayer))
        {
            if (reader.TryReadString(out var profileName, maxLength: 16) != DecodeStatus.Success ||
                !SkipProfileProperties(ref reader))
            {
                return false;
            }

            name = profileName;
        }

        if (actions.HasFlag(PlayerListActions.InitializeChat) && !SkipChatSession(ref reader))
        {
            return false;
        }

        if (actions.HasFlag(PlayerListActions.UpdateGameMode))
        {
            if (reader.TryReadVarInt(out var mode) != DecodeStatus.Success)
            {
                return false;
            }

            gameMode = mode;
        }

        if (actions.HasFlag(PlayerListActions.UpdateListed))
        {
            if (reader.TryReadBool(out var isListed) != DecodeStatus.Success)
            {
                return false;
            }

            listed = isListed;
        }

        if (actions.HasFlag(PlayerListActions.UpdateLatency))
        {
            if (reader.TryReadVarInt(out var ping) != DecodeStatus.Success)
            {
                return false;
            }

            latency = ping;
        }

        if (actions.HasFlag(PlayerListActions.UpdateDisplayName))
        {
            if (reader.TryReadBool(out var present) != DecodeStatus.Success)
            {
                return false;
            }

            if (present && !SkipNetworkComponent(ref reader, body))
            {
                return false;
            }

            hasDisplayName = present;
        }

        if (actions.HasFlag(PlayerListActions.UpdateListOrder))
        {
            if (reader.TryReadVarInt(out var order) != DecodeStatus.Success)
            {
                return false;
            }

            listOrder = order;
        }

        if (actions.HasFlag(PlayerListActions.UpdateHat))
        {
            if (reader.TryReadBool(out var hat) != DecodeStatus.Success)
            {
                return false;
            }

            showHat = hat;
        }

        entry = new PlayerInfoUpdate(id, name, gameMode, listed, latency, listOrder, showHat, hasDisplayName);
        return true;
    }

    /// <summary>Skips the profile property map, which is a count followed by named values.</summary>
    private static bool SkipProfileProperties(ref PayloadReader reader)
    {
        if (reader.TryReadVarInt(out var count) != DecodeStatus.Success || count < 0 || count > 64)
        {
            return false;
        }

        for (var index = 0; index < count; index++)
        {
            if (reader.TryReadString(out _, maxLength: 64) != DecodeStatus.Success ||
                reader.TryReadString(out _, maxLength: 4096) != DecodeStatus.Success ||
                reader.TryReadBool(out var signed) != DecodeStatus.Success)
            {
                return false;
            }

            if (signed && reader.TryReadString(out _, maxLength: 4096) != DecodeStatus.Success)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Skips a chat session: a session id, an expiry, and two byte arrays.</summary>
    private static bool SkipChatSession(ref PayloadReader reader) =>
        reader.TryReadUuid(out _) == DecodeStatus.Success &&
        reader.TryReadLong(out _) == DecodeStatus.Success &&
        reader.TryReadByteArray(out _, maxLength: 1024) == DecodeStatus.Success &&
        reader.TryReadByteArray(out _, maxLength: 4096) == DecodeStatus.Success;

    /// <summary>Skips a display name, which is a network NBT tag read and discarded for now.</summary>
    private static bool SkipNetworkComponent(ref PayloadReader reader, ReadOnlySpan<byte> body)
    {
        // The component is a network NBT tag. Rendering it needs component text, which the interface does not have
        // yet, so it is measured and skipped rather than decoded.
        var status = Nbt.NbtReader.TryReadNetworkRoot(body[reader.Position..], out _, out var consumed);

        if (status != DecodeStatus.Success || consumed <= 0)
        {
            return false;
        }

        reader.Skip(consumed);
        return true;
    }
}
