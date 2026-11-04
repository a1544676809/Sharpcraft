// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Cod animation coverage ported from <c>CodModel.setupAnim</c>. Only the tail fin moves:
/// <c>yRot = -amplitude·0.45·sin(0.6·ageInTicks)</c> with amplitude 1.0 in water and 1.5 out of water. Pins the tail
/// angle in/out of water, the amplitude difference, the idle/swim clip reconstruction, and the part-name contract.
/// </summary>
public sealed class CodAnimationTests
{
    private static readonly int TypeId = CodModelDefinition.CodEntityTypeId;

    private static EntityAnimationFrame Frame(float ageInTicks = 0f, bool inWater = false) =>
        new(
            TypeId,
            ageInTicks,
            0f,
            0f,
            0f,
            0f,
            0f,
            0f,
            0f,
            false,
            false,
            false,
            IsInWater: inWater);

    private static EntityPose Apply(IEntityAnimationState state, IEntityAnimationProvider provider, in EntityAnimationFrame frame)
    {
        var pose = new EntityPose();
        provider.Apply(state, in frame, pose);
        return pose;
    }

    private static float Expected(float ageInTicks, bool inWater) =>
        -(inWater ? 1f : 1.5f) * 0.45f * MathF.Sin(0.6f * ageInTicks);

    [Fact]
    public void ProviderExposesIdleSwimAndItsTypeId()
    {
        var provider = new CodAnimationProvider(TypeId);
        Assert.Equal(27, provider.TypeId);
        Assert.Equal(new[] { "idle", "swim" }, provider.Clips);
    }

    [Fact]
    public void TailFinMatchesTheReferenceInWater()
    {
        var provider = new CodAnimationProvider(TypeId);
        var pose = Apply(provider.CreateState(), provider, Frame(ageInTicks: 1f, inWater: true));
        Assert.Equal(Expected(1f, inWater: true), pose.Get("tail_fin").Rotation.Y, 4);
        Assert.Equal(0f, pose.Get("tail_fin").Rotation.X, 6);
        Assert.Equal(0f, pose.Get("tail_fin").Rotation.Z, 6);
    }

    [Fact]
    public void TailFinMatchesTheReferenceOutOfWater()
    {
        var provider = new CodAnimationProvider(TypeId);
        var pose = Apply(provider.CreateState(), provider, Frame(ageInTicks: 2.5f, inWater: false));
        Assert.Equal(Expected(2.5f, inWater: false), pose.Get("tail_fin").Rotation.Y, 4);
    }

    [Fact]
    public void OutOfWaterAmplitudeIsHalfAgainAsLarge()
    {
        var provider = new CodAnimationProvider(TypeId);
        var state = provider.CreateState();

        var inWater = Apply(state, provider, Frame(ageInTicks: 1.3f, inWater: true)).Get("tail_fin").Rotation.Y;
        var outOfWater = Apply(state, provider, Frame(ageInTicks: 1.3f, inWater: false)).Get("tail_fin").Rotation.Y;

        Assert.Equal(1.5f * inWater, outOfWater, 5);
        Assert.NotEqual(0f, inWater);
    }

    [Fact]
    public void TailRestsAtZeroAge()
    {
        var provider = new CodAnimationProvider(TypeId);
        var pose = Apply(provider.CreateState(), provider, Frame(ageInTicks: 0f, inWater: true));
        Assert.Equal(0f, pose.Get("tail_fin").Rotation.Y, 6);
    }

    [Fact]
    public void OnlyTheTailFinIsAnimated()
    {
        var provider = new CodAnimationProvider(TypeId);
        var pose = Apply(provider.CreateState(), provider, Frame(ageInTicks: 3f, inWater: true));

        Assert.Single(pose.Offsets);
        Assert.True(pose.Offsets.ContainsKey("tail_fin"));
    }

    [Fact]
    public void ApplyClipReconstructsIdleAndSwim()
    {
        var provider = new CodAnimationProvider(TypeId);
        var state = provider.CreateState();

        // "swim" forces the in-water amplitude at age = seconds·20.
        var swim = new EntityPose();
        provider.ApplyClip(state, Frame(), "swim", 0.1f, swim);
        Assert.Equal(Expected(2f, inWater: true), swim.Get("tail_fin").Rotation.Y, 4);

        // "idle" forces the wider out-of-water flop.
        var idle = new EntityPose();
        provider.ApplyClip(state, Frame(), "idle", 0.1f, idle);
        Assert.Equal(Expected(2f, inWater: false), idle.Get("tail_fin").Rotation.Y, 4);

        var unknown = new EntityPose();
        provider.ApplyClip(state, Frame(), "not_a_clip", 1f, unknown);
        Assert.Empty(unknown.Offsets);
    }

    [Fact]
    public void EveryAnimatedPartExistsInTheCodModel()
    {
        var modelParts = CodModelDefinition.Definition.Parts
            .Select(part => part.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("tail_fin", modelParts);
    }
}
