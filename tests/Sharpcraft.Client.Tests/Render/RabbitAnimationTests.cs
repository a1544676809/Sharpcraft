// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Rabbit provider: the derivable head-look path plus the now-derivable HOP clip (sampled from JumpCompletion), while the
/// idle-head-tilt timer stays blocked. These tests pin the head look, the hop keyframes at fixed completion points, and
/// that no idle clip is faked.
/// </summary>
public sealed class RabbitAnimationTests
{
    private const float ToRad = MathF.PI / 180f;

    private static EntityAnimationFrame Frame(
        float pitch = 0f,
        float yaw = 0f,
        float age = 0f,
        float walkPos = 0f,
        float walkSpeed = 0f,
        bool isBaby = false,
        bool onGround = true,
        float jumpCompletion = 0f,
        bool isJumping = false) => new(
        TypeId: RabbitModelDefinition.RabbitEntityTypeId,
        AgeInTicks: age,
        WalkAnimationPos: walkPos,
        WalkAnimationSpeed: walkSpeed,
        Pitch: pitch,
        Yaw: yaw,
        Flap: 0f,
        FlapSpeed: 0f,
        TentacleAngle: 0f,
        IsResting: false,
        IsBaby: isBaby,
        IsHidingInShell: false,
        IsOnGround: onGround,
        JumpCompletion: jumpCompletion,
        IsJumping: isJumping);

    private static EntityPose Apply(in EntityAnimationFrame frame)
    {
        var provider = new RabbitAnimationProvider(RabbitModelDefinition.RabbitEntityTypeId);
        var pose = new EntityPose();
        provider.Apply(provider.CreateState(), in frame, pose);
        return pose;
    }

    private static void AssertVec(Vector3 expected, Vector3 actual, int precision = 4)
    {
        Assert.Equal(expected.X, actual.X, precision);
        Assert.Equal(expected.Y, actual.Y, precision);
        Assert.Equal(expected.Z, actual.Z, precision);
    }

    [Fact]
    public void ProviderAdvertisesTheClipsAndTypeId()
    {
        var provider = new RabbitAnimationProvider(RabbitModelDefinition.RabbitEntityTypeId);
        Assert.Equal(RabbitModelDefinition.RabbitEntityTypeId, provider.TypeId);
        Assert.Equal(new[] { "idle", "hop" }, provider.Clips);
        Assert.NotNull(provider.CreateState());
    }

    [Fact]
    public void HeadLooksAlongPitchAndYawWhenNotHopping()
    {
        var pose = Apply(Frame(pitch: 20f, yaw: -45f));
        Assert.Equal(20f * ToRad, pose.Get("head").Rotation.X, 5);
        Assert.Equal(-45f * ToRad, pose.Get("head").Rotation.Y, 5);
        Assert.Equal(0f, pose.Get("head").Rotation.Z, 5);
        AssertVec(Vector3.Zero, pose.Get("head").Position);
    }

    [Fact]
    public void HopDrivesBodyLegsTailAndEarsAtHalfJump()
    {
        // JumpCompletion 0.5 -> HOP age 0.375 s (an exact keyframe for several channels).
        var pose = Apply(Frame(jumpCompletion: 0.5f, isJumping: true));

        Assert.Equal(-34.58f * ToRad, pose.Get("head").Rotation.X, 4);
        Assert.Equal(120f * ToRad, pose.Get("backlegs").Rotation.X, 4);
        Assert.Equal(27.5f * ToRad, pose.Get("tail").Rotation.X, 4);
        Assert.Equal(-48.5f * ToRad, pose.Get("left_ear").Rotation.X, 4);
        Assert.Equal(-31.5f * ToRad, pose.Get("right_ear").Rotation.X, 4);
        // Ear position keyframes use PosVec (Y inverted): left (-0.02, -0.3) -> (-0.02, +0.3); right 0.02, 0.23.
        Assert.Equal(0.3f, pose.Get("left_ear").Position.Y, 4);
        Assert.Equal(0.23f, pose.Get("right_ear").Position.Y, 4);
    }

    [Fact]
    public void HopFrontLegZRotMatchesKeyframeAtExactlyOneThirdSecond()
    {
        // HOP age 0.3333 s -> JumpCompletion 0.3333 / 0.75.
        var pose = Apply(Frame(jumpCompletion: 0.3333f / 0.75f, isJumping: true));
        Assert.Equal(-17.5f * ToRad, pose.Get("right_front_leg").Rotation.Z, 4);
        Assert.Equal(20f * ToRad, pose.Get("left_front_leg").Rotation.Z, 4);
    }

    [Fact]
    public void HeadLookCombinesWithHop()
    {
        var pose = Apply(Frame(pitch: 10f, yaw: 20f, jumpCompletion: 0.5f, isJumping: true));
        Assert.Equal((10f + -34.58f) * ToRad, pose.Get("head").Rotation.X, 4);
        Assert.Equal(20f * ToRad, pose.Get("head").Rotation.Y, 5);
    }

    [Fact]
    public void IdleHeadTiltIsNotFakedWhenNotHopping()
    {
        // Not jumping: only the derivable head-look offset exists (no hop/idle body/leg/ear offsets).
        var pose = Apply(Frame(age: 40f, walkPos: 3f, walkSpeed: 1f, onGround: false));

        Assert.Equal(new HashSet<string>(StringComparer.Ordinal) { "head" }, pose.Offsets.Keys.ToHashSet(StringComparer.Ordinal));
        foreach (var part in new[] { "body", "tail", "backlegs", "frontlegs", "left_ear", "right_ear", "right_hind_leg", "left_hind_leg" })
        {
            AssertVec(Vector3.Zero, pose.Get(part).Rotation);
            AssertVec(Vector3.Zero, pose.Get(part).Position);
        }
    }

    [Fact]
    public void ApplyClipCanPlayTheHopDirectly()
    {
        var provider = new RabbitAnimationProvider(RabbitModelDefinition.RabbitEntityTypeId);
        var state = provider.CreateState();
        var frame = Frame();
        var pose = new EntityPose();

        provider.ApplyClip(state, in frame, "hop", 0.375f, pose);
        Assert.Equal(120f * ToRad, pose.Get("backlegs").Rotation.X, 4);
        Assert.Equal(27.5f * ToRad, pose.Get("tail").Rotation.X, 4);

        pose.Clear();
        provider.ApplyClip(state, in frame, "idle", 0f, pose);
        Assert.Equal(new HashSet<string>(StringComparer.Ordinal) { "head" }, pose.Offsets.Keys.ToHashSet(StringComparer.Ordinal));
    }

    [Fact]
    public void EveryAnimatedPartExistsInTheRabbitModel()
    {
        var modelParts = RabbitModelDefinition.Definition.Parts.Select(part => part.Name).ToHashSet(StringComparer.Ordinal);
        var pose = Apply(Frame(jumpCompletion: 0.5f, isJumping: true));

        Assert.NotEmpty(pose.Offsets);
        foreach (var partName in pose.Offsets.Keys)
        {
            Assert.Contains(partName, modelParts);
        }

        Assert.Contains("head", modelParts);
        Assert.Contains("backlegs", modelParts);
        Assert.Contains("frontlegs", modelParts);
        Assert.Contains("right_front_leg", modelParts);
        Assert.Contains("left_ear", modelParts);
        Assert.Contains("tail", modelParts);
    }
}
