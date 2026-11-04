// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Network;
using Xunit;

namespace Sharpcraft.Common.Tests.Network;

/// <summary>The clientbound <c>entity_event</c> packet (id 34): a fixed entity id then one event byte.</summary>
public sealed class EntityEventPacketsTests
{
    [Fact]
    public void ReadsTheEntityIdAndEventByte()
    {
        // varint(34)=0x22, int 1234 big-endian = 00 00 04 D2, event byte 0x3E (=62).
        byte[] body = [0x22, 0x00, 0x00, 0x04, 0xD2, 0x3E];

        Assert.Equal(DecodeStatus.Success, EntityEventPackets.TryRead(body, out var entityId, out var eventId));
        Assert.Equal(1234, entityId);
        Assert.Equal(62, eventId);
    }

    [Fact]
    public void RejectsADifferentPacketId()
    {
        byte[] body = [0x23, 0x00, 0x00, 0x00, 0x01, 0x04];

        Assert.Equal(DecodeStatus.Invalid, EntityEventPackets.TryRead(body, out _, out _));
    }

    [Fact]
    public void RejectsATruncatedBody()
    {
        byte[] body = [0x22, 0x00, 0x00, 0x04];

        Assert.NotEqual(DecodeStatus.Success, EntityEventPackets.TryRead(body, out _, out _));
    }
}
