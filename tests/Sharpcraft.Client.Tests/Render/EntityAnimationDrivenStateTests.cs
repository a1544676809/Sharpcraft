// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// The entity-event/tick plumbing that turns the batch-1 sandbox-only clips into hot-path animations
/// (docs/modules/entity-animation.md 4).
/// </summary>
public sealed class EntityAnimationDrivenStateTests
{
    [Theory]
    [InlineData(WardenModelDefinition.WardenEntityTypeId)]
    [InlineData(CreakingModelDefinition.CreakingEntityTypeId)]
    public void EventDrivenProvidersExposeTheirStateCapabilities(int typeId)
    {
        var provider = EntityAnimationRegistry.For(typeId);
        Assert.NotNull(provider);
        var state = provider!.CreateState();
        Assert.IsAssignableFrom<IEntityEventAnimationState>(state);
        Assert.IsAssignableFrom<IEntityTickAnimationState>(state);
    }

    [Fact]
    public void WardenAttackEventStartsTheAttackClipOnTheHotPath()
    {
        var provider = EntityAnimationRegistry.For(WardenModelDefinition.WardenEntityTypeId)!;
        var state = provider.CreateState();
        ((IEntityEventAnimationState)state).OnEntityEvent(4, gameTime: 100);

        // 4 ticks after the event = 0.2 s into the 0.333 s attack clip.
        var frame = new EntityAnimationFrame(
            WardenModelDefinition.WardenEntityTypeId,
            AgeInTicks: 104f,
            WalkAnimationPos: 0f,
            WalkAnimationSpeed: 0f,
            Pitch: 0f,
            Yaw: 0f,
            Flap: 0f,
            FlapSpeed: 0f,
            TentacleAngle: 0f,
            IsResting: false,
            IsBaby: false,
            IsHidingInShell: false);

        var pose = new EntityPose();
        provider.Apply(state, in frame, pose);

        // The un-triggered state leaves the arm at bind pose (0); the attack swings it well below -0.5 rad.
        Assert.True(
            pose.Get("right_arm").Rotation.X < -0.5f,
            $"attack should swing the right arm, was {pose.Get("right_arm").Rotation.X}");
    }

    [Fact]
    public void WardenPoseChangeStartsTheEmergeClip()
    {
        var provider = EntityAnimationRegistry.For(WardenModelDefinition.WardenEntityTypeId)!;
        var state = provider.CreateState();
        ((IEntityPoseAnimationState)state).OnPoseChanged(13, gameTime: 100); // EMERGING

        var frame = Frame(WardenModelDefinition.WardenEntityTypeId, ageInTicks: 100f);
        var pose = new EntityPose();
        provider.Apply(state, in frame, pose);

        // Emerge at t=0 pushes the body deeply underground: posVec(0, -63, 0) -> (0, +63, 0).
        Assert.Equal(new Vector3(0f, 63f, 0f), pose.Get("body").Position);
    }

    [Fact]
    public void CreakingCanMoveFlagGatesTheWalkLoop()
    {
        var provider = EntityAnimationRegistry.For(CreakingModelDefinition.CreakingEntityTypeId)!;
        var state = provider.CreateState();
        var synced = (IEntitySyncedDataAnimationState)state;
        var frame = Frame(CreakingModelDefinition.CreakingEntityTypeId, ageInTicks: 0f, walkPos: 10f, walkSpeed: 1f);

        synced.OnSyncedData(16, 8, 0); // CAN_MOVE = false
        var locked = new EntityPose();
        provider.Apply(state, in frame, locked);
        Assert.Single(locked.Offsets); // only the head entry, no walk clip

        synced.OnSyncedData(16, 8, 1); // CAN_MOVE = true
        var moving = new EntityPose();
        provider.Apply(state, in frame, moving);
        Assert.True(moving.Offsets.Count > 1, "walk clip should add leg/body parts once movement is allowed");
    }

    [Fact]
    public void CreakingTearingDownDrivesTheDeathClip()
    {
        var provider = EntityAnimationRegistry.For(CreakingModelDefinition.CreakingEntityTypeId)!;
        var state = provider.CreateState();
        ((IEntitySyncedDataAnimationState)state).OnSyncedData(18, 8, 1); // IS_TEARING_DOWN = true
        ((IEntityTickAnimationState)state).OnTick(new EntityAnimationTickContext(5, 0f));

        var frame = Frame(CreakingModelDefinition.CreakingEntityTypeId, ageInTicks: 6f);
        var pose = new EntityPose();
        provider.Apply(state, in frame, pose);
        Assert.True(pose.Offsets.Count > 1, "death clip should animate more than the head");
    }

    private static EntityAnimationFrame Frame(int typeId, float ageInTicks, float walkPos = 0f, float walkSpeed = 0f) =>
        new(
            typeId,
            ageInTicks,
            walkPos,
            walkSpeed,
            0f,
            0f,
            0f,
            0f,
            0f,
            false,
            false,
            false,
            0f);
}
