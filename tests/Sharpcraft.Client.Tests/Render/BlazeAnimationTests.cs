// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Blaze procedural pose coverage ported from the reference <c>BlazeModel.setupAnim</c>. Pins the three ring formulas
/// (radius, spin rate and per-rod vertical bob), the additive-over-bind deltas (y negated for the y-up pivot convention),
/// the head look, and the part-name contract with the Blaze model.
/// </summary>
public sealed class BlazeAnimationTests
{
    private static readonly int TypeId = BlazeModelDefinition.BlazeEntityTypeId;

    private static float Rad(float degrees) => degrees * (MathF.PI / 180f);

    private static EntityAnimationFrame Frame(float ageInTicks = 0f, float pitch = 0f, float yaw = 0f) =>
        new(
            TypeId,
            ageInTicks,
            WalkAnimationPos: 0f,
            WalkAnimationSpeed: 0f,
            Pitch: pitch,
            Yaw: yaw,
            Flap: 0f,
            FlapSpeed: 0f,
            TentacleAngle: 0f,
            IsResting: false,
            IsBaby: false,
            IsHidingInShell: false);

    private static void AssertVec(Vector3 expected, Vector3 actual, int precision = 3)
    {
        Assert.Equal(expected.X, actual.X, precision);
        Assert.Equal(expected.Y, actual.Y, precision);
        Assert.Equal(expected.Z, actual.Z, precision);
    }

    [Fact]
    public void ProviderExposesTheBlazeTypeIdAndIdleClip()
    {
        var provider = new BlazeAnimationProvider(TypeId);
        Assert.Equal(TypeId, provider.TypeId);
        Assert.Equal(new[] { "idle" }, provider.Clips);
    }

    [Fact]
    public void EveryModelPartIsDrivenByTheApplyPose()
    {
        var provider = new BlazeAnimationProvider(TypeId);
        var pose = new EntityPose();
        provider.Apply(provider.CreateState(), Frame(ageInTicks: 5f), pose);

        var expected = new HashSet<string>(StringComparer.Ordinal) { "head" };
        for (var i = 0; i < 12; i++)
        {
            expected.Add("part" + i);
        }

        Assert.True(expected.SetEquals(pose.Offsets.Keys), "the pose must drive exactly the head and all twelve rods");

        var modelParts = BlazeModelDefinition.Definition.Parts.Select(part => part.Name).ToHashSet(StringComparer.Ordinal);
        Assert.True(expected.SetEquals(modelParts), "the driven parts must match the Blaze model definition");
    }

    [Fact]
    public void AtAgeZeroTheReferenceAnimationReproducesItsBindPose()
    {
        var provider = new BlazeAnimationProvider(TypeId);
        var pose = new EntityPose();
        provider.Apply(provider.CreateState(), Frame(ageInTicks: 0f), pose);

        for (var i = 0; i < 12; i++)
        {
            AssertVec(Vector3.Zero, pose.Get("part" + i).Position);
        }
    }

    [Fact]
    public void RingZeroRodPositionsMatchTheReferenceAtAgeFive()
    {
        var provider = new BlazeAnimationProvider(TypeId);
        var pose = new EntityPose();
        provider.Apply(provider.CreateState(), Frame(ageInTicks: 5f), pose);

        // Ring 0 spins at -0.1π/tick; at age 5 the base is exactly -π/2.
        AssertVec(new Vector3(-9f, 0.68468f, -9f), pose.Get("part0").Position);
        AssertVec(new Vector3(9f, 1.05583f, -9f), pose.Get("part1").Position);
        AssertVec(new Vector3(9f, 1.16848f, 9f), pose.Get("part2").Position);
        AssertVec(new Vector3(-9f, 0.99504f, 9f), pose.Get("part3").Position);
    }

    [Fact]
    public void RingOneRodPositionsMatchTheReferenceAtAgeFive()
    {
        var provider = new BlazeAnimationProvider(TypeId);
        var pose = new EntityPose();
        provider.Apply(provider.CreateState(), Frame(ageInTicks: 5f), pose);

        AssertVec(new Vector3(-2.78663f, 0.57798f, 1.70765f), pose.Get("part4").Position);
        AssertVec(new Vector3(-1.70765f, 0.01942f, -2.78663f), pose.Get("part5").Position);
        AssertVec(new Vector3(2.78663f, -0.54391f, -1.70765f), pose.Get("part6").Position);
        AssertVec(new Vector3(1.70765f, -0.97406f, 2.78663f), pose.Get("part7").Position);
    }

    [Fact]
    public void RingTwoRodPositionsMatchTheReferenceAtAgeFive()
    {
        var provider = new BlazeAnimationProvider(TypeId);
        var pose = new EntityPose();
        provider.Apply(provider.CreateState(), Frame(ageInTicks: 5f), pose);

        AssertVec(new Vector3(0.30025f, 1.56218f, -3.81504f), pose.Get("part8").Position);
        AssertVec(new Vector3(3.81504f, 1.87777f, 0.30025f), pose.Get("part9").Position);
        AssertVec(new Vector3(-0.30025f, 1.18571f, 3.81504f), pose.Get("part10").Position);
        AssertVec(new Vector3(-3.81504f, -0.14263f, -0.30025f), pose.Get("part11").Position);
    }

    [Fact]
    public void RodPositionsMatchTheReferenceAtAgeSevenAndAHalf()
    {
        var provider = new BlazeAnimationProvider(TypeId);
        var pose = new EntityPose();
        provider.Apply(provider.CreateState(), Frame(ageInTicks: 7.5f), pose);

        AssertVec(new Vector3(-15.36396f, 1.29953f, -6.36396f), pose.Get("part0").Position);
        AssertVec(new Vector3(15.36396f, 1.50498f, 6.36396f), pose.Get("part2").Position);
        AssertVec(new Vector3(4.40053f, -1.15189f, -2.02867f), pose.Get("part6").Position);
        AssertVec(new Vector3(5.51719f, 1.36854f, -0.65300f), pose.Get("part9").Position);
    }

    [Fact]
    public void TheThreeRingsSpinIndependently()
    {
        var provider = new BlazeAnimationProvider(TypeId);
        var pose = new EntityPose();
        provider.Apply(provider.CreateState(), Frame(ageInTicks: 5f), pose);

        // The rings use radius 9/7/5 and different base angles/rates, so the first rod of each ring differs.
        var ring0 = pose.Get("part0").Position;
        var ring1 = pose.Get("part4").Position;
        var ring2 = pose.Get("part8").Position;
        Assert.NotEqual(ring0, ring1);
        Assert.NotEqual(ring1, ring2);
        Assert.True(MathF.Abs(ring0.X) > MathF.Abs(ring1.X), "the outer ring must swing further than the middle ring");
        Assert.True(MathF.Abs(ring1.X) > MathF.Abs(ring2.X), "the middle ring must swing further than the inner ring");

        // Ring 0 contracts while ring 1 expands between two ages (opposite spin directions).
        var later = new EntityPose();
        provider.Apply(provider.CreateState(), Frame(ageInTicks: 10f), later);
        Assert.True(MathF.Abs(later.Get("part0").Position.X) != MathF.Abs(ring0.X));
        Assert.True(MathF.Abs(later.Get("part4").Position.X) != MathF.Abs(ring1.X));
    }

    [Fact]
    public void HeadLooksAlongPitchAndYawInRadians()
    {
        var provider = new BlazeAnimationProvider(TypeId);
        var pose = new EntityPose();
        provider.Apply(provider.CreateState(), Frame(ageInTicks: 3f, pitch: 30f, yaw: -45f), pose);

        AssertVec(new Vector3(Rad(30f), Rad(-45f), 0f), pose.Get("head").Rotation);
    }

    [Fact]
    public void ApplyClipReconstructsThePoseAtSecondsTimesTwentyTicks()
    {
        var provider = new BlazeAnimationProvider(TypeId);
        var state = provider.CreateState();
        var frame = Frame(ageInTicks: 100f, pitch: 10f, yaw: 20f);

        var fromClip = new EntityPose();
        provider.ApplyClip(state, frame, "idle", seconds: 5f, fromClip);

        var directFrame = Frame(ageInTicks: 100f, pitch: 10f, yaw: 20f);
        var direct = new EntityPose();
        provider.Apply(state, directFrame, direct);

        // seconds 5 -> age 100 ticks, matching the frame's own age.
        foreach (var part in new[] { "head", "part0", "part4", "part8", "part11" })
        {
            AssertVec(direct.Get(part).Position, fromClip.Get(part).Position);
            AssertVec(direct.Get(part).Rotation, fromClip.Get(part).Rotation);
        }
    }

    [Fact]
    public void ApplyClipIgnoresUnknownNames()
    {
        var provider = new BlazeAnimationProvider(TypeId);
        var pose = new EntityPose();
        provider.ApplyClip(provider.CreateState(), Frame(ageInTicks: 5f), "not_a_clip", 1f, pose);
        Assert.Empty(pose.Offsets);
    }
}
