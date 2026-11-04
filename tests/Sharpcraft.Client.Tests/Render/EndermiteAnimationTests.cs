// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Endermite procedural animation ported from <c>EndermiteModel.setupAnim</c> (like Silverfish with 0.01/0.1 factors).
/// Covers the per-segment yRot sway and x slide at several ages, the centre-segment symmetry, clip reconstruction and
/// part coverage.
/// </summary>
public sealed class EndermiteAnimationTests
{
    private static EntityAnimationFrame Frame(float age) => new(
        TypeId: EndermiteModelDefinition.EndermiteEntityTypeId,
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
        IsHidingInShell: false);

    private static EntityPose Apply(float age)
    {
        var provider = new EndermiteAnimationProvider(EndermiteModelDefinition.EndermiteEntityTypeId);
        var pose = new EntityPose();
        var frame = Frame(age);
        provider.Apply(provider.CreateState(), in frame, pose);
        return pose;
    }

    private static float ExpectedYRot(float age, int i) =>
        MathF.Cos((age * 0.9f) + (i * 0.15f * MathF.PI)) * MathF.PI * 0.01f * (1f + MathF.Abs(i - 2));

    private static float ExpectedX(float age, int i) =>
        MathF.Sin((age * 0.9f) + (i * 0.15f * MathF.PI)) * MathF.PI * 0.1f * MathF.Abs(i - 2);

    [Fact]
    public void ProviderAdvertisesTheSingleClipAndTypeId()
    {
        var provider = new EndermiteAnimationProvider(EndermiteModelDefinition.EndermiteEntityTypeId);
        Assert.Equal(EndermiteModelDefinition.EndermiteEntityTypeId, provider.TypeId);
        Assert.Equal(new[] { "idle" }, provider.Clips);
        Assert.NotNull(provider.CreateState());
    }

    [Fact]
    public void SegmentsAtAgeZeroMatchTheReference()
    {
        var pose = Apply(0f);

        // i = 0: spread 2 -> yRot = cos(0)*π*0.01*3, x = 0.
        Assert.Equal(3f * MathF.PI * 0.01f, pose.Get("segment0").Rotation.Y, 5);
        Assert.Equal(0f, pose.Get("segment0").Position.X, 6);

        // i = 1: spread 1 -> y = cos(0.15π)*π*0.01*2, x = sin(0.15π)*π*0.1.
        Assert.Equal(MathF.Cos(0.15f * MathF.PI) * MathF.PI * 0.01f * 2f, pose.Get("segment1").Rotation.Y, 5);
        Assert.Equal(MathF.Sin(0.15f * MathF.PI) * MathF.PI * 0.1f, pose.Get("segment1").Position.X, 5);

        // i = 2: spread 0 -> y = cos(0.3π)*π*0.01, x = 0.
        Assert.Equal(MathF.Cos(0.3f * MathF.PI) * MathF.PI * 0.01f, pose.Get("segment2").Rotation.Y, 5);
        Assert.Equal(0f, pose.Get("segment2").Position.X, 6);

        // i = 3: spread 1 -> x = sin(0.45π)*π*0.1.
        Assert.Equal(MathF.Sin(0.45f * MathF.PI) * MathF.PI * 0.1f, pose.Get("segment3").Position.X, 5);
        Assert.Equal(MathF.Cos(0.45f * MathF.PI) * MathF.PI * 0.01f * 2f, pose.Get("segment3").Rotation.Y, 5);
    }

    [Theory]
    [InlineData(0.0f)]
    [InlineData(1.7f)]
    [InlineData(4.0f)]
    [InlineData(9.35f)]
    public void AllSegmentsFollowTheReferenceFormulaAtSeveralAges(float age)
    {
        var pose = Apply(age);
        for (var i = 0; i < 4; i++)
        {
            var y = pose.Get("segment" + i).Rotation.Y;
            var x = pose.Get("segment" + i).Position.X;
            Assert.Equal(ExpectedYRot(age, i), y, 5);
            Assert.Equal(ExpectedX(age, i), x, 5);
            // Rotations only about Y; X/Z rotations untouched; positions only along X.
            Assert.Equal(0f, pose.Get("segment" + i).Rotation.X, 6);
            Assert.Equal(0f, pose.Get("segment" + i).Rotation.Z, 6);
            Assert.Equal(0f, pose.Get("segment" + i).Position.Y, 6);
            Assert.Equal(0f, pose.Get("segment" + i).Position.Z, 6);
        }
    }

    [Fact]
    public void CentreSegmentNeverSlidesAndEquidistantSegmentsShareAmplitudes()
    {
        foreach (var age in new[] { 0f, 2.3f, 5.0f })
        {
            var pose = Apply(age);
            // i = 2 has spread 0, so it never slides sideways.
            Assert.Equal(0f, pose.Get("segment2").Position.X, 6);

            // The yRot envelope is π*0.01*(1 + |i-2|): 3, 2, 1, 2 for i = 0..3 (symmetric about the centre).
            var envelopes = new[] { 3f, 2f, 1f, 2f };
            for (var i = 0; i < 4; i++)
            {
                var cos = MathF.Cos((age * 0.9f) + (i * 0.15f * MathF.PI));
                Assert.Equal(envelopes[i] * MathF.PI * 0.01f, pose.Get("segment" + i).Rotation.Y / cos, 4);
            }
        }
    }

    [Fact]
    public void ApplyClipReconstructsIdleAtExplicitSeconds()
    {
        var provider = new EndermiteAnimationProvider(EndermiteModelDefinition.EndermiteEntityTypeId);
        var state = provider.CreateState();
        var frame = Frame(age: 999f);
        var pose = new EntityPose();

        // seconds * 20 = ticks; seconds 0 -> age 0.
        provider.ApplyClip(state, in frame, "idle", 0f, pose);
        Assert.Equal(3f * MathF.PI * 0.01f, pose.Get("segment0").Rotation.Y, 5);

        // seconds 0.1 -> age 2.0.
        pose.Clear();
        provider.ApplyClip(state, in frame, "idle", 0.1f, pose);
        Assert.Equal(ExpectedYRot(2f, 1), pose.Get("segment1").Rotation.Y, 5);
        Assert.Equal(ExpectedX(2f, 3), pose.Get("segment3").Position.X, 5);
    }

    [Fact]
    public void EverySegmentExistsInTheEndermiteModel()
    {
        var modelParts = EndermiteModelDefinition.Definition.Parts.Select(part => part.Name).ToHashSet(StringComparer.Ordinal);
        var pose = Apply(1.4f);

        Assert.Equal(4, pose.Offsets.Count);
        foreach (var partName in pose.Offsets.Keys)
        {
            Assert.Contains(partName, modelParts);
        }

        Assert.Contains("segment0", modelParts);
        Assert.Contains("segment1", modelParts);
        Assert.Contains("segment2", modelParts);
        Assert.Contains("segment3", modelParts);
    }
}
