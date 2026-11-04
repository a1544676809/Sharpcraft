// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Snow Golem procedural animation ported from <c>SnowGolemModel.setupAnim</c>. Covers the head look, the quarter-turn
/// upper body yaw, the arm yaw follow plus the x/z position swing (with bind/mirror-aware additive deltas), and part
/// coverage.
/// </summary>
public sealed class SnowGolemAnimationTests
{
    private const float ToRad = MathF.PI / 180f;

    private static EntityAnimationFrame Frame(float pitch = 0f, float yaw = 0f) => new(
        TypeId: SnowGolemModelDefinition.SnowGolemEntityTypeId,
        AgeInTicks: 0f,
        WalkAnimationPos: 0f,
        WalkAnimationSpeed: 0f,
        Pitch: pitch,
        Yaw: yaw,
        Flap: 0f,
        FlapSpeed: 0f,
        TentacleAngle: 0f,
        IsResting: false,
        IsBaby: false,
        IsHidingInShell: false);

    private static EntityPose Apply(in EntityAnimationFrame frame)
    {
        var provider = new SnowGolemAnimationProvider(SnowGolemModelDefinition.SnowGolemEntityTypeId);
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
    public void ProviderAdvertisesTheTwoClipsAndTypeId()
    {
        var provider = new SnowGolemAnimationProvider(SnowGolemModelDefinition.SnowGolemEntityTypeId);
        Assert.Equal(SnowGolemModelDefinition.SnowGolemEntityTypeId, provider.TypeId);
        Assert.Equal(new[] { "idle", "look" }, provider.Clips);
        Assert.NotNull(provider.CreateState());
    }

    [Fact]
    public void HeadLooksAlongPitchAndYawInRadians()
    {
        var pose = Apply(Frame(pitch: 15f, yaw: -30f));
        Assert.Equal(15f * ToRad, pose.Get("head").Rotation.X, 5);
        Assert.Equal(-30f * ToRad, pose.Get("head").Rotation.Y, 5);
        Assert.Equal(0f, pose.Get("head").Rotation.Z, 5);
    }

    [Fact]
    public void UpperBodyFollowsTheYawAtAQuarterTurn()
    {
        var pose = Apply(Frame(yaw: 90f));
        Assert.Equal(90f * ToRad * 0.25f, pose.Get("upper_body").Rotation.Y, 5);
        // The arms' yaw follows the upper body; the right arm's bind yRot is π, so its delta equals upperYaw too.
        Assert.Equal(90f * ToRad * 0.25f, pose.Get("left_arm").Rotation.Y, 5);
        Assert.Equal(90f * ToRad * 0.25f, pose.Get("right_arm").Rotation.Y, 5);
    }

    [Fact]
    public void NeutralLookSitsAtTheRestPose()
    {
        var pose = Apply(Frame(pitch: 0f, yaw: 0f));
        Assert.Equal(0f, pose.Get("head").Rotation.X, 6);
        Assert.Equal(0f, pose.Get("upper_body").Rotation.Y, 6);
        Assert.Equal(0f, pose.Get("left_arm").Rotation.Y, 6);
        // Even at zero yaw the reference repositions the arms to z=0, i.e. the z bind ±1 is pulled back.
        AssertVec(new Vector3(0f, 0f, -1f), pose.Get("left_arm").Position, 5);
        AssertVec(new Vector3(0f, 0f, 1f), pose.Get("right_arm").Position, 5);
        // Arm bind z-rotation (∓1) is not animated.
        Assert.Equal(0f, pose.Get("left_arm").Rotation.Z, 6);
        Assert.Equal(0f, pose.Get("right_arm").Rotation.Z, 6);
    }

    [Fact]
    public void ArmsSwingAroundTheBodyWithTheUpperYaw()
    {
        var pose = Apply(Frame(yaw: 90f));
        var upperYaw = 90f * ToRad * 0.25f;
        var sin = MathF.Sin(upperYaw);
        var cos = MathF.Cos(upperYaw);

        // Left arm: engine x delta 5(1-cos), z delta -5sin-1 (bind z 1).
        Assert.Equal(5f * (1f - cos), pose.Get("left_arm").Position.X, 5);
        Assert.Equal((-5f * sin) - 1f, pose.Get("left_arm").Position.Z, 5);
        Assert.Equal(0f, pose.Get("left_arm").Position.Y, 6);

        // Right arm: engine x delta 5(cos-1), z delta 5sin+1 (bind z -1).
        Assert.Equal(5f * (cos - 1f), pose.Get("right_arm").Position.X, 5);
        Assert.Equal((5f * sin) + 1f, pose.Get("right_arm").Position.Z, 5);
        Assert.Equal(0f, pose.Get("right_arm").Position.Y, 6);
    }

    [Fact]
    public void ApplyClipReconstructsIdleAndLook()
    {
        var provider = new SnowGolemAnimationProvider(SnowGolemModelDefinition.SnowGolemEntityTypeId);
        var state = provider.CreateState();
        var frame = Frame(pitch: 20f, yaw: 45f);
        var pose = new EntityPose();

        provider.ApplyClip(state, in frame, "look", 0f, pose);
        Assert.Equal(20f * ToRad, pose.Get("head").Rotation.X, 5);
        Assert.Equal(45f * ToRad, pose.Get("head").Rotation.Y, 5);

        pose.Clear();
        provider.ApplyClip(state, in frame, "idle", 0f, pose);
        Assert.Equal(0f, pose.Get("head").Rotation.X, 5);
        Assert.Equal(0f, pose.Get("head").Rotation.Y, 5);
        Assert.Equal(0f, pose.Get("upper_body").Rotation.Y, 5);
    }

    [Fact]
    public void EveryAnimatedPartExistsInTheSnowGolemModel()
    {
        var modelParts = SnowGolemModelDefinition.Definition.Parts.Select(part => part.Name).ToHashSet(StringComparer.Ordinal);
        var pose = Apply(Frame(pitch: 10f, yaw: 70f));

        Assert.NotEmpty(pose.Offsets);
        foreach (var partName in pose.Offsets.Keys)
        {
            Assert.Contains(partName, modelParts);
        }

        Assert.Contains("head", modelParts);
        Assert.Contains("upper_body", modelParts);
        Assert.Contains("left_arm", modelParts);
        Assert.Contains("right_arm", modelParts);
    }
}
