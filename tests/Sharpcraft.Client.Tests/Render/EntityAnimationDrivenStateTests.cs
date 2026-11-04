// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

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
}
