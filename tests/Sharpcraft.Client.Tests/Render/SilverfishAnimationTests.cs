// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Silverfish animation coverage ported from <c>SilverfishModel.setupAnim</c>: for each of the seven segments,
/// <c>yRot = cos(age·0.9 + i·0.15π)·π·0.05·(1+|i−2|)</c> and <c>x = sin(age·0.9 + i·0.15π)·π·0.2·|i−2|</c>, with the
/// three layer cubes copying their parent segment. Pins representative values, the layer copy relationship, and the
/// part-name contract.
/// </summary>
public sealed class SilverfishAnimationTests
{
    private static readonly int TypeId = SilverfishModelDefinition.SilverfishEntityTypeId;

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

    private static float ExpectedYRot(int i, float ageInTicks)
    {
        var phase = (ageInTicks * 0.9f) + (i * 0.15f * MathF.PI);
        return MathF.Cos(phase) * MathF.PI * 0.05f * (1 + MathF.Abs(i - 2));
    }

    private static float ExpectedX(int i, float ageInTicks)
    {
        var phase = (ageInTicks * 0.9f) + (i * 0.15f * MathF.PI);
        return MathF.Sin(phase) * MathF.PI * 0.2f * MathF.Abs(i - 2);
    }

    [Fact]
    public void ProviderExposesIdleAndItsTypeId()
    {
        var provider = new SilverfishAnimationProvider(TypeId);
        Assert.Equal(117, provider.TypeId);
        Assert.Equal(new[] { "idle" }, provider.Clips);
    }

    [Fact]
    public void SegmentsMatchTheReferenceAtAgeTwo()
    {
        var provider = new SilverfishAnimationProvider(TypeId);
        var pose = Apply(provider.CreateState(), provider, Frame(ageInTicks: 2f));

        foreach (var i in new[] { 0, 1, 2, 3, 6 })
        {
            Assert.Equal(ExpectedYRot(i, 2f), pose.Get("segment" + i).Rotation.Y, 4);
            Assert.Equal(ExpectedX(i, 2f), pose.Get("segment" + i).Position.X, 4);
        }
    }

    [Fact]
    public void MiddleSegmentHasNoTranslation()
    {
        var provider = new SilverfishAnimationProvider(TypeId);
        var pose = Apply(provider.CreateState(), provider, Frame(ageInTicks: 4.25f));

        // i = 2 → |i−2| = 0, so x is always 0 but yRot still sways with factor 1.
        Assert.Equal(0f, pose.Get("segment2").Position.X, 5);
        Assert.Equal(ExpectedYRot(2, 4.25f), pose.Get("segment2").Rotation.Y, 4);
    }

    [Fact]
    public void SegmentAmplitudeGrowsWithDistanceFromTheMiddle()
    {
        var provider = new SilverfishAnimationProvider(TypeId);
        // At age 0, cos(phase) and sin(phase) share the same phase per segment; use the x factor as the envelope check.
        var pose = Apply(provider.CreateState(), provider, Frame(ageInTicks: 0f));

        // x = sin(i·0.15π)·π·0.2·|i−2|; segment2 has zero spread, segment0 has |−2| = 2.
        Assert.Equal(0f, pose.Get("segment2").Position.X, 5);
        Assert.Equal(ExpectedX(0, 0f), pose.Get("segment0").Position.X, 4);
        Assert.NotEqual(0f, pose.Get("segment3").Position.X);
    }

    [Fact]
    public void LayersCopyTheirParentSegments()
    {
        var provider = new SilverfishAnimationProvider(TypeId);
        var pose = Apply(provider.CreateState(), provider, Frame(ageInTicks: 3.3f));

        // layer0 ← segment2.yRot
        Assert.Equal(pose.Get("segment2").Rotation.Y, pose.Get("layer0").Rotation.Y, 6);
        Assert.Equal(0f, pose.Get("layer0").Position.X, 6);

        // layer1 ← segment4 (yRot + x)
        Assert.Equal(pose.Get("segment4").Rotation.Y, pose.Get("layer1").Rotation.Y, 6);
        Assert.Equal(pose.Get("segment4").Position.X, pose.Get("layer1").Position.X, 6);

        // layer2 ← segment1 (yRot + x)
        Assert.Equal(pose.Get("segment1").Rotation.Y, pose.Get("layer2").Rotation.Y, 6);
        Assert.Equal(pose.Get("segment1").Position.X, pose.Get("layer2").Position.X, 6);
    }

    [Fact]
    public void ApplyClipReconstructsIdleAtExplicitSeconds()
    {
        var provider = new SilverfishAnimationProvider(TypeId);
        var state = provider.CreateState();

        // "idle" at 0.1 s maps to age 2 ticks.
        var clip = new EntityPose();
        provider.ApplyClip(state, Frame(), "idle", 0.1f, clip);
        var direct = Apply(state, provider, Frame(ageInTicks: 2f));
        Assert.Equal(direct.Get("segment3").Rotation.Y, clip.Get("segment3").Rotation.Y, 4);
        Assert.Equal(direct.Get("segment3").Position.X, clip.Get("segment3").Position.X, 4);

        var unknown = new EntityPose();
        provider.ApplyClip(state, Frame(), "not_a_clip", 1f, unknown);
        Assert.Empty(unknown.Offsets);
    }

    [Fact]
    public void EveryAnimatedPartExistsInTheSilverfishModel()
    {
        var modelParts = SilverfishModelDefinition.Definition.Parts
            .Select(part => part.Name)
            .ToHashSet(StringComparer.Ordinal);

        for (var i = 0; i < 7; i++)
        {
            Assert.Contains("segment" + i, modelParts);
        }

        for (var i = 0; i < 3; i++)
        {
            Assert.Contains("layer" + i, modelParts);
        }
    }
}
