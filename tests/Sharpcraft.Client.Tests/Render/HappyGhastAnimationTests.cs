// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Happy Ghast tentacle animation coverage ported from <c>HappyGhastModel.setupAnim</c> / <c>GhastModel.animateTentacles</c>:
/// each tentacle's xRot is <c>0.2·sin(ageInTicks·0.3 + index) + 0.4</c>. The body squeeze (scale 0.9375 while holding a
/// body item) is intentionally not applied because the body-item flag is unavailable; the body is asserted to stay at
/// bind.
/// </summary>
public sealed class HappyGhastAnimationTests
{
    private static readonly int TypeId = HappyGhastModelDefinition.HappyGhastEntityTypeId;

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
        var provider = new HappyGhastAnimationProvider(TypeId);
        Assert.Equal(59, provider.TypeId);
        Assert.Equal(new[] { "idle" }, provider.Clips);
    }

    [Fact]
    public void TentacleAnglesMatchTheReference()
    {
        var provider = new HappyGhastAnimationProvider(TypeId);
        var state = provider.CreateState();

        foreach (var age in new[] { 2f, 5.5f })
        {
            var pose = Apply(state, provider, Frame(ageInTicks: age));
            foreach (var i in new[] { 0, 1, 4, 8 })
            {
                Assert.Equal(Expected(age, i), pose.Get("tentacle" + i).Rotation.X, 4);
            }
        }
    }

    [Fact]
    public void TentaclesKeepTheReferencePhaseShift()
    {
        var provider = new HappyGhastAnimationProvider(TypeId);
        var state = provider.CreateState();

        const float age = 3.7f;
        var pose = Apply(state, provider, Frame(ageInTicks: age));
        var shifted = Apply(state, provider, Frame(ageInTicks: age + (3f / 0.3f)));

        // Index 3 at age A shares the phase of index 0 at age A + 3/0.3.
        Assert.Equal(pose.Get("tentacle3").Rotation.X, shifted.Get("tentacle0").Rotation.X, 3);
    }

    [Fact]
    public void AllNineTentaclesAnimateAndTheBodyStaysAtBind()
    {
        var provider = new HappyGhastAnimationProvider(TypeId);
        var pose = Apply(provider.CreateState(), provider, Frame(ageInTicks: 1.5f));

        Assert.Equal(9, pose.Offsets.Count);
        for (var i = 0; i < 9; i++)
        {
            Assert.True(pose.Offsets.ContainsKey("tentacle" + i), $"tentacle{i} should be animated");
        }

        // Body squeeze is blocked (body-item flag unavailable) → no body entry / no scale change.
        Assert.False(pose.Offsets.ContainsKey("body"));
    }

    [Fact]
    public void ApplyClipReconstructsIdleAtExplicitSeconds()
    {
        var provider = new HappyGhastAnimationProvider(TypeId);
        var state = provider.CreateState();

        var clip = new EntityPose();
        provider.ApplyClip(state, Frame(), "idle", 0.1f, clip); // age 2
        Assert.Equal(Expected(2f, 6), clip.Get("tentacle6").Rotation.X, 4);

        var unknown = new EntityPose();
        provider.ApplyClip(state, Frame(), "not_a_clip", 1f, unknown);
        Assert.Empty(unknown.Offsets);
    }

    [Fact]
    public void EveryAnimatedPartExistsInTheHappyGhastModel()
    {
        var modelParts = HappyGhastModelDefinition.Definition.Parts
            .Select(part => part.Name)
            .ToHashSet(StringComparer.Ordinal);

        for (var i = 0; i < 9; i++)
        {
            Assert.Contains("tentacle" + i, modelParts);
        }
    }
}
