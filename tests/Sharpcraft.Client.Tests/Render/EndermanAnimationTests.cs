// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>Reference <c>EndermanModel.setupAnim</c>: humanoid base plus long-limb xRot halving and clamping.</summary>
public sealed class EndermanAnimationTests
{
    private static readonly float Rad = MathF.PI / 180f;

    private static EntityPose Apply(in EntityAnimationFrame frame)
    {
        var provider = EntityAnimationRegistry.For(HumanoidMobModelDefinition.EndermanEntityTypeId);
        Assert.NotNull(provider);
        var pose = new EntityPose();
        provider!.Apply(provider.CreateState(), in frame, pose);
        return pose;
    }

    private static EntityAnimationFrame Frame(float pitch, float yaw, float headYaw, float walkPos, float walkSpeed) => new(
        TypeId: HumanoidMobModelDefinition.EndermanEntityTypeId, AgeInTicks: 4f, WalkAnimationPos: walkPos,
        WalkAnimationSpeed: walkSpeed, Pitch: pitch, Yaw: yaw, Flap: 0f, FlapSpeed: 0f, TentacleAngle: 0f,
        IsResting: false, IsBaby: false, IsHidingInShell: false, HeadYaw: headYaw);

    [Fact]
    public void TheLongLimbsAreHalvedAndClampedToPointFour()
    {
        var pose = Apply(Frame(pitch: 0f, yaw: 0f, headYaw: 0f, walkPos: 0f, walkSpeed: 1f));

        // Humanoid walk gives right_arm -1, left_arm 1, right_leg 1.4, left_leg -1.4; halved then clamped to +-0.4.
        Assert.Equal(-0.4f, pose.Get("right_arm").Rotation.X, 4);
        Assert.Equal(0.4f, pose.Get("left_arm").Rotation.X, 4);
        Assert.Equal(0.4f, pose.Get("right_leg").Rotation.X, 4);
        Assert.Equal(-0.4f, pose.Get("left_leg").Rotation.X, 4);
    }

    [Fact]
    public void TheHeadStillFollowsPitchAndHeadRelativeYaw()
    {
        var pose = Apply(Frame(pitch: 20f, yaw: 30f, headYaw: 50f, walkPos: 0f, walkSpeed: 0f));
        var head = pose.Get("head").Rotation;
        Assert.Equal(20f * Rad, head.X, 4);
        Assert.Equal(20f * Rad, head.Y, 4);
    }
}
