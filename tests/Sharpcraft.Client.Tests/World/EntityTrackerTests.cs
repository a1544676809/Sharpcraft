// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.World;
using Sharpcraft.Common.Network;
using Xunit;

namespace Sharpcraft.Client.Tests.World;

/// <summary>
/// The entity table (docs/specs/network/entity-sync.md 5). The server is authoritative, so the tests pin how
/// deltas accumulate, when an absolute teleport wins, what happens to ids we never saw, and that a steady frame
/// allocates nothing.
/// </summary>
public sealed class EntityTrackerTests
{
    [Fact]
    public void DeltasAccumulateOnTheTargetAndAZeroAxisKeepsItsValue()
    {
        var tracker = new EntityTracker();
        tracker.Add(Entity(1, x: 10, y: 64, z: 10));

        // Only x and y moved: z must keep its value rather than collapse to zero.
        tracker.ApplyMove(new EntityMove(1, 1.0, 0, 0, HasPosition: true, 0f, 0f, HasRotation: false, OnGround: true));
        tracker.ApplyMove(new EntityMove(1, -0.5, 0.25, 0, HasPosition: true, 0f, 0f, HasRotation: false, OnGround: true));

        Assert.True(tracker.TryGet(1, out var entity));
        Assert.Equal(10.5, entity!.TargetX, 4);
        Assert.Equal(64.25, entity.TargetY, 4);
        Assert.Equal(10.0, entity.TargetZ, 4);
        Assert.Equal(2, tracker.MovesApplied);
    }

    [Fact]
    public void TheRenderedPositionApproachesTheTargetOverThreeTicks()
    {
        // Vanilla does not snap a remote entity to the reported position; it closes a third of the remaining
        // gap per tick, so the rendered entity trails the authoritative one by a few ticks.
        var tracker = new EntityTracker();
        tracker.Add(Entity(7, x: 0, y: 0, z: 0));
        tracker.ApplyMove(new EntityMove(7, 3.0, 0, 0, HasPosition: true, 0f, 0f, HasRotation: false, OnGround: true));

        Assert.True(tracker.TryGet(7, out var entity));
        Assert.Equal(0.0, entity!.X, 4);
        Assert.Equal(3.0, entity.TargetX, 4);

        tracker.Tick();
        Assert.Equal(1.0, entity.X, 4);   // a third of the way

        tracker.Tick();
        Assert.Equal(2.0, entity.X, 4);   // a third of what is left

        // The last step closes the remaining gap outright, so three ticks land exactly on the target.
        tracker.Tick();
        Assert.Equal(3.0, entity.X, 4);
        Assert.Equal(0, entity.LerpSteps);
    }

    [Fact]
    public void AnUpdateForAnUnknownEntityIsCountedInsteadOfThrown()
    {
        var tracker = new EntityTracker();

        var applied = tracker.ApplyMove(new EntityMove(99, 1, 1, 1, HasPosition: true, 0f, 0f, HasRotation: false, OnGround: false));

        Assert.False(applied);
        Assert.Equal(1, tracker.UnknownUpdates);
        Assert.Equal(0, tracker.Count);
    }

    [Fact]
    public void ATeleportOverridesUnlessTheAxisIsMarkedRelative()
    {
        var tracker = new EntityTracker();
        tracker.Add(Entity(2, x: 1, y: 2, z: 3));

        // x relative (bit 0), y and z absolute.
        tracker.ApplyTeleport(new EntityTeleport(2, 5, 70, -8, 0, 0, 0, 0f, 0f, Relatives: 0b001, OnGround: true));

        Assert.True(tracker.TryGet(2, out var entity));
        Assert.Equal(6.0, entity!.TargetX, 4);
        Assert.Equal(70.0, entity.TargetY, 4);
        Assert.Equal(-8.0, entity.TargetZ, 4);
        Assert.True(entity.OnGround);

        // Without the relative bit the same value replaces the coordinate instead of adding.
        tracker.ApplyTeleport(new EntityTeleport(2, 5, 70, -8, 0, 0, 0, 0f, 0f, Relatives: 0, OnGround: true));
        Assert.Equal(5.0, entity.TargetX, 4);
    }

    [Fact]
    public void RemovingEntitiesForgetsThem()
    {
        var tracker = new EntityTracker();
        tracker.Add(Entity(3, 0, 0, 0));
        tracker.Add(Entity(4, 0, 0, 0));

        var removed = tracker.Remove([3, 4, 5]);

        Assert.Equal(2, removed);
        Assert.Equal(0, tracker.Count);
        Assert.Equal(2, tracker.Removals);
    }

    [Fact]
    public void TheTableRefusesToGrowPastItsCap()
    {
        var tracker = new EntityTracker();
        for (var id = 0; id < EntityTracker.MaxTrackedEntities; id++)
        {
            Assert.True(tracker.Add(Entity(id, 0, 0, 0)));
        }

        Assert.False(tracker.Add(Entity(EntityTracker.MaxTrackedEntities + 1, 0, 0, 0)));
        Assert.Equal(1, tracker.OverflowDrops);
        Assert.Equal(EntityTracker.MaxTrackedEntities, tracker.Count);
    }

    [Fact]
    public void InterpolationUsesBothTicksAndTheShortArcForYaw()
    {
        var tracker = new EntityTracker();
        tracker.Add(Entity(5, x: 0, y: 0, z: 0, yaw: 350f));

        // Rotating from 350 to 10 must pass through 0 rather than the long way round.
        tracker.ApplyMove(new EntityMove(5, 4.0, 0, 0, HasPosition: true, 10f, 0f, HasRotation: true, OnGround: true));
        tracker.Tick();

        var atStart = tracker.Interpolate(0f)[0];
        var mid = tracker.Interpolate(0.5f)[0];
        var atEnd = tracker.Interpolate(1f)[0];

        // One tick moves a third of the way, and interpolation spans the previous and current tick.
        Assert.Equal(0f, atStart.X, 4);
        Assert.True(atEnd.X > 1.2f && atEnd.X < 1.4f, $"after one tick x was {atEnd.X}");
        Assert.True(mid.X > atStart.X && mid.X < atEnd.X);

        // The approach takes three ticks, so the yaw is still short of the target after one of them; what
        // matters is that it travelled the short way, staying near zero rather than swinging through 180.
        var midway = ((mid.Yaw % 360f) + 360f) % 360f;
        Assert.True(midway > 340f || midway < 20f, $"mid yaw was {midway}, expected the short arc near zero");

        tracker.Tick();
        tracker.Tick();
        var settled = ((tracker.Interpolate(1f)[0].Yaw % 360f) + 360f) % 360f;
        Assert.Equal(10f, settled, 1);
    }

    [Fact]
    public void InterpolationDoesNotAllocateOnceWarm()
    {
        var tracker = new EntityTracker();
        for (var id = 0; id < 64; id++)
        {
            tracker.Add(Entity(id, id, id, id));
        }

        // Warm past the point where tiered compilation replaces the method: a recompile installed in the middle of
        // the measured loop allocates once and looks like a regression that is not there.
        for (var warm = 0; warm < 32; warm++)
        {
            tracker.Tick();
            tracker.Interpolate(0.5f);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var frame = 0; frame < 64; frame++)
        {
            tracker.Tick();
            tracker.Interpolate(frame / 64f);
        }

        var after = GC.GetAllocatedBytesForCurrentThread();
        Assert.Equal(before, after);
    }

    private static AddedEntity Entity(int id, double x, double y, double z, float yaw = 0f) =>
        new(id, Guid.Empty, TypeId: 1, x, y, z, 0f, 0f, 0f, 0f, yaw, yaw, 0);
}
