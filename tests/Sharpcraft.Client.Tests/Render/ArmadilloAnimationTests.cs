// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Render;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Adult armadillo animation clips ported from the reference ArmadilloAnimation data
/// (docs/analysis/render/entity-animation.md). The clips are keyframe data, so these tests pin the reference
/// lengths/looping and a few representative frames plus the part-name contract with the model definition.
/// </summary>
public sealed class ArmadilloAnimationTests
{
    private const float ToRad = MathF.PI / 180f;

    private static EntityPose Apply(KeyframeAnimation animation, float seconds)
    {
        var pose = new EntityPose();
        animation.Apply(seconds, 1f, pose);
        return pose;
    }

    [Fact]
    public void ClipsDeclareReferenceLengthsAndLooping()
    {
        Assert.Equal(0.5f, ArmadilloAnimations.RollUp.LengthInSeconds);
        Assert.False(ArmadilloAnimations.RollUp.Looping);

        Assert.Equal(1.5f, ArmadilloAnimations.RollOut.LengthInSeconds);
        Assert.False(ArmadilloAnimations.RollOut.Looping);

        Assert.Equal(1.4583f, ArmadilloAnimations.Walk.LengthInSeconds);
        Assert.True(ArmadilloAnimations.Walk.Looping);

        Assert.Equal(2.5f, ArmadilloAnimations.Peek.LengthInSeconds);
        Assert.False(ArmadilloAnimations.Peek.Looping);
    }

    [Fact]
    public void RollUpStartsFromTheUnrolledPoseAndReachesTheRolledHeadAndLegPose()
    {
        var start = Apply(ArmadilloAnimations.RollUpBaked, 0f);
        Assert.Equal(Vector3.Zero, start.Get("body").Position);
        Assert.Equal(Vector3.Zero, start.Get("body").Rotation);
        Assert.Equal(0f, start.Get("head").Rotation.X, 4);
        // The shell cube begins three units up (Y is inverted by PosVec) and only replaces the body while hiding.
        Assert.Equal(new Vector3(0f, -3f, 0f), start.Get("cube").Position);

        var rolled = Apply(ArmadilloAnimations.RollUpBaked, 0.25f);
        Assert.Equal(new Vector3(0f, -6f, -1f), rolled.Get("body").Position);
        Assert.Equal(-72.5f * ToRad, rolled.Get("head").Rotation.X, 4);
        Assert.Equal(new Vector3(0f, -8f, -2f), rolled.Get("right_hind_leg").Position);
        Assert.Equal(-85f * ToRad, rolled.Get("left_front_leg").Rotation.X, 4);
    }

    [Fact]
    public void WalkFirstAndHalfCycleMatchTheReferenceKeyframes()
    {
        var start = Apply(ArmadilloAnimations.WalkBaked, 0f);
        Assert.Equal(0f, start.Get("body").Rotation.X, 4);
        Assert.Equal(0f, start.Get("body").Rotation.Z, 4);
        Assert.Equal(-50f * ToRad, start.Get("right_hind_leg").Rotation.X, 4);
        Assert.Equal(0f, start.Get("tail").Rotation.X, 4);

        var half = Apply(ArmadilloAnimations.WalkBaked, 0.5f);
        Assert.Equal(0f, half.Get("right_hind_leg").Rotation.X, 4);
        Assert.Equal(-9.17f * ToRad, half.Get("tail").Rotation.X, 4);
        Assert.Equal(0f, half.Get("body").Rotation.Z, 4);
    }

    [Fact]
    public void WalkLoopsInsteadOfClampingPastTheEnd()
    {
        // A non-looping clip would clamp to the last keyframe (tail 0). Looping wraps to the 0.5s phase (tail -9.17).
        var wrapped = Apply(ArmadilloAnimations.WalkBaked, ArmadilloAnimations.Walk.LengthInSeconds + 0.5f);
        Assert.Equal(-9.17f * ToRad, wrapped.Get("tail").Rotation.X, 3);
        Assert.Equal(0f, wrapped.Get("right_hind_leg").Rotation.X, 3);
    }

    [Fact]
    public void PeekRaisesTheHeadAndFlipsItSidewaysOnTheReferenceFrames()
    {
        var start = Apply(ArmadilloAnimations.PeekBaked, 0f);
        Assert.Equal(-70f * ToRad, start.Get("head").Rotation.X, 4);
        Assert.Equal(new Vector3(0f, -1f, 7f), start.Get("head").Position);
        Assert.Equal(-90f * ToRad, start.Get("right_front_leg").Rotation.X, 4);
        Assert.Equal(new Vector3(-1f, -2f, 2f), start.Get("right_front_leg").Position);

        var lookAside = Apply(ArmadilloAnimations.PeekBaked, 0.9f);
        Assert.Equal(-7.5f * ToRad, lookAside.Get("head").Rotation.X, 4);
        Assert.Equal(45f * ToRad, lookAside.Get("head").Rotation.Z, 4);
    }

    [Fact]
    public void RollOutStartsShelledAndReturnsTheLegsAndBodyToNeutral()
    {
        var start = Apply(ArmadilloAnimations.RollOutBaked, 0f);
        Assert.Equal(-50f * ToRad, start.Get("head").Rotation.X, 4);
        Assert.Equal(new Vector3(0f, -1f, 5f), start.Get("head").Position);
        Assert.Equal(-90f * ToRad, start.Get("right_front_leg").Rotation.X, 4);
        Assert.Equal(new Vector3(-1f, -2f, 2f), start.Get("right_front_leg").Position);
        Assert.Equal(Vector3.Zero, start.Get("cube").Position);
        Assert.Equal(Vector3.Zero, start.Get("body").Position);

        var planting = Apply(ArmadilloAnimations.RollOutBaked, 1.3f);
        Assert.Equal(new Vector3(0f, -4f, 0f), planting.Get("body").Position);
        Assert.Equal(30f * ToRad, planting.Get("right_hind_leg").Rotation.Z, 4);
        Assert.Equal(30f * ToRad, planting.Get("right_front_leg").Rotation.Z, 4);

        var settled = Apply(ArmadilloAnimations.RollOutBaked, 1.5f);
        Assert.Equal(Vector3.Zero, settled.Get("body").Position);
        Assert.Equal(new Vector3(0f, -1f, 0f), settled.Get("cube").Position);
    }

    [Fact]
    public void EveryAnimatedPartExistsInTheArmadilloModel()
    {
        var modelParts = ArmadilloModelDefinition.Definition.Parts.Select(part => part.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var definition in new[]
                 {
                     ArmadilloAnimations.RollUp,
                     ArmadilloAnimations.RollOut,
                     ArmadilloAnimations.Walk,
                     ArmadilloAnimations.Peek
                 })
        {
            foreach (var partName in definition.BoneAnimations.Keys)
            {
                Assert.Contains(partName, modelParts);
            }
        }
    }
}
