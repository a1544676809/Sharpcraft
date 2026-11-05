// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Network;
using Xunit;

namespace Sharpcraft.Common.Tests.Network;

/// <summary>Decoders for small play clientbound packets (block_changed_ack, set_held_slot, set_experience).</summary>
public sealed class SimpleClientboundPacketsTests
{
    [Fact]
    public void BlockChangedAckReadsSequence()
    {
        Assert.Equal(DecodeStatus.Success, PlayPackets.TryReadBlockChangedAck(new byte[] { 0x05 }, out var seq));
        Assert.Equal(5, seq);
    }

    [Fact]
    public void SetHeldSlotReadsSlot()
    {
        Assert.Equal(DecodeStatus.Success, PlayPackets.TryReadSetHeldSlot(new byte[] { 0x03 }, out var slot));
        Assert.Equal(3, slot);
    }

    [Fact]
    public void SetExperienceReadsProgressTotalLevel()
    {
        // float 0.5 = 0x3F000000 (big-endian), total VarInt 100 = 0x64, level 7 = 0x07.
        var body = new byte[] { 0x3F, 0x00, 0x00, 0x00, 0x64, 0x07 };
        Assert.Equal(DecodeStatus.Success, PlayPackets.TryReadSetExperience(body, out var progress, out var total, out var level));
        Assert.Equal(0.5f, progress);
        Assert.Equal(100, total);
        Assert.Equal(7, level);
    }
}
