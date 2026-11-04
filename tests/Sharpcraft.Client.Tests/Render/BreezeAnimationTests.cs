// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Breeze animation clips ported from the reference BreezeAnimation data. The six clips are keyframe data, so these
/// tests pin the reference lengths/looping, a few representative frames per clip, the part-name contract with the
/// model definition, and the provider behaviour (clip names, Apply is a documented no-op, ApplyClip plays each clip).
/// </summary>
public sealed class BreezeAnimationTests
{
    private const float ToRad = MathF.PI / 180f;

    private static EntityPose Apply(KeyframeAnimation animation, float seconds)
    {
        var pose = new EntityPose();
        animation.Apply(seconds, 1f, pose);
        return pose;
    }

    private static void AssertVec(Vector3 expected, Vector3 actual, int precision = 4)
    {
        Assert.Equal(expected.X, actual.X, precision);
        Assert.Equal(expected.Y, actual.Y, precision);
        Assert.Equal(expected.Z, actual.Z, precision);
    }

    [Fact]
    public void ClipsDeclareReferenceLengthsAndLooping()
    {
        Assert.Equal(2.0f, BreezeAnimations.Idle.LengthInSeconds, 4);
        Assert.True(BreezeAnimations.Idle.Looping);

        Assert.Equal(1.125f, BreezeAnimations.Shoot.LengthInSeconds, 4);
        Assert.False(BreezeAnimations.Shoot.Looping);

        Assert.Equal(0.2f, BreezeAnimations.Slide.LengthInSeconds, 4);
        Assert.False(BreezeAnimations.Slide.Looping);

        Assert.Equal(0.1f, BreezeAnimations.SlideBack.LengthInSeconds, 4);
        Assert.False(BreezeAnimations.SlideBack.Looping);

        Assert.Equal(2.0f, BreezeAnimations.Inhale.LengthInSeconds, 4);
        Assert.False(BreezeAnimations.Inhale.Looping);

        Assert.Equal(0.5f, BreezeAnimations.Jump.LengthInSeconds, 4);
        Assert.False(BreezeAnimations.Jump.Looping);
    }

    [Fact]
    public void IdleStartsNeutralAndRotatesTheRodsAtTheHalfwayPoint()
    {
        var start = Apply(BreezeAnimations.IdleBaked, 0f);
        AssertVec(new Vector3(0.5f, 0.0f, 0.0f), start.Get("wind_top").Position);
        AssertVec(new Vector3(0.5f, 0.0f, -0.5f), start.Get("wind_mid").Position);
        AssertVec(Vector3.Zero, start.Get("head").Position);
        AssertVec(Vector3.Zero, start.Get("rods").Rotation);
        AssertVec(Vector3.Zero, start.Get("rods").Position);

        var half = Apply(BreezeAnimations.IdleBaked, 1.0f);
        AssertVec(new Vector3(0.0f, -1.0f, 0.0f), half.Get("head").Position);
        AssertVec(new Vector3(0.0f, 1.0f, 0.0f), half.Get("rods").Position);
        Assert.Equal(540f * ToRad, half.Get("rods").Rotation.Y, 4);
        AssertVec(new Vector3(-0.5f, 0.0f, 0.5f), half.Get("wind_mid").Position);
        AssertVec(new Vector3(-0.5f, 0.0f, 0.0f), half.Get("wind_top").Position);
    }

    [Fact]
    public void IdleLoopsOverItsExactLength()
    {
        var quarter = Apply(BreezeAnimations.IdleBaked, 0.25f);
        AssertVec(new Vector3(0.5f, 0.0f, -0.5f), quarter.Get("wind_top").Position);

        var wrapped = Apply(BreezeAnimations.IdleBaked, BreezeAnimations.Idle.LengthInSeconds + 0.25f);
        AssertVec(quarter.Get("wind_top").Position, wrapped.Get("wind_top").Position);
    }

    [Fact]
    public void ShootPullsTheBodyBackAndFlicksItForward()
    {
        var start = Apply(BreezeAnimations.ShootBaked, 0f);
        AssertVec(Vector3.Zero, start.Get("head").Rotation);
        AssertVec(Vector3.Zero, start.Get("body").Position);
        AssertVec(Vector3.Zero, start.Get("wind_mid").Position);

        var windUp = Apply(BreezeAnimations.ShootBaked, 0.25f);
        Assert.Equal(-12.5f * ToRad, windUp.Get("head").Rotation.X, 4);
        Assert.Equal(12.5f * ToRad, windUp.Get("body").Rotation.X, 4);
        Assert.Equal(12.5f * ToRad, windUp.Get("wind_mid").Rotation.X, 4);
        Assert.Equal(15f * ToRad, windUp.Get("wind_top").Rotation.X, 4);
        AssertVec(new Vector3(0.0f, -3.0f, 5.0f), windUp.Get("body").Position);
        AssertVec(new Vector3(0.0f, 0.0f, 5.0f), windUp.Get("wind_mid").Position);
        AssertVec(new Vector3(0.0f, 0.0f, 3.0f), windUp.Get("wind_top").Position);

        var hold = Apply(BreezeAnimations.ShootBaked, 0.75f);
        Assert.Equal(-12.5f * ToRad, hold.Get("head").Rotation.X, 4);

        var pushed = Apply(BreezeAnimations.ShootBaked, 0.8333f);
        AssertVec(new Vector3(0.0f, -3.0f, 6.0f), pushed.Get("body").Position);

        var rods = Apply(BreezeAnimations.ShootBaked, 1.0f);
        Assert.Equal(360f * ToRad, rods.Get("rods").Rotation.Y, 4);

        var end = Apply(BreezeAnimations.ShootBaked, BreezeAnimations.Shoot.LengthInSeconds);
        AssertVec(Vector3.Zero, end.Get("head").Rotation);
        AssertVec(Vector3.Zero, end.Get("body").Position);
    }

    [Fact]
    public void SlidePushesTheBodyAndWindShellsForward()
    {
        var start = Apply(BreezeAnimations.SlideBaked, 0f);
        AssertVec(Vector3.Zero, start.Get("body").Position);

        var mid = Apply(BreezeAnimations.SlideBaked, 0.1f);
        AssertVec(new Vector3(0.0f, 0.0f, -3.0f), mid.Get("body").Position);
        AssertVec(new Vector3(0.0f, 0.0f, -1.5f), mid.Get("wind_mid").Position);
        AssertVec(new Vector3(0.0f, 0.0f, -1.0f), mid.Get("wind_top").Position);

        var end = Apply(BreezeAnimations.SlideBaked, BreezeAnimations.Slide.LengthInSeconds);
        AssertVec(new Vector3(0.0f, 0.0f, -6.0f), end.Get("body").Position);
        AssertVec(new Vector3(0.0f, 0.0f, -3.0f), end.Get("wind_mid").Position);
        AssertVec(new Vector3(0.0f, 0.0f, -2.0f), end.Get("wind_top").Position);
    }

    [Fact]
    public void SlideBackReturnsFromTheSlidePoseToNeutral()
    {
        var start = Apply(BreezeAnimations.SlideBackBaked, 0f);
        AssertVec(new Vector3(0.0f, 0.0f, -6.0f), start.Get("body").Position);
        AssertVec(new Vector3(0.0f, 0.0f, -3.0f), start.Get("wind_mid").Position);
        AssertVec(new Vector3(0.0f, 0.0f, -2.0f), start.Get("wind_top").Position);

        var mid = Apply(BreezeAnimations.SlideBackBaked, 0.05f);
        AssertVec(new Vector3(0.0f, 0.0f, -3.0f), mid.Get("body").Position);

        var end = Apply(BreezeAnimations.SlideBackBaked, BreezeAnimations.SlideBack.LengthInSeconds);
        AssertVec(Vector3.Zero, end.Get("body").Position);
    }

    [Fact]
    public void InhaleSinksTheBodyAndHoldsThroughTheEnd()
    {
        var start = Apply(BreezeAnimations.InhaleBaked, 0f);
        AssertVec(Vector3.Zero, start.Get("body").Position);

        var suck = Apply(BreezeAnimations.InhaleBaked, 0.5f);
        AssertVec(new Vector3(0.0f, 10.0f, 0.0f), suck.Get("body").Position);
        Assert.Equal(22.5f * ToRad, suck.Get("head").Rotation.X, 4);
        AssertVec(new Vector3(0.0f, 6.0f, 0.0f), suck.Get("wind_mid").Position);
        AssertVec(new Vector3(0.0f, 5.0f, 0.0f), suck.Get("wind_top").Position);

        var hold = Apply(BreezeAnimations.InhaleBaked, 0.625f);
        AssertVec(new Vector3(0.0f, 10.0f, 0.0f), hold.Get("body").Position);
        AssertVec(new Vector3(0.0f, 6.0f, 0.0f), hold.Get("wind_mid").Position);
        Assert.Equal(360f * ToRad, hold.Get("rods").Rotation.Y, 4);
    }

    [Fact]
    public void JumpSpringsTheBodyAndStretchesTheWindBody()
    {
        var start = Apply(BreezeAnimations.JumpBaked, 0f);
        AssertVec(new Vector3(0.0f, 10.0f, 0.0f), start.Get("body").Position);
        Assert.Equal(22.5f * ToRad, start.Get("head").Rotation.X, 4);
        Assert.Equal(90f * ToRad, start.Get("wind_bottom").Rotation.Y, 4);
        AssertVec(new Vector3(0.0f, 6.0f, 0.0f), start.Get("wind_mid").Position);
        AssertVec(new Vector3(0.0f, 5.0f, 0.0f), start.Get("wind_top").Position);
        AssertVec(Vector3.Zero, start.Get("wind_body").Scale);

        var rise = Apply(BreezeAnimations.JumpBaked, 0.125f);
        AssertVec(new Vector3(0.0f, -11.0f, 0.0f), rise.Get("body").Position);
        AssertVec(new Vector3(0.0f, 0.3f, 0.0f), rise.Get("wind_body").Scale);
        AssertVec(new Vector3(0.0f, 0.1f, 0.0f), rise.Get("wind_bottom").Scale);
        AssertVec(new Vector3(0.0f, -2.0f, 0.0f), rise.Get("wind_mid").Position);

        var dip = Apply(BreezeAnimations.JumpBaked, 0.2083f);
        Assert.Equal(-19.25f * ToRad, dip.Get("head").Rotation.X, 4);

        var end = Apply(BreezeAnimations.JumpBaked, BreezeAnimations.Jump.LengthInSeconds);
        AssertVec(Vector3.Zero, end.Get("body").Position);
        Assert.Equal(360f * ToRad, end.Get("rods").Rotation.Y, 4);
    }

    [Fact]
    public void EveryAnimatedBoneExistsInTheBreezeModel()
    {
        var modelParts = BreezeModelDefinition.Definition.Parts.Select(part => part.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var definition in new[]
                 {
                     BreezeAnimations.Idle,
                     BreezeAnimations.Shoot,
                     BreezeAnimations.Slide,
                     BreezeAnimations.SlideBack,
                     BreezeAnimations.Inhale,
                     BreezeAnimations.Jump
                 })
        {
            foreach (var partName in definition.BoneAnimations.Keys)
            {
                Assert.Contains(partName, modelParts);
            }
        }
    }

    [Fact]
    public void ProviderAdvertisesTheSixReferenceClips()
    {
        var provider = new BreezeAnimationProvider(BreezeModelDefinition.BreezeEntityTypeId);
        Assert.Equal(BreezeModelDefinition.BreezeEntityTypeId, provider.TypeId);
        Assert.Equal(
            new[] { "idle", "shoot", "slide", "slide_back", "inhale", "jump" },
            provider.Clips);
        Assert.NotNull(provider.CreateState());
    }

    [Fact]
    public void ApplyWritesNothingBecauseThePerClipClocksAreMissing()
    {
        // The reference drives all six clips from BreezeRenderState AnimationState clocks that EntityAnimationFrame
        // does not carry, so the hot path must not guess a time base.
        var provider = new BreezeAnimationProvider(BreezeModelDefinition.BreezeEntityTypeId);
        var state = provider.CreateState();
        var frame = new EntityAnimationFrame(
            TypeId: BreezeModelDefinition.BreezeEntityTypeId,
            AgeInTicks: 40f,
            WalkAnimationPos: 3.25f,
            WalkAnimationSpeed: 0.8f,
            Pitch: 15f,
            Yaw: -30f,
            Flap: 0f,
            FlapSpeed: 0f,
            TentacleAngle: 0f,
            IsResting: false,
            IsBaby: false,
            IsHidingInShell: false);
        var pose = new EntityPose();

        provider.Apply(state, in frame, pose);

        Assert.Empty(pose.Offsets);
    }

    [Fact]
    public void ApplyClipPlaysEachClipAtExplicitSeconds()
    {
        var provider = new BreezeAnimationProvider(BreezeModelDefinition.BreezeEntityTypeId);
        var state = provider.CreateState();
        var frame = default(EntityAnimationFrame);
        var pose = new EntityPose();

        provider.ApplyClip(state, in frame, "slide", 0.1f, pose);
        AssertVec(new Vector3(0.0f, 0.0f, -3.0f), pose.Get("body").Position);

        pose.Clear();
        provider.ApplyClip(state, in frame, "jump", 0.125f, pose);
        AssertVec(new Vector3(0.0f, -11.0f, 0.0f), pose.Get("body").Position);

        pose.Clear();
        provider.ApplyClip(state, in frame, "idle", 0.25f, pose);
        AssertVec(new Vector3(0.5f, 0.0f, -0.5f), pose.Get("wind_top").Position);

        pose.Clear();
        provider.ApplyClip(state, in frame, "jump", 0.2083f, pose);
        Assert.Equal(-19.25f * ToRad, pose.Get("head").Rotation.X, 4);
    }
}
