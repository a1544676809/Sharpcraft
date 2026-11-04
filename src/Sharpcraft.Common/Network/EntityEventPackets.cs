// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Common.Network;

/// <summary>
/// Decodes the play <c>entity_event</c> packet (clientbound 34): a fixed big-endian entity id followed by one event
/// byte. Animations that the reference starts from <c>handleEntityEvent</c> (Warden attack/sonic charge/tendrils,
/// Creaking attack/invulnerability, ...) are driven through this packet.
/// </summary>
public static class EntityEventPackets
{
    /// <summary>Reads the entity id and event id from a packet body that includes the packet id.</summary>
    /// <param name="body">Packet body, including the packet id.</param>
    /// <param name="entityId">Runtime entity id.</param>
    /// <param name="eventId">Event id (reference <c>EntityEvent</c> constant).</param>
    public static DecodeStatus TryRead(ReadOnlySpan<byte> body, out int entityId, out int eventId)
    {
        entityId = 0;
        eventId = 0;

        var reader = new PayloadReader(body);
        var status = reader.TryReadVarInt(out var packetId);
        if (status != DecodeStatus.Success)
        {
            return status;
        }

        if (packetId != PacketIds.PlayClientbound.EntityEvent)
        {
            return DecodeStatus.Invalid;
        }

        status = reader.TryReadInt(out entityId);
        if (status != DecodeStatus.Success)
        {
            return status;
        }

        status = reader.TryReadByte(out var rawEvent);
        if (status != DecodeStatus.Success)
        {
            return status;
        }

        eventId = rawEvent;
        return DecodeStatus.Success;
    }
}
