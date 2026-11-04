// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Rabbit provider: head-look fallback, the HOP clip from JumpCompletion, and the IDLE_HEAD_TILT clip from the per-entity
/// idle clock. These tests pin keyframes for each clip and the start/stop gating (head look is suppressed while idle is
/// active, matching the reference).
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
        bool isJumping = false,
        bool idleStarted = false,
        float idleAgeTicks = 0f) => new(
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
        IsJumping: isJumping,
        IsIdleHeadTiltStarted: idleStarted,
        IdleHeadTiltAgeTicks: idleAgeTicks);

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
        Assert.Equal(new[] { "idle", "hop", "idle_head_tilt" }, provider.Clips);
        Assert.NotNull(provider.CreateState());
    }

    [Fact]
    public void HeadLooksAlongPitchAndYawWhenNotIdleOrHopping()
    {
        var pose = Apply(Frame(pitch: 20f, yaw: -45f));
        Assert.Equal(20f * ToRad, pose.Get("head").Rotation.X, 5);
        Assert.Equal(-45f * ToRad, pose.Get("head").Rotation.Y, 5);
        AssertVec(Vector3.Zero, pose.Get("head").Position);
    }

    [Fact]
    public void HopDrivesBodyLegsTailAndEarsAtHalfJump()
    {
        var pose = Apply(Frame(jumpCompletion: 0.5f, isJumping: true));

        Assert.Equal(-34.58f * ToRad, pose.Get("head").Rotation.X, 4);
        Assert.Equal(120f * ToRad, pose.Get("backlegs").Rotation.X, 4);
        Assert.Equal(27.5f * ToRad, pose.Get("tail").Rotation.X, 4);
        Assert.Equal(-48.5f * ToRad, pose.Get("left_ear").Rotation.X, 4);
        Assert.Equal(-31.5f * ToRad, pose.Get("right_ear").Rotation.X, 4);
        Assert.Equal(0.3f, pose.Get("left_ear").Position.Y, 4);
        Assert.Equal(0.23f, pose.Get("right_ear").Position.Y, 4);
    }

    [Fact]
    public void HopFrontLegZRotMatchesKeyframe()
    {
        var pose = Apply(Frame(jumpCompletion: 0.3333f / 0.75f, isJumping: true));
        Assert.Equal(-17.5f * ToRad, pose.Get("right_front_leg").Rotation.Z, 4);
        Assert.Equal(20f * ToRad, pose.Get("left_front_leg").Rotation.Z, 4);
    }

    [Fact]
    public void IdleHeadTiltDrivesBodyAndHeadAndSuppressesLook()
    {
        // Idle started at age 0, sampled at 0 -> body/head keyframe 0. Pitch/yaw must be suppressed.
        var suppressed = Apply(Frame(pitch: 20f, yaw: -45f, idleStarted: true, idleAgeTicks: 0f));
        Assert.Equal(0f, suppressed.Get("head").Rotation.X, 5);
        Assert.Equal(0f, suppressed.Get("head").Rotation.Y, 5);

        // 5 ticks = 0.25 s: body rotationX keyframe -62°, head rotationX 54.5°, head position z -2.5.
        var quarter = Apply(Frame(idleStarted: true, idleAgeTicks: 5f));
        Assert.Equal(-62f * ToRad, quarter.Get("body").Rotation.X, 4);
        Assert.Equal(54.5f * ToRad, quarter.Get("head").Rotation.X, 4);
        Assert.Equal(-2.5f, quarter.Get("head").Position.Z, 4);
    }

    [Fact]
    public void IdleHeadTiltBodyPositionKeyframe()
    {
        // 0.1667 s = 3.334 ticks: body position posVec(0, 0.7, 0) -> y-up storage (0, -0.7, 0).
        var pose = Apply(Frame(idleStarted: true, idleAgeTicks: 3.334f));
        Assert.Equal(-0.7f, pose.Get("body").Position.Y, 4);
    }

    [Fact]
    public void IdleSuppressesLookButHopStillApplies()
    {
        // Idle active suppresses head look; hop is not active here, so the only head rotation is the idle clip's.
        var idleOnly = Apply(Frame(pitch: 30f, idleStarted: true, idleAgeTicks: 0f));
        Assert.Equal(0f, idleOnly.Get("head").Rotation.X, 5);

        // Not idle: look applies.
        var look = Apply(Frame(pitch: 30f));
        Assert.Equal(30f * ToRad, look.Get("head").Rotation.X, 5);
    }

    [Fact]
    public void ApplyClipPlaysHopAndIdleHeadTiltDirectly()
    {
        var provider = new RabbitAnimationProvider(RabbitModelDefinition.RabbitEntityTypeId);
        var state = provider.CreateState();
        var frame = Frame();
        var pose = new EntityPose();

        provider.ApplyClip(state, in frame, "hop", 0.375f, pose);
        Assert.Equal(120f * ToRad, pose.Get("backlegs").Rotation.X, 4);

        pose.Clear();
        provider.ApplyClip(state, in frame, "idle_head_tilt", 0.25f, pose);
        Assert.Equal(-62f * ToRad, pose.Get("body").Rotation.X, 4);
        Assert.Equal(54.5f * ToRad, pose.Get("head").Rotation.X, 4);
    }

    [Fact]
    public void EveryAnimatedPartExistsInTheRabbitModel()
    {
        var modelParts = RabbitModelDefinition.Definition.Parts.Select(part => part.Name).ToHashSet(StringComparer.Ordinal);
        var pose = Apply(Frame(idleStarted: true, idleAgeTicks: 10f));

        Assert.NotEmpty(pose.Offsets);
        foreach (var partName in pose.Offsets.Keys)
        {
            Assert.Contains(partName, modelParts);
        }

        Assert.Contains("head", modelParts);
        Assert.Contains("body", modelParts);
        Assert.Contains("frontlegs", modelParts);
        Assert.Contains("right_front_leg", modelParts);
        Assert.Contains("left_ear", modelParts);
        Assert.Contains("tail", modelParts);
    }
}
