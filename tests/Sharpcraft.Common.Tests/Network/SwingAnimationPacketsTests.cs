// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Network;
using Xunit;

namespace Sharpcraft.Common.Tests.Network;

/// <summary>The clientbound <c>swing_animation</c> packet (id 123): entity id, hand, type, duration.</summary>
public sealed class SwingAnimationPacketsTests
{
    [Fact]
    public void ReadsTheSwingDescription()
    {
        // varint(123)=0x7B, entity 42=0x2A, hand 0, type 1 (whack), duration 6.
        byte[] body = [0x7B, 0x2A, 0x00, 0x01, 0x06];

        Assert.Equal(DecodeStatus.Success, SwingAnimationPackets.TryRead(body, out var entityId, out var hand, out var type, out var duration));
        Assert.Equal(42, entityId);
        Assert.Equal(0, hand);
        Assert.Equal(1, type);
        Assert.Equal(6, duration);
    }

    [Fact]
    public void RejectsADifferentPacketId()
    {
        byte[] body = [0x7C, 0x01, 0x00, 0x01, 0x06];

        Assert.Equal(DecodeStatus.Invalid, SwingAnimationPackets.TryRead(body, out _, out _, out _, out _));
    }

    [Fact]
    public void RejectsATruncatedBody()
    {
        byte[] body = [0x7B, 0x2A, 0x00];

        Assert.NotEqual(DecodeStatus.Success, SwingAnimationPackets.TryRead(body, out _, out _, out _, out _));
    }
}
