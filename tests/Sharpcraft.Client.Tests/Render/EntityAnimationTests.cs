// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Render;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>Animation evaluator core (docs/analysis/render/entity-animation.md).</summary>
public sealed class EntityAnimationTests
{
    private static AnimationChannel Rotation(params AnimationKeyframe[] frames) =>
        new(AnimationTarget.Rotation, frames);

    [Fact]
    public void PositionAndScaleVectorHelpersFollowTheReferenceConventions()
    {
        Assert.Equal(new Vector3(1f, -2f, 3f), KeyframeAnimations.PosVec(1f, 2f, 3f));
        Assert.Equal(new Vector3(0f, 0f, 0f), KeyframeAnimations.ScaleVec(1f, 1f, 1f));
        Assert.Equal(new Vector3(1f, -0.5f, 2f), KeyframeAnimations.ScaleVec(2f, 0.5f, 3f));
    }

    [Fact]
    public void LinearKeysInterpolateAndTargetScaleMultipliesTheOffset()
    {
        var animation = new AnimationDefinition(1f, false, new Dictionary<string, IReadOnlyList<AnimationChannel>>
        {
            ["leg"] = [Rotation(
                new(0f, Vector3.Zero, AnimationInterpolation.Linear),
                new(1f, new Vector3(0f, MathF.PI, 0f), AnimationInterpolation.Linear))]
        }).Bake(new HashSet<string> { "leg" });

        var pose = new EntityPose();
        animation.Apply(0.5f, 0.5f, pose);
        Assert.Equal(MathF.PI * 0.25f, pose.Get("leg").Rotation.Y, 4);

        pose.Clear();
        animation.Apply(0.5f, 1f, pose);
        Assert.Equal(MathF.PI * 0.5f, pose.Get("leg").Rotation.Y, 4);
    }

    [Fact]
    public void LoopingWrapsTheElapsedSecondsAndStaticUsesTimeZero()
    {
        var animation = new AnimationDefinition(2f, true, new Dictionary<string, IReadOnlyList<AnimationChannel>>
        {
            ["head"] = [Rotation(
                new(0f, Vector3.Zero, AnimationInterpolation.Linear),
                new(2f, new Vector3(0f, 1f, 0f), AnimationInterpolation.Linear))]
        }).Bake(new HashSet<string> { "head" });

        var pose = new EntityPose();
        animation.Apply(2.5f, 1f, pose);
        Assert.Equal(0.25f, pose.Get("head").Rotation.Y, 4);

        pose.Clear();
        animation.ApplyStatic(pose);
        Assert.Equal(0f, pose.Get("head").Rotation.Y, 4);
    }

    [Fact]
    public void MultipleChannelsAccumulateOnTheSamePart()
    {
        var animation = new AnimationDefinition(1f, false, new Dictionary<string, IReadOnlyList<AnimationChannel>>
        {
            ["body"] =
            [
                new(AnimationTarget.Position, [new(0f, KeyframeAnimations.PosVec(0f, 2f, 0f), AnimationInterpolation.Linear)]),
                new(AnimationTarget.Rotation, [new(0f, new Vector3(0f, 0.5f, 0f), AnimationInterpolation.Linear)])
            ]
        }).Bake(new HashSet<string> { "body" });

        var pose = new EntityPose();
        animation.Apply(0f, 1f, pose);

        Assert.Equal(new Vector3(0f, -2f, 0f), pose.Get("body").Position);
        Assert.Equal(0.5f, pose.Get("body").Rotation.Y, 4);
    }

    [Fact]
    public void AnimationStateUsesFiftyMillisecondsPerTick()
    {
        var state = new AnimationState();
        state.Start(10);
        Assert.True(state.IsStarted);
        Assert.Equal(500, state.GetTimeInMillis(20f));
        state.Stop();
        Assert.False(state.IsStarted);
    }

    [Fact]
    public void PigWalkControllerMatchesTheReferenceLegPhases()
    {
        var state = new Sharpcraft.Client.World.EntityRenderState(1, PigModelDefinition.PigEntityTypeId, 0f, 0f, 0f, 0f, 0f)
        {
            WalkAnimationPos = 0f,
            WalkAnimationSpeed = 1f
        };
        var pose = new EntityPose();
        EntityAnimationController.Apply(state, pose);

        Assert.Equal(1.4f, pose.Get("right_hind_leg").Rotation.X, 4);
        Assert.Equal(-1.4f, pose.Get("left_hind_leg").Rotation.X, 4);
        Assert.Equal(-1.4f, pose.Get("right_front_leg").Rotation.X, 4);
        Assert.Equal(1.4f, pose.Get("left_front_leg").Rotation.X, 4);
    }
    [Fact]
    public void CamelWalkUsesThePortedKeyframeData()
    {
        var state = new Sharpcraft.Client.World.EntityRenderState(1, CamelModelDefinition.CamelEntityTypeId, 0f, 0f, 0f, 0f, 0f)
        {
            WalkAnimationPos = 0f,
            WalkAnimationSpeed = 1f
        };
        var pose = new EntityPose();
        EntityAnimationController.Apply(state, pose);

        Assert.Equal(2.5f * (MathF.PI / 180f), pose.Get("root").Rotation.Z, 4);
        Assert.Equal(22.5f * (MathF.PI / 180f), pose.Get("right_front_leg").Rotation.X, 4);
    }
    [Fact]
    public void BakingRejectsMissingParts()
    {
        var animation = new AnimationDefinition(1f, false, new Dictionary<string, IReadOnlyList<AnimationChannel>>
        {
            ["missing"] = [Rotation(new AnimationKeyframe(0f, Vector3.Zero, AnimationInterpolation.Linear))]
        });

        Assert.Throws<InvalidOperationException>(() => animation.Bake(new HashSet<string> { "head" }));
    }
}




