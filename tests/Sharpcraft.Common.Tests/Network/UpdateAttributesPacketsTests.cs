// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Network;
using Xunit;

namespace Sharpcraft.Common.Tests.Network;

/// <summary>update_attributes: entity id + (attribute id, base, modifiers); extracts SCALE.</summary>
public sealed class UpdateAttributesPacketsTests
{
    [Fact]
    public void ReadsTheScaleAttribute()
    {
        Assert.Equal(30, Sharpcraft.Common.Registries.AttributeTypes.GetId("scale"));
        // id 134, entity 7, one attribute: scale (30), base 2.0 (double), zero modifiers.
        byte[] body =
        [
            0x86, 0x01,                // VarInt 134
            7,
            1,                          // one attribute
            30,                         // scale attribute id
            0x40, 0, 0, 0, 0, 0, 0, 0,  // double 2.0
            0                           // no modifiers
        ];

        Assert.Equal(DecodeStatus.Success, UpdateAttributesPackets.TryRead(body, out var entityId, out var scale));
        Assert.Equal(7, entityId);
        Assert.Equal(2f, scale, 5);
    }

    [Fact]
    public void RejectsATruncatedBody()
    {
        Assert.NotEqual(DecodeStatus.Success, UpdateAttributesPackets.TryRead([134, 1, 1, 30], out _, out _));
    }
}
