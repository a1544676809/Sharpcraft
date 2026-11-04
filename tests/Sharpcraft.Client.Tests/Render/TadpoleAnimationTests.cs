// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Tadpole procedural animation ported from <c>TadpoleModel.setupAnim</c>. The tail yaw is
/// <c>-amplitude * 0.25 * sin(0.3 * ageInTicks)</c> with amplitude 1.0 in water and 1.5 on land.
/// </summary>
public sealed class TadpoleAnimationTests
{
    private static EntityAnimationFrame Frame(float age, bool inWater) => new(
        TypeId: TadpoleModelDefinition.TadpoleEntityTypeId,
        AgeInTicks: age,
        WalkAnimationPos: 0f,
        WalkAnimationSpeed: 0f,
        Pitch: 0f,
        Yaw: 0f,
        Flap: 0f,
        FlapSpeed: 0f,
        TentacleAngle: 0f,
        IsResting: false,
        IsBaby: false,
        IsHidingInShell: false,
        IsInWater: inWater);

    private static EntityPose Apply(float age, bool inWater)
    {
        var provider = new TadpoleAnimationProvider(TadpoleModelDefinition.TadpoleEntityTypeId);
        var frame = Frame(age, inWater);
        var pose = new EntityPose();
        provider.Apply(provider.CreateState(), in frame, pose);
        return pose;
    }

    [Fact]
    public void ProviderAdvertisesTheTwoClipsAndTypeId()
    {
        var provider = new TadpoleAnimationProvider(TadpoleModelDefinition.TadpoleEntityTypeId);
        Assert.Equal(TadpoleModelDefinition.TadpoleEntityTypeId, provider.TypeId);
        Assert.Equal(new[] { "idle", "swim" }, provider.Clips);
        Assert.NotNull(provider.CreateState());
    }

    [Fact]
    public void TailYawIsZeroAtRest()
    {
        var inWater = Apply(0f, inWater: true);
        Assert.Equal(0f, inWater.Get("tail").Rotation.Y, 6);
        Assert.Equal(0f, inWater.Get("tail").Rotation.X, 6);
        Assert.Equal(0f, inWater.Get("tail").Rotation.Z, 6);

        var onLand = Apply(0f, inWater: false);
        Assert.Equal(0f, onLand.Get("tail").Rotation.Y, 6);
    }

    [Fact]
    public void InWaterTailPeaksAtMinusOneQuarterRadian()
    {
        // 0.3 * age = π/2 -> sin = 1 -> yaw = -1.0 * 0.25 = -0.25 rad.
        var age = (MathF.PI / 2f) / 0.3f;
        var pose = Apply(age, inWater: true);
        Assert.Equal(-0.25f, pose.Get("tail").Rotation.Y, 5);
    }

    [Fact]
    public void OutOfWaterTailPeaksAtMinusThreeEighthsRadian()
    {
        // Amplitude 1.5 -> yaw = -1.5 * 0.25 = -0.375 rad at the same phase.
        var age = (MathF.PI / 2f) / 0.3f;
        var pose = Apply(age, inWater: false);
        Assert.Equal(-0.375f, pose.Get("tail").Rotation.Y, 5);
    }

    [Fact]
    public void RepresentativeAngleMatchesTheReference()
    {
        // age = 10 -> sin(3) ≈ 0.14112 -> in water yaw = -0.25 * sin(3) ≈ -0.035280.
        var pose = Apply(10f, inWater: true);
        var expected = -1.0f * 0.25f * MathF.Sin(3f);
        Assert.Equal(expected, pose.Get("tail").Rotation.Y, 5);
    }

    [Fact]
    public void LandAmplitudeIsOneAndAHalfTimesTheWaterAmplitude()
    {
        const float age = 7.3f;
        var water = Apply(age, inWater: true).Get("tail").Rotation.Y;
        var land = Apply(age, inWater: false).Get("tail").Rotation.Y;
        Assert.NotEqual(0f, water);
        Assert.Equal(1.5f, land / water, 5);
    }

    [Fact]
    public void ApplyClipSelectsTheAmplitudeForTheClip()
    {
        var provider = new TadpoleAnimationProvider(TadpoleModelDefinition.TadpoleEntityTypeId);
        var state = provider.CreateState();
        var frame = Frame(0f, inWater: false);
        // Pick seconds so that 0.3 * (seconds * 20) = π/2 (sin = 1).
        var seconds = (MathF.PI / 2f) / 0.3f / 20f;

        var pose = new EntityPose();
        provider.ApplyClip(state, in frame, "swim", seconds, pose);
        Assert.Equal(-0.25f, pose.Get("tail").Rotation.Y, 5);

        pose.Clear();
        provider.ApplyClip(state, in frame, "idle", seconds, pose);
        Assert.Equal(-0.375f, pose.Get("tail").Rotation.Y, 5);
    }

    [Fact]
    public void OnlyTheTailIsAnimatedAndItExistsInTheModel()
    {
        var modelParts = TadpoleModelDefinition.Definition.Parts.Select(part => part.Name).ToHashSet(StringComparer.Ordinal);
        var pose = Apply(3.3f, inWater: false);

        var keys = pose.Offsets.Keys.ToHashSet(StringComparer.Ordinal);
        Assert.Equal(new HashSet<string>(StringComparer.Ordinal) { "tail" }, keys);
        Assert.Contains("tail", modelParts);
    }
}
