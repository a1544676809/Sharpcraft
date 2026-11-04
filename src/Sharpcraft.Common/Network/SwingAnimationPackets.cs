// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Common.Network;

/// <summary>
/// Decodes the play <c>swing_animation</c> packet (clientbound 123): entity id, hand, swing-animation type and duration.
/// It is the modern replacement for the old "swing arm" animate action and drives the arm-swing animation used by
/// armed mob render states.
/// </summary>
public static class SwingAnimationPackets
{
    /// <summary>Reads the swing description from a packet body that includes the packet id.</summary>
    /// <param name="body">Packet body, including the packet id.</param>
    /// <param name="entityId">Runtime entity id.</param>
    /// <param name="hand">Hand id (0 = main, 1 = off; reference <c>InteractionHand</c>).</param>
    /// <param name="animationType">Swing animation type id (0 none / 1 whack / 2 stab).</param>
    /// <param name="durationTicks">Swing duration in ticks.</param>
    public static DecodeStatus TryRead(ReadOnlySpan<byte> body, out int entityId, out int hand, out int animationType, out int durationTicks)
    {
        entityId = 0;
        hand = 0;
        animationType = 0;
        durationTicks = 0;

        var reader = new PayloadReader(body);
        var status = reader.TryReadVarInt(out var packetId);
        if (status != DecodeStatus.Success)
        {
            return status;
        }

        if (packetId != PacketIds.PlayClientbound.SwingAnimation)
        {
            return DecodeStatus.Invalid;
        }

        status = reader.TryReadVarInt(out entityId);
        if (status != DecodeStatus.Success)
        {
            return status;
        }

        status = reader.TryReadVarInt(out hand);
        if (status != DecodeStatus.Success)
        {
            return status;
        }

        status = reader.TryReadVarInt(out animationType);
        if (status != DecodeStatus.Success)
        {
            return status;
        }

        return reader.TryReadVarInt(out durationTicks);
    }
}
