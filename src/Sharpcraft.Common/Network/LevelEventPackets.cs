// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Common.Network;

/// <summary>
/// Decodes the play <c>level_event</c> packet (clientbound 47): int event type, packed block position, int data and a
/// global-event flag. Jukebox song start/stop are level event types 1010/1011 and drive parrot party state.
/// </summary>
public static class LevelEventPackets
{
    /// <summary>Reads a level event from a packet body that includes the packet id.</summary>
    public static DecodeStatus TryRead(ReadOnlySpan<byte> body, out int type, out long packedPosition, out int data, out bool global)
    {
        type = 0;
        packedPosition = 0;
        data = 0;
        global = false;

        var reader = new PayloadReader(body);
        if (reader.TryReadVarInt(out var packetId) != DecodeStatus.Success)
        {
            return DecodeStatus.NeedMoreData;
        }

        if (packetId != PacketIds.PlayClientbound.LevelEvent)
        {
            return DecodeStatus.Invalid;
        }

        if (reader.TryReadInt(out type) != DecodeStatus.Success ||
            reader.TryReadLong(out packedPosition) != DecodeStatus.Success ||
            reader.TryReadInt(out data) != DecodeStatus.Success ||
            reader.TryReadBool(out global) != DecodeStatus.Success)
        {
            return DecodeStatus.NeedMoreData;
        }

        return DecodeStatus.Success;
    }
}
