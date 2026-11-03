// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Render;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>Reference FrogAnimation keyframe data and part coverage.</summary>
public sealed class FrogAnimationTests
{
    private static float Rad(float degrees) => degrees * (MathF.PI / 180f);

    private static void AssertParts(AnimationDefinition definition, params string[] expected) =>
        Assert.Equal(
            expected.OrderBy(name => name, StringComparer.Ordinal),
            definition.BoneAnimations.Keys.OrderBy(name => name, StringComparer.Ordinal));

    [Fact]
    public void DefinitionsMatchTheReferenceLengthsAndLoopingFlags()
    {
        Assert.Equal(3f, FrogAnimations.Croak.LengthInSeconds, 4);
        Assert.False(FrogAnimations.Croak.Looping);

        Assert.Equal(1.25f, FrogAnimations.Walk.LengthInSeconds, 4);
        Assert.True(FrogAnimations.Walk.Looping);

        Assert.Equal(0.5f, FrogAnimations.Jump.LengthInSeconds, 4);
        Assert.False(FrogAnimations.Jump.Looping);

        Assert.Equal(0.5f, FrogAnimations.Tongue.LengthInSeconds, 4);
        Assert.False(FrogAnimations.Tongue.Looping);

        Assert.Equal(1.04167f, FrogAnimations.Swim.LengthInSeconds, 5);
        Assert.True(FrogAnimations.Swim.Looping);

        Assert.Equal(3f, FrogAnimations.IdleWater.LengthInSeconds, 4);
        Assert.True(FrogAnimations.IdleWater.Looping);
    }

    [Fact]
    public void EveryAnimatedBoneExistsInTheFrogModel()
    {
        var modelParts = new HashSet<string>(
            FrogModelDefinition.Definition.Parts.Select(part => part.Name),
            StringComparer.Ordinal);

        foreach (var definition in new[]
                 {
                     FrogAnimations.Croak, FrogAnimations.Walk, FrogAnimations.Jump,
                     FrogAnimations.Tongue, FrogAnimations.Swim, FrogAnimations.IdleWater
                 })
        {
            foreach (var bone in definition.BoneAnimations.Keys)
            {
                Assert.Contains(bone, modelParts);
            }
        }
    }

    [Fact]
    public void EachDefinitionCoversTheReferencePartSet()
    {
        AssertParts(FrogAnimations.Croak, "croaking_body");
        AssertParts(FrogAnimations.Walk, "left_arm", "right_arm", "left_leg", "right_leg", "body");
        AssertParts(FrogAnimations.Jump, "body", "left_arm", "right_arm", "left_leg", "right_leg");
        AssertParts(FrogAnimations.Tongue, "head", "tongue");
        AssertParts(FrogAnimations.Swim, "body", "left_arm", "right_arm", "left_leg", "right_leg");
        AssertParts(FrogAnimations.IdleWater, "body", "left_arm", "right_arm", "left_leg", "right_leg");
    }

    [Fact]
    public void InterpolationMatchesTheReferencePerState()
    {
        Assert.All(
            FrogAnimations.Croak.BoneAnimations["croaking_body"][1].Keyframes,
            frame => Assert.Equal(AnimationInterpolation.Linear, frame.Interpolation));
        Assert.All(
            FrogAnimations.Walk.BoneAnimations["body"][0].Keyframes,
            frame => Assert.Equal(AnimationInterpolation.Linear, frame.Interpolation));
        Assert.All(
            FrogAnimations.Jump.BoneAnimations["body"][0].Keyframes,
            frame => Assert.Equal(AnimationInterpolation.Linear, frame.Interpolation));
        Assert.All(
            FrogAnimations.Tongue.BoneAnimations["tongue"][1].Keyframes,
            frame => Assert.Equal(AnimationInterpolation.Linear, frame.Interpolation));
        Assert.All(
            FrogAnimations.Swim.BoneAnimations["left_arm"][0].Keyframes,
            frame => Assert.Equal(AnimationInterpolation.CatmullRom, frame.Interpolation));
        Assert.All(
            FrogAnimations.IdleWater.BoneAnimations["left_leg"][1].Keyframes,
            frame => Assert.Equal(AnimationInterpolation.CatmullRom, frame.Interpolation));
    }

    [Fact]
    public void CroakRaisesAndScalesTheCroakingBody()
    {
        var pose = new EntityPose();
        FrogAnimations.CroakBaked.Apply(0f, 1f, pose);
        var collapsed = pose.Get("croaking_body");
        Assert.Equal(0f, collapsed.Position.X, 5);
        Assert.Equal(0f, collapsed.Position.Y, 5);
        Assert.Equal(0f, collapsed.Position.Z, 5);
        Assert.Equal(-1f, collapsed.Scale.X, 5);

        pose.Clear();
        FrogAnimations.CroakBaked.Apply(0.4583f, 1f, pose);
        var raised = pose.Get("croaking_body");
        Assert.Equal(-1f, raised.Position.Y, 5);
        Assert.Equal(0f, raised.Scale.X, 5);

        pose.Clear();
        FrogAnimations.CroakBaked.Apply(0.5417f, 1f, pose);
        var pulse = pose.Get("croaking_body");
        Assert.Equal(0.3f, pulse.Scale.X, 5);
        Assert.Equal(1.1f, pulse.Scale.Y, 5);
        Assert.Equal(0.6f, pulse.Scale.Z, 5);

        pose.Clear();
        FrogAnimations.CroakBaked.Apply(1f, 1f, pose);
        Assert.Equal(-1f, pose.Get("croaking_body").Position.Y, 5);
        Assert.Equal(0f, pose.Get("croaking_body").Scale.X, 5);
    }

    [Fact]
    public void JumpHoldsTheTakeOffPoseAndScalesWithTargetScale()
    {
        var pose = new EntityPose();
        FrogAnimations.JumpBaked.Apply(0f, 1f, pose);
        Assert.Equal(Rad(-22.5f), pose.Get("body").Rotation.X, 4);
        Assert.Equal(Rad(-56.14f), pose.Get("left_arm").Rotation.X, 4);
        Assert.Equal(Rad(-56.14f), pose.Get("right_arm").Rotation.X, 4);
        Assert.Equal(Rad(45f), pose.Get("left_leg").Rotation.X, 4);
        Assert.Equal(-1f, pose.Get("left_arm").Position.Y, 5);

        pose.Clear();
        FrogAnimations.JumpBaked.Apply(0.5f, 1f, pose);
        Assert.Equal(Rad(-22.5f), pose.Get("body").Rotation.X, 4);

        pose.Clear();
        FrogAnimations.JumpBaked.Apply(0f, 0.5f, pose);
        Assert.Equal(Rad(-11.25f), pose.Get("body").Rotation.X, 4);
    }

    [Fact]
    public void WalkPosesAndLoopMatchTheReference()
    {
        var pose = new EntityPose();
        FrogAnimations.WalkBaked.Apply(0f, 1f, pose);
        Assert.Equal(Rad(-5f), pose.Get("left_arm").Rotation.Y, 4);
        Assert.Equal(Rad(5f), pose.Get("body").Rotation.Y, 4);
        Assert.Equal(Rad(-33.75f), pose.Get("right_leg").Rotation.X, 4);
        Assert.Equal(-0.1f, pose.Get("left_leg").Position.Y, 5);
        Assert.Equal(1.2f, pose.Get("left_leg").Position.Z, 5);

        pose.Clear();
        FrogAnimations.WalkBaked.Apply(0.625f, 1f, pose);
        Assert.Equal(0f, pose.Get("left_arm").Rotation.X, 5);
        Assert.Equal(Rad(-5f), pose.Get("body").Rotation.Y, 4);

        pose.Clear();
        FrogAnimations.WalkBaked.Apply(0f, 1f, pose);
        var atStart = pose.Get("body").Rotation.Y;
        pose.Clear();
        FrogAnimations.WalkBaked.Apply(FrogAnimations.Walk.LengthInSeconds, 1f, pose);
        Assert.Equal(atStart, pose.Get("body").Rotation.Y, 4);
    }

    [Fact]
    public void TongueExtendsAndRetractsPerTheReference()
    {
        var pose = new EntityPose();
        FrogAnimations.TongueBaked.Apply(0.0833f, 1f, pose);
        Assert.Equal(Rad(-60f), pose.Get("head").Rotation.X, 4);
        // The reference builds the head scale track with degreeVec, so the values are tiny radian offsets.
        Assert.Equal(Rad(0.998f), pose.Get("head").Scale.X, 5);
        Assert.Equal(Rad(1f), pose.Get("head").Scale.Y, 5);

        pose.Clear();
        FrogAnimations.TongueBaked.Apply(0.1667f, 1f, pose);
        Assert.Equal(-0.5f, pose.Get("tongue").Scale.X, 5);
        Assert.Equal(0f, pose.Get("tongue").Scale.Y, 5);
        Assert.Equal(4f, pose.Get("tongue").Scale.Z, 5);

        pose.Clear();
        FrogAnimations.TongueBaked.Apply(0.5f, 1f, pose);
        Assert.Equal(0f, pose.Get("head").Rotation.X, 5);
    }

    [Fact]
    public void SwimKeyframesFollowTheReferenceCurves()
    {
        var pose = new EntityPose();
        FrogAnimations.SwimBaked.Apply(0f, 1f, pose);
        Assert.Equal(Rad(90f), pose.Get("left_arm").Rotation.X, 4);
        Assert.Equal(Rad(22.5f), pose.Get("left_arm").Rotation.Y, 4);
        Assert.Equal(0.64f, pose.Get("left_arm").Position.Y, 4);
        Assert.Equal(2f, pose.Get("left_arm").Position.Z, 4);

        pose.Clear();
        FrogAnimations.SwimBaked.Apply(0.3333f, 1f, pose);
        Assert.Equal(Rad(10f), pose.Get("body").Rotation.X, 4);

        pose.Clear();
        FrogAnimations.SwimBaked.Apply(0.6667f, 1f, pose);
        Assert.Equal(Rad(-10f), pose.Get("body").Rotation.X, 4);
        Assert.Equal(Rad(-22.5f), pose.Get("left_arm").Rotation.X, 4);
        Assert.Equal(Rad(-22.5f), pose.Get("right_arm").Rotation.X, 4);
    }

    [Fact]
    public void SwimLoopsOverItsExactLength()
    {
        var pose = new EntityPose();
        FrogAnimations.SwimBaked.Apply(0.25f, 1f, pose);
        var expected = pose.Get("body").Rotation.X;

        pose.Clear();
        FrogAnimations.SwimBaked.Apply(FrogAnimations.Swim.LengthInSeconds + 0.25f, 1f, pose);
        Assert.Equal(expected, pose.Get("body").Rotation.X, 4);
    }

    [Fact]
    public void IdleWaterPosesMatchTheReference()
    {
        var pose = new EntityPose();
        FrogAnimations.IdleWaterBaked.Apply(0f, 1f, pose);
        Assert.Equal(Rad(-22.5f), pose.Get("left_arm").Rotation.Z, 4);
        Assert.Equal(Rad(22.5f), pose.Get("right_arm").Rotation.Z, 4);
        Assert.Equal(Rad(22.5f), pose.Get("left_leg").Rotation.X, 4);
        Assert.Equal(Rad(-22.5f), pose.Get("left_leg").Rotation.Y, 4);
        Assert.Equal(-1f, pose.Get("left_arm").Position.X, 5);
        Assert.Equal(1f, pose.Get("right_arm").Position.X, 5);
        Assert.Equal(1f, pose.Get("left_leg").Position.Z, 5);

        pose.Clear();
        FrogAnimations.IdleWaterBaked.Apply(1.625f, 1f, pose);
        Assert.Equal(Rad(-10f), pose.Get("body").Rotation.X, 4);

        pose.Clear();
        FrogAnimations.IdleWaterBaked.Apply(1f, 1f, pose);
        Assert.Equal(Rad(-45f), pose.Get("left_leg").Rotation.Z, 4);
        Assert.Equal(Rad(45f), pose.Get("right_leg").Rotation.Z, 4);
        Assert.Equal(1f, pose.Get("left_leg").Position.Y, 5);
    }
}
