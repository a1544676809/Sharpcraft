// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Common.Network;

/// <summary>Network state of one world clock (docs/modules/time.md): total ticks, sub-tick partial, and rate.</summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.world.clock.ClockNetworkState", "ref://mc-26.3-analysis/src/net/minecraft/world/clock/ClockNetworkState.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "auto-resolved by name; verify")]
public readonly record struct ClockNetworkState(long TotalTicks, float PartialTick, float Rate);

/// <summary>
/// The <c>set_time</c> play packet: game time plus the state of each world clock keyed by registry id
/// (docs/modules/time.md §3). The overworld clock is id 0 (WorldClocks registers overworld first).
/// </summary>
public sealed record SetTimePacket(long GameTime, IReadOnlyDictionary<int, ClockNetworkState> Clocks);

/// <summary>Parses the <c>set_time</c> packet.</summary>
public static class SetTimePacketCodec
{
    /// <summary>Largest clock map accepted, so a malformed count cannot allocate unbounded.</summary>
    public const int MaxClocks = 64;

    /// <summary>Reads a <c>set_time</c> packet body.</summary>
    public static DecodeStatus TryRead(ref PayloadReader reader, out SetTimePacket packet)
    {
        packet = null!;

        var status = reader.TryReadVarInt(out var id);
        if (status != DecodeStatus.Success)
        {
            return status;
        }

        if (id != PacketIds.PlayClientbound.SetTime)
        {
            return DecodeStatus.Invalid;
        }

        if (reader.TryReadLong(out var gameTime) != DecodeStatus.Success)
        {
            return DecodeStatus.Invalid;
        }

        if (reader.TryReadVarInt(out var count) != DecodeStatus.Success || count < 0 || count > MaxClocks)
        {
            return DecodeStatus.Invalid;
        }

        var clocks = new Dictionary<int, ClockNetworkState>(count);

        for (var i = 0; i < count; i++)
        {
            if (reader.TryReadVarInt(out var clockId) != DecodeStatus.Success ||
                reader.TryReadVarLong(out var totalTicks) != DecodeStatus.Success ||
                reader.TryReadFloat(out var partialTick) != DecodeStatus.Success ||
                reader.TryReadFloat(out var rate) != DecodeStatus.Success)
            {
                return DecodeStatus.Invalid;
            }

            clocks[clockId] = new ClockNetworkState(totalTicks, partialTick, rate);
        }

        packet = new SetTimePacket(gameTime, clocks);
        return DecodeStatus.Success;
    }
}
