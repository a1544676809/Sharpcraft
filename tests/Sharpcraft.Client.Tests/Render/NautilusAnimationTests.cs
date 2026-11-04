// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>Nautilus swim animation ported from NautilusAnimation.SWIMMING and NautilusModel.setupAnim.</summary>
public sealed class NautilusAnimationTests
{
    private static float Rad(float degrees) => degrees * (MathF.PI / 180f);

    private static EntityPose Apply(KeyframeAnimation animation, float seconds, float scale = 1f)
    {
        var pose = new EntityPose();
        animation.Apply(seconds, scale, pose);
        return pose;
    }

    private static EntityAnimationFrame Frame(float age = 0f, float walkPos = 0f, float walkSpeed = 0f, float pitch = 0f, float yaw = 0f) =>
        new(
            NautilusModelDefinition.NautilusEntityTypeId,
            age,
            walkPos,
            walkSpeed,
            pitch,
            yaw,
            0f,
            0f,
            0f,
            false,
            false,
            false,
            0f);

    [Fact]
    public void SwimmingIsAOneSecondLoop()
    {
        Assert.Equal(1f, NautilusAnimations.Swimming.LengthInSeconds);
        Assert.True(NautilusAnimations.Swimming.Looping);
    }

    [Fact]
    public void MouthAndBodyKeyframesMatchTheReference()
    {
        var start = Apply(NautilusAnimations.SwimmingBaked, 0f);
        Assert.Equal(Vector3.Zero, start.Get("body").Scale);

        var mid = Apply(NautilusAnimations.SwimmingBaked, 0.5f);
        Assert.Equal(0.2f, mid.Get("body").Scale.Z, 4);
        Assert.Equal(Rad(30f), mid.Get("upper_mouth").Rotation.X, 4);
        Assert.Equal(Rad(-30f), mid.Get("lower_mouth").Rotation.X, 4);
        Assert.Equal(-0.2f, mid.Get("inner_mouth").Scale.X, 4);
        Assert.Equal(0.4f, mid.Get("upper_mouth").Scale.Z, 4);

        var squeeze = Apply(NautilusAnimations.SwimmingBaked, 0.75f);
        Assert.Equal(-0.1f, squeeze.Get("body").Scale.Z, 4);
    }

    [Fact]
    public void ProviderAppliesTheApplyWalkMappingAndClampsBodyRotation()
    {
        var provider = new NautilusAnimationProvider(NautilusModelDefinition.NautilusEntityTypeId);
        var pose = new EntityPose();

        // pos = 5, age = 0 -> seconds = 5 * 2 * 0.05 = 0.5; scale = min((0.1 + 0.2) * 3, 1) = 0.9.
        provider.Apply(provider.CreateState(), Frame(walkPos: 5f, walkSpeed: 0.1f), pose);
        Assert.Equal(0.2f * 0.9f, pose.Get("body").Scale.Z, 4);

        var clamped = new EntityPose();
        provider.Apply(provider.CreateState(), Frame(pitch: -50f, yaw: 50f), clamped);
        Assert.Equal(Rad(-10f), clamped.Get("body").Rotation.X, 4);
        Assert.Equal(Rad(10f), clamped.Get("body").Rotation.Y, 4);
    }

    [Fact]
    public void ApplyClipPlaysSwimming()
    {
        var provider = new NautilusAnimationProvider(NautilusModelDefinition.NautilusEntityTypeId);
        var pose = new EntityPose();
        provider.ApplyClip(provider.CreateState(), Frame(), "swimming", 0.5f, pose);
        Assert.Equal(0.2f, pose.Get("body").Scale.Z, 4);
    }
}
