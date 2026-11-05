// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Pufferfish <c>setupAnim</c>: both side fins flap with the same formula on every puff-state model. Pins the phase
/// values on both fin naming schemes (small vs medium/large) and the sandbox clip mapping.
/// </summary>
public sealed class PufferfishAnimationTests
{
    private static EntityAnimationFrame Frame(float ageInTicks = 0f) => new(
        TypeId: PufferfishModelDefinition.PufferfishEntityTypeId,
        AgeInTicks: ageInTicks,
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

    private static EntityPose Apply(in EntityAnimationFrame frame, string clip = null!, float seconds = 0f)
    {
        var provider = new PufferfishAnimationProvider(PufferfishModelDefinition.PufferfishEntityTypeId);
        var pose = new EntityPose();
        if (clip is null)
        {
            provider.Apply(provider.CreateState(), in frame, pose);
        }
        else
        {
            provider.ApplyClip(provider.CreateState(), in frame, clip, seconds, pose);
        }

        return pose;
    }

    // sin(age·0.2) = +1  -> age·0.2 = π/2
    private static readonly float QuarterCycleAge = MathF.PI / 2f / 0.2f;

    // sin(age·0.2) = -1  -> age·0.2 = 3π/2
    private static readonly float ThreeQuarterCycleAge = 3f * MathF.PI / 2f / 0.2f;

    [Fact]
    public void ProviderAdvertisesTheIdleClipAndTypeId()
    {
        var provider = new PufferfishAnimationProvider(PufferfishModelDefinition.PufferfishEntityTypeId);
        Assert.Equal(PufferfishModelDefinition.PufferfishEntityTypeId, provider.TypeId);
        Assert.Equal(new[] { "idle" }, provider.Clips);
        Assert.NotNull(provider.CreateState());
    }

    [Fact]
    public void FinsRestAtTheirOffsetsWhenTheSwayIsZero()
    {
        // sin(0) = 0: right = -0.2, left = +0.2, on both naming schemes (small and medium/large).
        var pose = Apply(Frame(0f));

        Assert.Equal(-0.2f, pose.Get("right_blue_fin").Rotation.Z, 5);
        Assert.Equal(0.2f, pose.Get("left_blue_fin").Rotation.Z, 5);
        Assert.Equal(-0.2f, pose.Get("right_fin").Rotation.Z, 5);
        Assert.Equal(0.2f, pose.Get("left_fin").Rotation.Z, 5);
    }

    [Fact]
    public void FinsReachTheAmpitudeAtTheQuarterCycle()
    {
        // sin(age·0.2) = 1: right = -0.2 + 0.4 = 0.2, left = 0.2 - 0.4 = -0.2.
        var pose = Apply(Frame(QuarterCycleAge));

        Assert.Equal(0.2f, pose.Get("right_blue_fin").Rotation.Z, 4);
        Assert.Equal(-0.2f, pose.Get("left_blue_fin").Rotation.Z, 4);
        Assert.Equal(0.2f, pose.Get("right_fin").Rotation.Z, 4);
        Assert.Equal(-0.2f, pose.Get("left_fin").Rotation.Z, 4);
    }

    [Fact]
    public void FinsReachTheOppositeAmplitudeAtTheThreeQuarterCycle()
    {
        // sin(age·0.2) = -1: right = -0.2 - 0.4 = -0.6, left = 0.2 + 0.4 = 0.6.
        var pose = Apply(Frame(ThreeQuarterCycleAge));

        Assert.Equal(-0.6f, pose.Get("right_blue_fin").Rotation.Z, 4);
        Assert.Equal(0.6f, pose.Get("left_blue_fin").Rotation.Z, 4);
        Assert.Equal(-0.6f, pose.Get("right_fin").Rotation.Z, 4);
        Assert.Equal(0.6f, pose.Get("left_fin").Rotation.Z, 4);
    }

    [Fact]
    public void TheTwoFinsAreAlwaysAntisymmetric()
    {
        var pose = Apply(Frame(1.234f));
        var right = pose.Get("right_blue_fin").Rotation.Z;
        var left = pose.Get("left_blue_fin").Rotation.Z;

        Assert.Equal(-right, left, 5);
        Assert.Equal(pose.Get("right_fin").Rotation.Z, right, 5);
        Assert.Equal(pose.Get("left_fin").Rotation.Z, left, 5);
    }

    [Fact]
    public void FinsOnlyRotateAboutZ()
    {
        var pose = Apply(Frame(2.5f));
        var offset = pose.Get("right_blue_fin");

        Assert.Equal(0f, offset.Rotation.X, 5);
        Assert.Equal(0f, offset.Rotation.Y, 5);
        Assert.Equal(Vector3.Zero, offset.Position);
        Assert.Equal(Vector3.Zero, offset.Scale);
    }

    [Fact]
    public void ApplyClipIdleUsesTwentyTicksPerSecond()
    {
        var direct = Apply(Frame(40f));
        var fromClip = Apply(Frame(0f), "idle", 2f);

        Assert.Equal(direct.Get("right_blue_fin").Rotation.Z, fromClip.Get("right_blue_fin").Rotation.Z, 5);
        Assert.Equal(direct.Get("left_fin").Rotation.Z, fromClip.Get("left_fin").Rotation.Z, 5);
    }

    [Fact]
    public void UnknownClipLeavesThePoseEmpty()
    {
        var pose = Apply(Frame(1f), "not_a_clip", 1f);

        Assert.Empty(pose.Offsets);
    }
}
