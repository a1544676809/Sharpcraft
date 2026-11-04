// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Rabbit provider. Only the derivable head-look pose is implemented; hop and idle-head-tilt timing are blocked (see the
/// provider remarks). These tests pin the head look, prove no hop/other parts are faked, and check part coverage.
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
        bool onGround = true) => new(
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
        IsOnGround: onGround);

    private static EntityPose Apply(in EntityAnimationFrame frame)
    {
        var provider = new RabbitAnimationProvider(RabbitModelDefinition.RabbitEntityTypeId);
        var pose = new EntityPose();
        provider.Apply(provider.CreateState(), in frame, pose);
        return pose;
    }

    [Fact]
    public void ProviderAdvertisesTheClipAndTypeId()
    {
        var provider = new RabbitAnimationProvider(RabbitModelDefinition.RabbitEntityTypeId);
        Assert.Equal(RabbitModelDefinition.RabbitEntityTypeId, provider.TypeId);
        Assert.Equal(new[] { "idle" }, provider.Clips);
        Assert.NotNull(provider.CreateState());
    }

    [Fact]
    public void HeadLooksAlongPitchAndYaw()
    {
        var pose = Apply(Frame(pitch: 20f, yaw: -45f));
        Assert.Equal(20f * ToRad, pose.Get("head").Rotation.X, 5);
        Assert.Equal(-45f * ToRad, pose.Get("head").Rotation.Y, 5);
        Assert.Equal(0f, pose.Get("head").Rotation.Z, 5);
        AssertVec(Vector3.Zero, pose.Get("head").Position);
    }

    [Fact]
    public void OnlyTheHeadIsAnimatedAndNoHopIsFaked()
    {
        // Even while airborne / moving, the blocked hop must not emit body/leg/ear offsets.
        var pose = Apply(Frame(age: 40f, walkPos: 3f, walkSpeed: 1f, onGround: false));

        Assert.Equal(new HashSet<string>(StringComparer.Ordinal) { "head" }, pose.Offsets.Keys.ToHashSet(StringComparer.Ordinal));
        foreach (var part in new[] { "body", "tail", "backlegs", "frontlegs", "left_ear", "right_ear", "right_hind_leg", "left_hind_leg" })
        {
            AssertVec(Vector3.Zero, pose.Get(part).Rotation);
            AssertVec(Vector3.Zero, pose.Get(part).Position);
        }
    }

    [Fact]
    public void ApplyClipAppliesTheHeadPose()
    {
        var provider = new RabbitAnimationProvider(RabbitModelDefinition.RabbitEntityTypeId);
        var state = provider.CreateState();
        var frame = Frame(pitch: 10f, yaw: 25f);
        var pose = new EntityPose();

        provider.ApplyClip(state, in frame, "idle", 0f, pose);
        Assert.Equal(10f * ToRad, pose.Get("head").Rotation.X, 5);
        Assert.Equal(25f * ToRad, pose.Get("head").Rotation.Y, 5);
    }

    [Fact]
    public void EveryAnimatedPartExistsInTheRabbitModel()
    {
        var modelParts = RabbitModelDefinition.Definition.Parts.Select(part => part.Name).ToHashSet(StringComparer.Ordinal);
        var pose = Apply(Frame(pitch: 5f, yaw: 5f));

        Assert.NotEmpty(pose.Offsets);
        foreach (var partName in pose.Offsets.Keys)
        {
            Assert.Contains(partName, modelParts);
        }

        Assert.Contains("head", modelParts);
    }

    private static void AssertVec(Vector3 expected, Vector3 actual, int precision = 4)
    {
        Assert.Equal(expected.X, actual.X, precision);
        Assert.Equal(expected.Y, actual.Y, precision);
        Assert.Equal(expected.Z, actual.Z, precision);
    }
}
