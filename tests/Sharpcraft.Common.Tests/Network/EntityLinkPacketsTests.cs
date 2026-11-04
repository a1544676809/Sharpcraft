// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Network;
using Xunit;

namespace Sharpcraft.Common.Tests.Network;

/// <summary>set_entity_link: int source id + int destination id (0 = detach).</summary>
public sealed class EntityLinkPacketsTests
{
    [Fact]
    public void ReadsTheLeashLink()
    {
        byte[] body = [102, 0, 0, 0, 7, 0, 0, 0, 9];
        Assert.Equal(DecodeStatus.Success, EntityLinkPackets.TryRead(body, out var source, out var destination));
        Assert.Equal(7, source);
        Assert.Equal(9, destination);
    }

    [Fact]
    public void RejectsATruncatedBody()
    {
        Assert.NotEqual(DecodeStatus.Success, EntityLinkPackets.TryRead([102, 0, 0, 0], out _, out _));
    }
}
