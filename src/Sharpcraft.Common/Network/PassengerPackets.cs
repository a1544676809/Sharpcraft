// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Common.Network;

/// <summary>
/// Decodes the play <c>set_passengers</c> packet (clientbound 109): the vehicle's entity id, a passenger count, then
/// that many passenger entity ids. The client only needs it to know which entities are riding what.
/// </summary>
public static class PassengerPackets
{
    /// <summary>Upper bound on passengers, so a corrupt packet cannot grow the caller's span without limit.</summary>
    public const int MaxPassengers = 64;

    /// <summary>Reads the vehicle id and passenger ids from a packet body that includes the packet id.</summary>
    public static DecodeStatus TryRead(ReadOnlySpan<byte> body, out int vehicleId, Span<int> passengers, out int count)
    {
        vehicleId = 0;
        count = 0;

        var reader = new PayloadReader(body);
        var status = reader.TryReadVarInt(out var packetId);
        if (status != DecodeStatus.Success)
        {
            return status;
        }

        if (packetId != PacketIds.PlayClientbound.SetPassengers)
        {
            return DecodeStatus.Invalid;
        }

        status = reader.TryReadVarInt(out vehicleId);
        if (status != DecodeStatus.Success)
        {
            return status;
        }

        status = reader.TryReadVarInt(out var declared);
        if (status != DecodeStatus.Success || declared < 0 || declared > passengers.Length)
        {
            return status != DecodeStatus.Success ? status : DecodeStatus.Invalid;
        }

        for (var i = 0; i < declared; i++)
        {
            status = reader.TryReadVarInt(out var passengerId);
            if (status != DecodeStatus.Success)
            {
                return status;
            }

            passengers[count++] = passengerId;
        }

        return DecodeStatus.Success;
    }
}
