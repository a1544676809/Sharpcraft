// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Common.Network;

/// <summary>Decodes the play <c>set_entity_link</c> packet (clientbound 102): source id + destination id (0 = detach).</summary>
public static class EntityLinkPackets
{
    public static DecodeStatus TryRead(ReadOnlySpan<byte> body, out int sourceId, out int destinationId)
    {
        sourceId = 0;
        destinationId = 0;

        var reader = new PayloadReader(body);
        if (reader.TryReadVarInt(out var packetId) != DecodeStatus.Success)
        {
            return DecodeStatus.NeedMoreData;
        }

        if (packetId != PacketIds.PlayClientbound.SetEntityLink)
        {
            return DecodeStatus.Invalid;
        }

        if (reader.TryReadInt(out sourceId) != DecodeStatus.Success ||
            reader.TryReadInt(out destinationId) != DecodeStatus.Success)
        {
            return DecodeStatus.NeedMoreData;
        }

        return DecodeStatus.Success;
    }
}
