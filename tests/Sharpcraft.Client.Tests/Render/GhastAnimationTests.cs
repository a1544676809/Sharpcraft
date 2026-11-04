// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Ghast tentacle animation coverage ported from <c>GhastModel.animateTentacles</c>: each tentacle's xRot is
/// <c>0.2·sin(ageInTicks·0.3 + index) + 0.4</c>. Pins representative angles, the per-index phase shift, the idle clip
/// reconstruction, and the part-name contract.
/// </summary>
public sealed class GhastAnimationTests
{
    private static readonly int TypeId = GhastModelDefinition.GhastEntityTypeId;

    private static EntityAnimationFrame Frame(float ageInTicks = 0f) =>
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
            false);

    private static EntityPose Apply(IEntityAnimationState state, IEntityAnimationProvider provider, in EntityAnimationFrame frame)
    {
        var pose = new EntityPose();
        provider.Apply(state, in frame, pose);
        return pose;
    }

    private static float Expected(float ageInTicks, int index) =>
        (0.2f * MathF.Sin((ageInTicks * 0.3f) + index)) + 0.4f;

    [Fact]
    public void ProviderExposesIdleAndItsTypeId()
    {
        var provider = new GhastAnimationProvider(TypeId);
        Assert.Equal(58, provider.TypeId);
        Assert.Equal(new[] { "idle" }, provider.Clips);
    }

    [Fact]
    public void TentacleAnglesMatchTheReference()
    {
        var provider = new GhastAnimationProvider(TypeId);
        var pose = Apply(provider.CreateState(), provider, Frame(ageInTicks: 2f));

        foreach (var i in new[] { 0, 1, 4, 8 })
        {
            Assert.Equal(Expected(2f, i), pose.Get("tentacle" + i).Rotation.X, 4);
        }
    }

    [Fact]
    public void TentaclesKeepTheReferencePhaseShiftAndBaseOffset()
    {
        var provider = new GhastAnimationProvider(TypeId);
        var state = provider.CreateState();

        const float age = 3.7f;
        var pose = Apply(state, provider, Frame(ageInTicks: age));
        for (var i = 0; i < 9; i++)
        {
            Assert.Equal(Expected(age, i), pose.Get("tentacle" + i).Rotation.X, 4);
            // The base 0.4 plus a ±0.2 sine keeps every tentacle within [0.2, 0.6].
            var value = pose.Get("tentacle" + i).Rotation.X;
            Assert.InRange(value, 0.2f, 0.6f);
        }

        // Index i at age A is the same phase as index 0 at age A + i/0.3.
        var shifted = Apply(state, provider, Frame(ageInTicks: age + (3f / 0.3f)));
        Assert.Equal(pose.Get("tentacle3").Rotation.X, shifted.Get("tentacle0").Rotation.X, 3);
    }

    [Fact]
    public void AllNineTentaclesAnimateAndTheBodyDoesNot()
    {
        var provider = new GhastAnimationProvider(TypeId);
        var pose = Apply(provider.CreateState(), provider, Frame(ageInTicks: 1.5f));

        Assert.Equal(9, pose.Offsets.Count);
        for (var i = 0; i < 9; i++)
        {
            Assert.True(pose.Offsets.ContainsKey("tentacle" + i), $"tentacle{i} should be animated");
        }

        Assert.False(pose.Offsets.ContainsKey("body"), "the body is not animated");
    }

    [Fact]
    public void ApplyClipReconstructsIdleAtExplicitSeconds()
    {
        var provider = new GhastAnimationProvider(TypeId);
        var state = provider.CreateState();

        // "idle" at 0.1 s maps to age 2 ticks.
        var clip = new EntityPose();
        provider.ApplyClip(state, Frame(), "idle", 0.1f, clip);
        var direct = Apply(state, provider, Frame(ageInTicks: 2f));
        Assert.Equal(direct.Get("tentacle2").Rotation.X, clip.Get("tentacle2").Rotation.X, 4);

        var unknown = new EntityPose();
        provider.ApplyClip(state, Frame(), "not_a_clip", 1f, unknown);
        Assert.Empty(unknown.Offsets);
    }

    [Fact]
    public void EveryAnimatedPartExistsInTheGhastModel()
    {
        var modelParts = GhastModelDefinition.Definition.Parts
            .Select(part => part.Name)
            .ToHashSet(StringComparer.Ordinal);

        for (var i = 0; i < 9; i++)
        {
            Assert.Contains("tentacle" + i, modelParts);
        }
    }
}
