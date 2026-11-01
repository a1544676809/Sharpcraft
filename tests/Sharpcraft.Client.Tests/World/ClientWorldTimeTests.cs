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
        world.AdvanceTick();
        Assert.Equal(6001, world.DayTime);
    }

    [Fact]
    public void RoutineEmptySetTimeDoesNotResetClocks()
    {
        // Routine set_time packets carry no clock updates; they must not clear the clocks (reference handleUpdates).
        // The clock advances by the game-time delta (1019 - 999 = 20), staying near 6000 instead of dropping to 0.
        var world = WithOverworldClock(6000);
        world.ApplyTime(new SetTimePacket(1019, new Dictionary<int, ClockNetworkState>()));
        Assert.Equal(6020, world.DayTime);
    }

    [Fact]
    public void TimeSetSurvivesFollowingEmptyPacketsAndAdvances()
    {
        var world = WithOverworldClock(0);
        world.ApplyTime(new SetTimePacket(1000, new Dictionary<int, ClockNetworkState>
        {
            [ClientWorld.OverworldClockId] = new ClockNetworkState(1000, 0f, 1f)
        }));
        Assert.Equal(1000, world.DayTime);

        // The next routine packet only advances game time by 20 ticks; the clock advances with it, it is not wiped.
        world.ApplyTime(new SetTimePacket(1020, new Dictionary<int, ClockNetworkState>()));
        Assert.Equal(1020, world.DayTime);
    }

    [Fact]
    public void NoClockIsDayZeroEvenWithGameTime()
    {
        // The reference derives day time only from the overworld clock, never from the raw game time. A server that sends
        // an empty clock map (only game time) therefore leaves day time at 0 until it sends a clock update.
        var world = new ClientWorld(minSectionY: 0, sectionCount: 1);
        world.ApplyTime(new SetTimePacket(18000, new Dictionary<int, ClockNetworkState>()));
        Assert.Equal(0, world.DayTime);

        world.AdvanceTick();
        Assert.Equal(0, world.DayTime);
    }

    [Fact]
    public void NoTimeIsDayZero()
    {
        var world = new ClientWorld(minSectionY: 0, sectionCount: 1);
        Assert.Equal(0, world.DayTime);
    }
}
