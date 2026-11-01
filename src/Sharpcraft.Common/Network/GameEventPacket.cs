// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Common.Network;

/// <summary>
/// The <c>game_event</c> play packet: an unsigned-byte event type and a float parameter
/// (docs/modules/weather-and-sky-color.md 6.3; reference ClientboundGameEventPacket).
/// </summary>
/// <param name="EventType">Event type id (see <see cref="GameEventPacketCodec"/> constants).</param>
/// <param name="Param">Event-specific float parameter.</param>
public sealed record GameEventPacket(byte EventType, float Param);

/// <summary>Parses the <c>game_event</c> packet and names the event types this client consumes.</summary>
public static class GameEventPacketCodec
{
    /// <summary>Start of rain; the server follows it with a RAIN_LEVEL_CHANGE carrying the real level.</summary>
    public const byte StartRaining = 1;

    /// <summary>End of rain; likewise followed by a RAIN_LEVEL_CHANGE.</summary>
    public const byte StopRaining = 2;

    /// <summary>Rain level change (0..1).</summary>
    public const byte RainLevelChange = 7;

    /// <summary>Thunder level change (0..1).</summary>
    public const byte ThunderLevelChange = 8;

    /// <summary>Reads a <c>game_event</c> packet body: VarInt id, unsigned-byte type, float param.</summary>
    public static DecodeStatus TryRead(ref PayloadReader reader, out GameEventPacket packet)
    {
        packet = null!;

        var status = reader.TryReadVarInt(out var id);
        if (status != DecodeStatus.Success)
        {
            return status;
        }

        if (id != PacketIds.PlayClientbound.GameEvent)
        {
            return DecodeStatus.Invalid;
        }

        if (reader.TryReadByte(out var eventType) != DecodeStatus.Success)
        {
            return DecodeStatus.Invalid;
        }

        if (reader.TryReadFloat(out var param) != DecodeStatus.Success)
        {
            return DecodeStatus.Invalid;
        }

        packet = new GameEventPacket(eventType, param);
        return DecodeStatus.Success;
    }
}
