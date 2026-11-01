// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Network;
using Xunit;

namespace Sharpcraft.Common.Tests.Network;

/// <summary>Parses the game_event packet (docs/modules/weather-and-sky-color.md 6.3): VarInt id, byte type, float param.</summary>
public sealed class GameEventPacketTests
{
    private const byte GameEventId = 39;

    [Fact]
    public void ReadsRainLevelChange()
    {
        // id 39 (one byte, <128), type 7 (RAIN_LEVEL_CHANGE), param 0.5f big-endian = 3F 00 00 00.
        var bytes = new byte[] { GameEventId, 7, 0x3F, 0x00, 0x00, 0x00 };
        var reader = new PayloadReader(bytes);

        var status = GameEventPacketCodec.TryRead(ref reader, out var packet);

        Assert.Equal(DecodeStatus.Success, status);
        Assert.Equal(7, packet.EventType);
        Assert.Equal(0.5f, packet.Param, 5);
    }

    [Fact]
    public void RejectsAnotherPacketId()
    {
        var reader = new PayloadReader(new byte[] { 40, 7, 0x3F, 0x00, 0x00, 0x00 });
        Assert.Equal(DecodeStatus.Invalid, GameEventPacketCodec.TryRead(ref reader, out _));
    }
}
