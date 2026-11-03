// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Render;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>Axolotl animation behaviour: adult factor blending and baby keyframe clips.</summary>
public sealed class AxolotlAnimationTests
{
    private const float Deg = MathF.PI / 180f;

    private static EntityPose Apply(KeyframeAnimation animation, float seconds, float scale = 1f)
    {
        var pose = new EntityPose();
        animation.Apply(seconds, scale, pose);
        return pose;
    }

    private static EntityPose ApplyAdult(
        float ageInTicks,
        float bodyYawDegrees,
        float pitchDegrees,
        float playingDead,
        float inWater,
        float onGround,
        float moving)
    {
        var pose = new EntityPose();
        AxolotlAnimations.ApplyAdult(pose, ageInTicks, bodyYawDegrees, pitchDegrees, playingDead, inWater, onGround, moving);
        return pose;
    }

    // ----- Adult procedural factors -----

    [Fact]
    public void AdultPlayDeadRotatesBodyAndMirrorsTheLeftLegs()
    {
        // playingDead=1, everything else 0 -> mirroredLegsFactor = 1 - min(0, 0) = 1.
        var pose = ApplyAdult(0f, 0f, 0f, playingDead: 1f, inWater: 0f, onGround: 0f, moving: 0f);

        Assert.Equal(-0.15f, pose.Get("body").Rotation.X, 4);
        Assert.Equal(0.35f, pose.Get("body").Rotation.Z, 4);
        Assert.Equal(1.4137167f, pose.Get("left_hind_leg").Rotation.X, 4);
        Assert.Equal(1.0995574f, pose.Get("left_hind_leg").Rotation.Y, 4);
        Assert.Equal(MathF.PI / 4f, pose.Get("left_hind_leg").Rotation.Z, 4);
        Assert.Equal(MathF.PI / 4f, pose.Get("left_front_leg").Rotation.X, 4);
        Assert.Equal(2.042035f, pose.Get("left_front_leg").Rotation.Y, 4);

        // Mirror: right legs copy left legs with Y/Z negated.
        Assert.Equal(1.4137167f, pose.Get("right_hind_leg").Rotation.X, 4);
        Assert.Equal(-1.0995574f, pose.Get("right_hind_leg").Rotation.Y, 4);
        Assert.Equal(-MathF.PI / 4f, pose.Get("right_hind_leg").Rotation.Z, 4);
        Assert.Equal(MathF.PI / 4f, pose.Get("right_front_leg").Rotation.X, 4);
        Assert.Equal(-2.042035f, pose.Get("right_front_leg").Rotation.Y, 4);
    }

    [Fact]
    public void AdultSwimmingUsesTheBlendedFactorAtRestPhaseZero()
    {
        // inWater=1, moving=1 -> swim factor 1; age 0 => sin 0, cos 1.
        var pose = ApplyAdult(0f, 0f, 0f, playingDead: 0f, inWater: 1f, onGround: 0f, moving: 1f);

        Assert.Equal(-0.45f, pose.Get("body").Position.Y, 4);
        Assert.Equal(-0.8f, pose.Get("top_gills").Rotation.X, 4);
        Assert.Equal(0.9f, pose.Get("left_gills").Rotation.Y, 4);
        Assert.Equal(-0.9f, pose.Get("right_gills").Rotation.Y, 4);
        Assert.Equal(0.3f, pose.Get("tail").Rotation.Y, 4);
        Assert.Equal(1.8849558f, pose.Get("left_hind_leg").Rotation.X, 4);
        Assert.Equal(MathF.PI / 2f, pose.Get("left_hind_leg").Rotation.Z, 4);
        Assert.Equal(-0.3f, pose.Get("left_front_leg").Rotation.Y, 4);
        Assert.Equal(1.8849558f, pose.Get("right_hind_leg").Rotation.X, 4);
        Assert.Equal(0.3f, pose.Get("right_front_leg").Rotation.Y, 4);
    }

    [Fact]
    public void AdultLayStillOnGroundUsesTheReferenceSwayFormulas()
    {
        // onGround=1, moving=0, inWater=0 -> lay-still factor min(1, 1) = 1; age 0 => sin 0, cos 1.
        var pose = ApplyAdult(0f, 0f, 0f, playingDead: 0f, inWater: 0f, onGround: 1f, moving: 0f);

        Assert.Equal(-0.2f, pose.Get("head").Rotation.Z, 4);
        Assert.Equal(-0.1f, pose.Get("tail").Rotation.Y, 4);
        Assert.Equal(0.65f, pose.Get("top_gills").Rotation.X, 4);
        Assert.Equal(-0.65f, pose.Get("left_gills").Rotation.Y, 4);
        Assert.Equal(0.65f, pose.Get("right_gills").Rotation.Y, 4);
        Assert.Equal(1.1f, pose.Get("left_hind_leg").Rotation.X, 4);
        Assert.Equal(1.0f, pose.Get("left_hind_leg").Rotation.Y, 4);
        Assert.Equal(-1.0f, pose.Get("right_hind_leg").Rotation.Y, 4);
    }

    [Fact]
    public void AdultBodyYawIsAddedIndependentlyOfTheStateFactors()
    {
        var pose = ApplyAdult(3f, bodyYawDegrees: 90f, pitchDegrees: 0f, playingDead: 0f, inWater: 0f, onGround: 0f, moving: 0f);

        Assert.Equal(MathF.PI / 2f, pose.Get("body").Rotation.Y, 4);
        Assert.Equal(0f, pose.Get("body").Rotation.X, 4);
    }

    [Fact]
    public void AdultZeroFactorsProduceNoPoseOffset()
    {
        var pose = ApplyAdult(12f, 0f, 0f, playingDead: 0f, inWater: 0f, onGround: 0f, moving: 0f);

        Assert.Equal(Vector3.Zero, pose.Get("body").Rotation);
        Assert.Equal(Vector3.Zero, pose.Get("body").Position);
        Assert.Equal(Vector3.Zero, pose.Get("left_hind_leg").Rotation);
        Assert.Equal(Vector3.Zero, pose.Get("right_hind_leg").Rotation);
    }

    // ----- Baby keyframe clips -----

    [Fact]
    public void BabySwimFirstFrameMatchesTheReferenceKeyframes()
    {
        var pose = Apply(AxolotlAnimations.BabySwimBaked, 0f);

        Assert.Equal(8f * Deg, pose.Get("body").Rotation.X, 4);
        Assert.Equal(-72f * Deg, pose.Get("left_gills").Rotation.Y, 4);
        Assert.Equal(72f * Deg, pose.Get("right_gills").Rotation.Y, 4);
        Assert.Equal(-57f * Deg, pose.Get("top_gills").Rotation.X, 4);
        Assert.Equal(15f * Deg, pose.Get("tail").Rotation.Y, 4);
        Assert.Equal(Vector3.Zero, pose.Get("body").Position);
    }

    [Fact]
    public void BabySwimPositionKeyUsesTheReferenceYInversion()
    {
        // Reference position keyframe at 0.52 is posVec(0, -1, 0); KeyframeAnimations.WorldPosVec inverts Y.
        var pose = Apply(AxolotlAnimations.BabySwimBaked, 0.52f);

        Assert.Equal(new Vector3(0f, 1f, 0f), pose.Get("body").Position);
    }

    [Fact]
    public void BabySwimLinearlyInterpolatesTheFinalSegment()
    {
        // Last keys: 0.68 -> -7.5 deg (linear toward the 1.0 key at 8 deg). At 0.84 alpha = 0.5.
        var pose = Apply(AxolotlAnimations.BabySwimBaked, 0.84f);

        Assert.Equal(0.25f * Deg, pose.Get("body").Rotation.X, 4);
    }

    [Fact]
    public void LoopingClipsWrapModuloTheirLength()
    {
        var start = Apply(AxolotlAnimations.BabyIdleFloorBaked, 0f);
        var wrapped = Apply(AxolotlAnimations.BabyIdleFloorBaked, 2.88f);
        Assert.Equal(start.Get("head").Rotation, wrapped.Get("head").Rotation);
        Assert.Equal(start.Get("top_gills").Rotation, wrapped.Get("top_gills").Rotation);

        var swimA = Apply(AxolotlAnimations.BabySwimBaked, 0.5f);
        var swimB = Apply(AxolotlAnimations.BabySwimBaked, 1.5f);
        Assert.Equal(swimA.Get("body").Rotation, swimB.Get("body").Rotation);
    }

    [Fact]
    public void BabyIdleFloorHeadHoldsBetweenEqualLinearKeys()
    {
        var pose = Apply(AxolotlAnimations.BabyIdleFloorBaked, 1.44f);

        Assert.Equal(-6.45f * Deg, pose.Get("head").Rotation.X, 4);
        Assert.Equal(-1.45f * Deg, pose.Get("head").Rotation.Y, 4);
        Assert.Equal(-6.5f * Deg, pose.Get("head").Rotation.Z, 4);
    }

    [Fact]
    public void NonLoopingPlayDeadStaysAtItsSingleKeyframe()
    {
        Assert.Equal(0.04f, AxolotlAnimations.PlayDead.LengthInSeconds, 5);
        Assert.False(AxolotlAnimations.PlayDead.Looping);

        var pose = Apply(AxolotlAnimations.PlayDeadBaked, 0.5f);

        Assert.Equal(30f * Deg, pose.Get("body").Rotation.Z, 4);
        Assert.Equal(Vector3.Zero, pose.Get("body").Scale);
        Assert.Equal(-18.67f * Deg, pose.Get("tail").Rotation.Y, 4);
        Assert.Equal(43.21f * Deg, pose.Get("left_gills").Rotation.Y, 4);
        Assert.Equal(418f * Deg, pose.Get("right_hind_leg").Rotation.X, 4);
    }

    [Fact]
    public void ClipMetadataMatchesTheReferenceLengthsAndLooping()
    {
        Assert.True(AxolotlAnimations.BabySwim.Looping);
        Assert.Equal(1f, AxolotlAnimations.BabySwim.LengthInSeconds, 5);
        Assert.Equal(2.88f, AxolotlAnimations.BabyIdleFloor.LengthInSeconds, 5);
        Assert.Equal(5.12f, AxolotlAnimations.IdleFloorUnderwater.LengthInSeconds, 5);
        Assert.Equal(5.2f, AxolotlAnimations.IdleUnderwater.LengthInSeconds, 5);
        Assert.Equal(2.72f, AxolotlAnimations.WalkFloor.LengthInSeconds, 5);
        Assert.Equal(2.04f, AxolotlAnimations.WalkFloorUnderwater.LengthInSeconds, 5);
    }

    [Fact]
    public void BakedClipsOnlyReferencePartsThatExistInTheModel()
    {
        Assert.Contains("body", AxolotlAnimations.PartNames);
        Assert.Contains("top_gills", AxolotlAnimations.PartNames);
        Assert.DoesNotContain("root", AxolotlAnimations.PartNames);
        Assert.DoesNotContain("root", AxolotlAnimations.PlayDead.BoneAnimations.Keys);
    }
}
