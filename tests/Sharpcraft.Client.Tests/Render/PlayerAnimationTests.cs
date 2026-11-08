// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// The player drove no animation at all before this provider existed: <c>EntityAnimationRegistry</c> only listed the
/// humanoid mobs, so a player stood in a fixed T-ish pose while walking and attacking.
/// </summary>
public sealed class PlayerAnimationTests
{
    private static EntityAnimationFrame Frame(
        float walkPosition = 0f,
        float walkSpeed = 0f,
        float swing = 0f,
        int mainHandItemId = 0) =>
        new(
            TypeId: PlayerAnimationProvider.PlayerEntityTypeId,
            AgeInTicks: 10f,
            WalkAnimationPos: walkPosition,
            WalkAnimationSpeed: walkSpeed,
            Pitch: 0f,
            Yaw: 0f,
            Flap: 0f,
            FlapSpeed: 0f,
            TentacleAngle: 0f,
            IsResting: false,
            IsBaby: false,
            IsHidingInShell: false,
            SwingAnimation: swing,
            MainHandItemId: mainHandItemId);

    [Fact]
    public void ThePlayerTypeHasAnAnimationProvider()
    {
        Assert.True(EntityAnimationRegistry.TryGet(PlayerAnimationProvider.PlayerEntityTypeId, out var provider));
        Assert.IsType<PlayerAnimationProvider>(provider);
    }

    [Fact]
    public void WalkingUsesTheReferenceSwingAmplitudes()
    {
        // Reference HumanoidModel.setupAnim:
        //   rightArm.xRot = cos(pos * 0.6662 + PI) * 2.0 * speed * 0.5 / speedValue
        //   leftArm.xRot  = cos(pos * 0.6662)        * 2.0 * speed * 0.5 / speedValue
        //   rightLeg.xRot = cos(pos * 0.6662)        * 1.4 * speed / speedValue
        //   leftLeg.xRot  = cos(pos * 0.6662 + PI)   * 1.4 * speed / speedValue
        // with speedValue = max(1, state.speedValue), plus AnimationUtils.bobArms adding
        //   xRot ± sin(age * 0.067) * 0.05 and zRot ± (cos(age * 0.09) * 0.05 + 0.05).
        var provider = new PlayerAnimationProvider();
        var pose = new EntityPose();
        var frame = Frame(walkPosition: 1f, walkSpeed: 1f);
        provider.Apply(provider.CreateState(), in frame, pose);

        var cos = MathF.Cos(0.6662f);
        var bobX = MathF.Sin(10f * 0.067f) * 0.05f;
        var bobZ = (MathF.Cos(10f * 0.09f) * 0.05f) + 0.05f;

        Assert.Equal(cos * 1.4f, pose.Get("right_leg").Rotation.X, 4);
        Assert.Equal(-cos * 1.4f, pose.Get("left_leg").Rotation.X, 4);
        Assert.Equal(-cos + bobX, pose.Get("right_arm").Rotation.X, 4);
        Assert.Equal(cos - bobX, pose.Get("left_arm").Rotation.X, 4);
        Assert.Equal(bobZ, pose.Get("right_arm").Rotation.Z, 4);
        Assert.Equal(-bobZ, pose.Get("left_arm").Rotation.Z, 4);
        // The reference also gives the legs a tiny splay on Y and Z.
        Assert.Equal(0.005f, pose.Get("right_leg").Rotation.Y, 5);
        Assert.Equal(-0.005f, pose.Get("left_leg").Rotation.Y, 5);
    }

    [Fact]
    public void StandingStillLeavesTheLegsAtRestAndOnlyBobsTheArms()
    {
        var provider = new PlayerAnimationProvider();
        var pose = new EntityPose();
        var frame = Frame();
        provider.Apply(provider.CreateState(), in frame, pose);

        Assert.Equal(0f, pose.Get("right_leg").Rotation.X, 5);
        Assert.Equal(0f, pose.Get("left_leg").Rotation.X, 5);
        // bobArms still runs while idle, so the arms are never exactly zero.
        var bobX = MathF.Sin(10f * 0.067f) * 0.05f;
        Assert.Equal(bobX, pose.Get("right_arm").Rotation.X, 5);
        Assert.Equal(-bobX, pose.Get("left_arm").Rotation.X, 5);
    }

    [Fact]
    public void AttackingMovesTheArmButNotTheLegs()
    {
        var provider = new PlayerAnimationProvider();
        var pose = new EntityPose();
        var frame = Frame(swing: 0.5f);
        provider.Apply(provider.CreateState(), in frame, pose);

        Assert.NotEqual(0f, pose.Get("right_arm").Rotation.X);
        Assert.Equal(0f, pose.Get("right_leg").Rotation.X, 5);
    }

    [Fact]
    public void TheHeadTracksPitchAndYaw()
    {
        var provider = new PlayerAnimationProvider();
        var pose = new EntityPose();
        var frame = Frame() with { Pitch = 20f, HeadYaw = 30f };
        provider.Apply(provider.CreateState(), in frame, pose);

        Assert.NotEqual(0f, pose.Get("head").Rotation.X);
        Assert.NotEqual(0f, pose.Get("head").Rotation.Y);
    }
}
