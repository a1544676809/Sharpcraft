// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Polar Bear pose coverage ported from <c>PolarBearModel.setupAnim</c> (which first runs <c>QuadrupedModel.setupAnim</c>).
/// Pins the base head look and four-leg walk swing, the settled standing rear for adult and baby, and the flat-model part
/// grouping (head siblings + body siblings).
/// </summary>
public sealed class PolarBearAnimationTests
{
    private static readonly int TypeId = PolarBearModelDefinition.PolarBearEntityTypeId;

    private static float Rad(float degrees) => degrees * (MathF.PI / 180f);

    private static EntityAnimationFrame Frame(
        float walkPos = 0f,
        float walkSpeed = 0f,
        float pitch = 0f,
        float yaw = 0f,
        bool isStanding = false,
        bool isBaby = false) =>
        new(
            TypeId,
            AgeInTicks: 0f,
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
            IsStanding: isStanding,
            StandScale: isStanding ? 1f : 0f);

    private static EntityPose Apply(PolarBearAnimationProvider provider, in EntityAnimationFrame frame)
    {
        var pose = new EntityPose();
        provider.Apply(provider.CreateState(), in frame, pose);
        return pose;
    }

    private static void AssertVec(Vector3 expected, Vector3 actual, int precision = 4)
    {
        Assert.Equal(expected.X, actual.X, precision);
        Assert.Equal(expected.Y, actual.Y, precision);
        Assert.Equal(expected.Z, actual.Z, precision);
    }

    [Fact]
    public void ProviderExposesThePolarBearTypeIdAndClips()
    {
        var provider = new PolarBearAnimationProvider(TypeId);
        Assert.Equal(TypeId, provider.TypeId);
        Assert.Equal(new[] { "idle", "walk", "stand" }, provider.Clips);
    }

    [Fact]
    public void BaseWalkSwingMatchesQuadrupedTheReference()
    {
        var provider = new PolarBearAnimationProvider(TypeId);

        // phase 0: cos(0) = 1, cos(π) = -1; right/left front copy the opposite hind leg.
        var pose = Apply(provider, Frame(walkPos: 0f, walkSpeed: 1f));
        Assert.Equal(1.4f, pose.Get("right_hind_leg").Rotation.X, 4);
        Assert.Equal(-1.4f, pose.Get("left_hind_leg").Rotation.X, 4);
        Assert.Equal(-1.4f, pose.Get("right_front_leg").Rotation.X, 4);
        Assert.Equal(1.4f, pose.Get("left_front_leg").Rotation.X, 4);
    }

    [Fact]
    public void BaseHeadLookDrivesTheWholeHeadGroup()
    {
        var provider = new PolarBearAnimationProvider(TypeId);
        var pose = Apply(provider, Frame(pitch: 30f, yaw: -45f));

        var expected = new Vector3(Rad(30f), Rad(-45f), 0f);
        foreach (var part in new[] { "head", "mouth", "right_ear", "left_ear" })
        {
            AssertVec(expected, pose.Get(part).Rotation);
            AssertVec(Vector3.Zero, pose.Get(part).Position);
        }
    }

    [Fact]
    public void NotStandingLeavesBodyAndFrontLegRearAtZero()
    {
        var provider = new PolarBearAnimationProvider(TypeId);
        var standing = Apply(provider, Frame(walkPos: 0f, walkSpeed: 0f, isStanding: false));

        AssertVec(Vector3.Zero, standing.Get("body").Rotation);
        AssertVec(Vector3.Zero, standing.Get("body").Position);
        AssertVec(Vector3.Zero, standing.Get("head").Position);
        AssertVec(Vector3.Zero, standing.Get("right_front_leg").Position);
        Assert.Equal(0f, standing.Get("right_front_leg").Rotation.X, 4);
    }

    [Fact]
    public void SettledStandingRearMatchesTheReferenceAdultPose()
    {
        var provider = new PolarBearAnimationProvider(TypeId);
        var pose = Apply(provider, Frame(isStanding: true));

        // head: x += π·0.15, y/z position += 24/13.
        Assert.Equal(MathF.PI * 0.15f, pose.Get("head").Rotation.X, 4);
        AssertVec(new Vector3(0f, 24f, 13f), pose.Get("head").Position);
        Assert.Equal(MathF.PI * 0.15f, pose.Get("mouth").Rotation.X, 4);
        AssertVec(new Vector3(0f, 24f, 13f), pose.Get("right_ear").Position);

        // body: xRot -= π·0.35, y += 2 (stored y negated).
        Assert.Equal(-MathF.PI * 0.35f, pose.Get("body").Rotation.X, 4);
        Assert.Equal(-MathF.PI * 0.35f, pose.Get("body_upper").Rotation.X, 4);
        AssertVec(new Vector3(0f, -2f, 0f), pose.Get("body").Position);

        // front legs: xRot -= π·0.45, y -= 20, z += 4 (stored y negated).
        Assert.Equal(-MathF.PI * 0.45f, pose.Get("right_front_leg").Rotation.X, 4);
        Assert.Equal(-MathF.PI * 0.45f, pose.Get("left_front_leg").Rotation.X, 4);
        AssertVec(new Vector3(0f, 20f, 4f), pose.Get("right_front_leg").Position);
        AssertVec(new Vector3(0f, 20f, 4f), pose.Get("left_front_leg").Position);

        // hind legs are untouched by standing.
        Assert.Equal(0f, pose.Get("right_hind_leg").Rotation.X, 4);
    }

    [Fact]
    public void SettledStandingScalesTheRearByTheBabyAgeScale()
    {
        var provider = new PolarBearAnimationProvider(TypeId);
        var pose = Apply(provider, Frame(isStanding: true, isBaby: true));

        // bodyAgeScale 0.5 applies to body.y and front-leg y/z only; head lift is unscaled.
        AssertVec(new Vector3(0f, -1f, 0f), pose.Get("body").Position);
        AssertVec(new Vector3(0f, 10f, 2f), pose.Get("right_front_leg").Position);
        AssertVec(new Vector3(0f, 24f, 13f), pose.Get("head").Position);
    }

    [Fact]
    public void StandingAndWalkingCompose()
    {
        var provider = new PolarBearAnimationProvider(TypeId);
        var pose = Apply(provider, Frame(walkPos: 0f, walkSpeed: 1f, isStanding: true));

        // rightFront base = cos(π)·1.4 = -1.4, then -π·0.45; leftFront base = 1.4, then -π·0.45.
        Assert.Equal(-1.4f - (MathF.PI * 0.45f), pose.Get("right_front_leg").Rotation.X, 3);
        Assert.Equal(1.4f - (MathF.PI * 0.45f), pose.Get("left_front_leg").Rotation.X, 3);
        // hind legs still swing.
        Assert.Equal(1.4f, pose.Get("right_hind_leg").Rotation.X, 4);
    }

    [Fact]
    public void ApplyDrivesTheWholeFlatModelPartSet()
    {
        var provider = new PolarBearAnimationProvider(TypeId);
        var pose = Apply(provider, Frame(walkPos: 2f, walkSpeed: 0.5f, isStanding: true));

        var expected = new HashSet<string>(StringComparer.Ordinal)
        {
            "head", "mouth", "right_ear", "left_ear", "body", "body_upper",
            "right_hind_leg", "left_hind_leg", "right_front_leg", "left_front_leg"
        };
        Assert.True(expected.SetEquals(pose.Offsets.Keys), "every flat model part must be driven");

        var modelParts = PolarBearModelDefinition.Definition.Parts.Select(part => part.Name).ToHashSet(StringComparer.Ordinal);
        Assert.True(expected.SetEquals(modelParts), "the driven parts must match the Polar Bear model definition");
    }

    [Fact]
    public void ApplyClipStandReconstructsTheSettledPose()
    {
        var provider = new PolarBearAnimationProvider(TypeId);
        var pose = new EntityPose();
        provider.ApplyClip(provider.CreateState(), Frame(), "stand", seconds: 0f, pose);

        Assert.Equal(MathF.PI * 0.15f, pose.Get("head").Rotation.X, 4);
        Assert.Equal(-MathF.PI * 0.35f, pose.Get("body").Rotation.X, 4);
        AssertVec(new Vector3(0f, 20f, 4f), pose.Get("right_front_leg").Position);
    }
}
