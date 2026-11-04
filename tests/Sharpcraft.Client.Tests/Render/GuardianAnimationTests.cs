// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Guardian animation coverage ported from <c>GuardianModel.setupAnim</c> and the client clocks in
/// <c>Guardian.aiStep</c>. Pins the head look, the spike withdrawal offsets, the tail segment fan-out ratios, the
/// water/movement tail &amp; spikes clock evolution, and the part-name contract. Eye look is intentionally not animated
/// (it needs camera/target inputs) and is asserted to stay at bind.
/// </summary>
public sealed class GuardianAnimationTests
{
    private static readonly int TypeId = GuardianModelDefinition.GuardianEntityTypeId;

    private static readonly float[] SpikeX = [0f, 0f, 8f, -8f, -8f, 8f, 8f, -8f, 0f, 0f, 8f, -8f];
    private static readonly float[] SpikeY = [-8f, -8f, -8f, -8f, 0f, 0f, 0f, 0f, 8f, 8f, 8f, 8f];
    private static readonly float[] SpikeZ = [8f, -8f, 0f, 0f, -8f, -8f, 8f, 8f, 8f, -8f, 0f, 0f];

    private static EntityAnimationFrame Frame(
        float ageInTicks = 0f,
        float pitch = 0f,
        float yaw = 0f,
        float partial = 0f) =>
        new(
            TypeId,
            ageInTicks,
            0f,
            0f,
            pitch,
            yaw,
            0f,
            0f,
            0f,
            false,
            false,
            false,
            partial);

    private static EntityPose Apply(IEntityAnimationState state, IEntityAnimationProvider provider, in EntityAnimationFrame frame)
    {
        var pose = new EntityPose();
        provider.Apply(state, in frame, pose);
        return pose;
    }

    private static GuardianAnimationProvider.State CreateState(GuardianAnimationProvider provider) =>
        (GuardianAnimationProvider.State)provider.CreateState();

    private static void Tick(GuardianAnimationProvider.State state, float distance, bool inWater) =>
        ((IEntityTickAnimationState)state).OnTick(new EntityAnimationTickContext(1, distance, inWater));

    private static Vector3 ExpectedSpike(int i, float ageInTicks, float withdrawal)
    {
        var offset = (1f + (MathF.Cos((ageInTicks * 1.5f) + i) * 0.01f)) - withdrawal;
        var delta = offset - 1f;
        return new Vector3(SpikeX[i] * delta, -(SpikeY[i] * delta), SpikeZ[i] * delta);
    }

    private static void AssertVec(Vector3 expected, Vector3 actual, int precision = 4)
    {
        Assert.Equal(expected.X, actual.X, precision);
        Assert.Equal(expected.Y, actual.Y, precision);
        Assert.Equal(expected.Z, actual.Z, precision);
    }

    [Fact]
    public void ProviderExposesIdleSwimAndItsTypeId()
    {
        var provider = new GuardianAnimationProvider(TypeId);
        Assert.Equal(64, provider.TypeId);
        Assert.Equal(new[] { "idle", "swim" }, provider.Clips);
    }

    [Fact]
    public void HeadLooksAlongPitchAndYaw()
    {
        var provider = new GuardianAnimationProvider(TypeId);
        var pose = Apply(provider.CreateState(), provider, Frame(pitch: 30f, yaw: -45f));
        Assert.Equal(30f * (MathF.PI / 180f), pose.Get("head").Rotation.X, 4);
        Assert.Equal(-45f * (MathF.PI / 180f), pose.Get("head").Rotation.Y, 4);
    }

    [Fact]
    public void SpikeOffsetsMatchTheReferenceWithdrawal()
    {
        var provider = new GuardianAnimationProvider(TypeId);
        // A fresh state has spikesAnimation 0 → withdrawal (1-0)·0.55 = 0.55.
        var state = CreateState(provider);
        var pose = Apply(state, provider, Frame(ageInTicks: 2f));

        foreach (var i in new[] { 0, 2, 4, 8 })
        {
            AssertVec(ExpectedSpike(i, 2f, 0.55f), pose.Get("spike" + i).Position);
        }
    }

    [Fact]
    public void TailSegmentsShareTheSineWithHeightFactorRatios()
    {
        var provider = new GuardianAnimationProvider(TypeId);
        var state = CreateState(provider);
        Tick(state, distance: 0f, inWater: true); // advance the tail phase

        var pose = Apply(state, provider, Frame(partial: 0.5f));
        var swim = MathF.Sin(state.TailAnimation(0.5f));
        var tail0 = swim * MathF.PI * 0.05f;
        var tail1 = swim * MathF.PI * 0.1f;
        var tail2 = swim * MathF.PI * 0.15f;

        Assert.Equal(tail0, pose.Get("tail0").Rotation.Y, 4);
        Assert.Equal(tail1, pose.Get("tail1").Rotation.Y, 4);
        Assert.Equal(tail2, pose.Get("tail2").Rotation.Y, 4);
        Assert.Equal(2f * pose.Get("tail0").Rotation.Y, pose.Get("tail1").Rotation.Y, 5);
        Assert.Equal(3f * pose.Get("tail0").Rotation.Y, pose.Get("tail2").Rotation.Y, 5);
    }

    [Fact]
    public void TailClockAdvancesPerWaterAndMovementState()
    {
        var provider = new GuardianAnimationProvider(TypeId);

        var dry = CreateState(provider);
        var dryBefore = dry.TailAnimation(1f);
        Tick(dry, distance: 0f, inWater: false);
        Assert.Equal(dryBefore + 2f, dry.TailAnimation(1f), 4); // out of water: speed 2

        var stillWater = CreateState(provider);
        var stillBefore = stillWater.TailAnimation(1f);
        Tick(stillWater, distance: 0f, inWater: true);
        Assert.Equal(stillBefore + 0.025f, stillWater.TailAnimation(1f), 4); // still: speed eases to 0.125 → 0.025

        var movingWater = CreateState(provider);
        var movingBefore = movingWater.TailAnimation(1f);
        Tick(movingWater, distance: 1f, inWater: true);
        Assert.Equal(movingBefore + 4f, movingWater.TailAnimation(1f), 4); // moving: speed jumps to 4
    }

    [Fact]
    public void SpikesFactorEvolvesPerWaterAndMovementState()
    {
        var provider = new GuardianAnimationProvider(TypeId);

        var dry = CreateState(provider);
        Tick(dry, distance: 0f, inWater: false);
        Assert.InRange(dry.SpikesAnimation(1f), 0f, 1f); // randomised out of water

        var stillWater = CreateState(provider);
        Tick(stillWater, distance: 0f, inWater: true);
        Assert.Equal(0.06f, stillWater.SpikesAnimation(1f), 4); // 0 + (1-0)·0.06

        var movingWater = CreateState(provider);
        Tick(movingWater, distance: 1f, inWater: true);
        Assert.Equal(0f, movingWater.SpikesAnimation(1f), 4); // retract toward 0
    }

    [Fact]
    public void MovingRetractsSpikesFurtherThanStayingStill()
    {
        var provider = new GuardianAnimationProvider(TypeId);

        var retracted = CreateState(provider); // spikes 0 → withdrawal 0.55 (max retraction)
        var retractedX = MathF.Abs(Apply(retracted, provider, Frame(ageInTicks: 2f)).Get("spike2").Position.X);

        var extended = CreateState(provider);
        for (var i = 0; i < 300; i++)
        {
            Tick(extended, distance: 0f, inWater: true); // spikes ease toward 1 → withdrawal near 0
        }

        var extendedX = MathF.Abs(Apply(extended, provider, Frame(ageInTicks: 2f)).Get("spike2").Position.X);
        Assert.True(retractedX > extendedX, $"retracted {retractedX} should exceed extended {extendedX}");
    }

    [Fact]
    public void TailAnimationInterpolatesThePartialTick()
    {
        var provider = new GuardianAnimationProvider(TypeId);
        var state = CreateState(provider);
        Tick(state, distance: 0f, inWater: true);

        var previous = state.TailAnimation(0f);
        var current = state.TailAnimation(1f);
        Assert.Equal((previous + current) * 0.5f, state.TailAnimation(0.5f), 5);
    }

    [Fact]
    public void EyeIsLeftAtBindBecauseLookInputsAreUnavailable()
    {
        var provider = new GuardianAnimationProvider(TypeId);
        var pose = Apply(provider.CreateState(), provider, Frame(pitch: 10f, yaw: 20f));
        Assert.False(pose.Offsets.ContainsKey("eye"), "eye look needs camera/target inputs and must not be faked");
    }

    [Fact]
    public void ApplyClipPlaysEachNamedClipAndIgnoresUnknownOnes()
    {
        var provider = new GuardianAnimationProvider(TypeId);
        var state = provider.CreateState();
        var frame = Frame(ageInTicks: 2f);

        foreach (var clip in provider.Clips)
        {
            var pose = new EntityPose();
            provider.ApplyClip(state, frame, clip, 0.5f, pose);
            Assert.NotEmpty(pose.Offsets);
        }

        var unknown = new EntityPose();
        provider.ApplyClip(state, frame, "not_a_clip", 1f, unknown);
        Assert.Empty(unknown.Offsets);
    }

    [Fact]
    public void EveryAnimatedPartExistsInTheGuardianModel()
    {
        var modelParts = GuardianModelDefinition.Definition.Parts
            .Select(part => part.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("head", modelParts);
        for (var i = 0; i < 12; i++)
        {
            Assert.Contains("spike" + i, modelParts);
        }

        Assert.Contains("tail0", modelParts);
        Assert.Contains("tail1", modelParts);
        Assert.Contains("tail2", modelParts);
        Assert.Contains("eye", modelParts); // present, but not animated here
    }
}
