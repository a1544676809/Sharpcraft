// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.World;
using Sharpcraft.Common.Network;
using Xunit;

namespace Sharpcraft.Client.Tests.World;

/// <summary>Client world time model (docs/modules/time.md 4): dayTime from the overworld clock and clock advance.</summary>
public sealed class ClientWorldTimeTests
{
    private static ClientWorld WithOverworldClock(long totalTicks, float rate = 1f)
    {
        var world = new ClientWorld(minSectionY: 0, sectionCount: 1);
        world.ApplyTime(new SetTimePacket(999, new Dictionary<int, ClockNetworkState>
        {
            [ClientWorld.OverworldClockId] = new ClockNetworkState(totalTicks, 0f, rate)
        }));
        return world;
    }

    [Fact]
    public void DayTimeComesFromTheOverworldClock()
    {
        Assert.Equal(6000, WithOverworldClock(6000).DayTime);
        Assert.Equal(100, WithOverworldClock(24000 + 100).DayTime);
        Assert.Equal(23900, WithOverworldClock(-100).DayTime); // negative wraps into 0..23999
    }

    [Fact]
    public void GameTimeIsKept()
    {
        var world = new ClientWorld(minSectionY: 0, sectionCount: 1);
        world.ApplyTime(new SetTimePacket(424242, new Dictionary<int, ClockNetworkState>
        {
            [ClientWorld.OverworldClockId] = new ClockNetworkState(0, 0f, 1f)
        }));
        Assert.Equal(424242, world.GameTime);
    }

    [Fact]
    public void AdvanceClocksMovesByRate()
    {
        var world = WithOverworldClock(6000);
        world.AdvanceClocks();
        Assert.Equal(6001, world.DayTime);
    }

    [Fact]
    public void NoTimeIsDayZero()
    {
        var world = new ClientWorld(minSectionY: 0, sectionCount: 1);
        Assert.Equal(0, world.DayTime);
    }
}
