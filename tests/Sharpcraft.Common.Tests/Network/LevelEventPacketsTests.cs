// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Network;
using Xunit;

namespace Sharpcraft.Common.Tests.Network;

/// <summary>Level event packet: int type, packed block position, int data, bool global.</summary>
public sealed class LevelEventPacketsTests
{
    [Fact]
    public void ReadsAJukeboxStartEvent()
    {
        byte[] body =
        [
            47,
            0, 0, 3, 242,           // type 1010
            0, 0, 0, 0, 0, 0, 0, 0, // packed block position 0,0,0
            0, 0, 0, 0,             // data 0
            0                       // not global
        ];

        Assert.Equal(DecodeStatus.Success, LevelEventPackets.TryRead(body, out var type, out var pos, out var data, out var global));
        Assert.Equal(1010, type);
        Assert.Equal(0, pos);
        Assert.Equal(0, data);
        Assert.False(global);
    }

    [Fact]
    public void RejectsATruncatedBody()
    {
        Assert.NotEqual(DecodeStatus.Success, LevelEventPackets.TryRead([47, 0, 0, 3], out _, out _, out _, out _));
    }
}
