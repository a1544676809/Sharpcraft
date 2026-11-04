// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
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
        ApplyAnimation(state, pose);

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
        ApplyAnimation(state, pose);

        Assert.Equal(2.5f * (MathF.PI / 180f), pose.Get("root").Rotation.Z, 4);
        Assert.Equal(22.5f * (MathF.PI / 180f), pose.Get("right_front_leg").Rotation.X, 4);
    }
    [Fact]
    public void SkeletonAggressiveMeleeOverridesTheArmsOnlyWhileNotHoldingABow()
    {
        var neutral = new Sharpcraft.Client.World.EntityRenderState(1, HumanoidMobModelDefinition.SkeletonEntityTypeId, 0f, 0f, 0f, 0f, 0f)
        {
            WalkAnimationPos = 0f,
            WalkAnimationSpeed = 1f,
            IsAggressive = false
        };
        var neutralPose = new EntityPose();
        ApplyAnimation(neutral, neutralPose);
        Assert.Equal(-1f, neutralPose.Get("right_arm").Rotation.X, 4);
        Assert.Equal(1f, neutralPose.Get("left_arm").Rotation.X, 4);

        // Aggressive and not holding a bow: the arms are raised to the melee stance plus AnimationUtils.bobArms.
        var aggressive = neutral with { IsAggressive = true, SwingAnimation = 0f, AgeInTicks = 0f };
        var aggressivePose = new EntityPose();
        ApplyAnimation(aggressive, aggressivePose);
        Assert.Equal(-MathF.PI / 2f, aggressivePose.Get("right_arm").Rotation.X, 4);
        Assert.Equal(-0.1f, aggressivePose.Get("right_arm").Rotation.Y, 4);
        Assert.Equal(0.1f, aggressivePose.Get("right_arm").Rotation.Z, 4);
        Assert.Equal(-MathF.PI / 2f, aggressivePose.Get("left_arm").Rotation.X, 4);
        Assert.Equal(0.1f, aggressivePose.Get("left_arm").Rotation.Y, 4);
        Assert.Equal(-0.1f, aggressivePose.Get("left_arm").Rotation.Z, 4);
        // The legs keep the humanoid walk phases.
        Assert.Equal(1.4f, aggressivePose.Get("right_leg").Rotation.X, 4);
        Assert.Equal(-1.4f, aggressivePose.Get("left_leg").Rotation.X, 4);

        // Holding a bow suppresses the melee branch, so the arms fall back to the walk swing.
        if (SkeletonAnimationProvider.BowItemId > 0)
        {
            var bowPose = new EntityPose();
            ApplyAnimation(aggressive with { MainHandItemId = SkeletonAnimationProvider.BowItemId }, bowPose);
            Assert.Equal(-1f, bowPose.Get("right_arm").Rotation.X, 4);
            Assert.Equal(1f, bowPose.Get("left_arm").Rotation.X, 4);
        }
    }

    [Fact]
    public void HumanoidAndVillagerWalkPosesFollowTheReferencePhases()
    {
        var zombie = new Sharpcraft.Client.World.EntityRenderState(1, 154, 0f, 0f, 0f, 0f, 0f)
        {
            WalkAnimationPos = 0f,
            WalkAnimationSpeed = 1f
        };
        var zombiePose = new EntityPose();
        ApplyAnimation(zombie, zombiePose);
        Assert.Equal(-1f, zombiePose.Get("right_arm").Rotation.X, 4);
        Assert.Equal(1f, zombiePose.Get("left_arm").Rotation.X, 4);
        Assert.Equal(1.4f, zombiePose.Get("right_leg").Rotation.X, 4);
        Assert.Equal(-1.4f, zombiePose.Get("left_leg").Rotation.X, 4);

        var villager = new Sharpcraft.Client.World.EntityRenderState(1, 143, 0f, 0f, 0f, 0f, 0f)
        {
            WalkAnimationPos = 0f,
            WalkAnimationSpeed = 1f
        };
        var villagerPose = new EntityPose();
        ApplyAnimation(villager, villagerPose);
        Assert.Equal(0.7f, villagerPose.Get("right_leg").Rotation.X, 4);
        Assert.Equal(-0.7f, villagerPose.Get("left_leg").Rotation.X, 4);
    }
    [Fact]
    public void CamelIdleUsesThePortedIdleKeyframes()
    {
        var state = new Sharpcraft.Client.World.EntityRenderState(1, CamelModelDefinition.CamelEntityTypeId, 0f, 0f, 0f, 0f, 0f)
        {
            WalkAnimationSpeed = 0f,
            AgeInTicks = 0f
        };
        var pose = new EntityPose();
        ApplyAnimation(state, pose);

        Assert.Equal(5f * (MathF.PI / 180f), pose.Get("tail").Rotation.X, 4);
    }
    [Fact]
    public void CowSheepGoatReuseTheQuadrupedWalkController()
    {
        foreach (var typeId in new[] { 30, 87, 114, 63 })
        {
            var state = new Sharpcraft.Client.World.EntityRenderState(1, typeId, 0f, 0f, 0f, 0f, 0f)
            {
                WalkAnimationPos = 0f,
                WalkAnimationSpeed = 1f
            };
            var pose = new EntityPose();
            ApplyAnimation(state, pose);
            Assert.Equal(1.4f, pose.Get("right_hind_leg").Rotation.X, 4);
            Assert.Equal(1.4f, pose.Get("left_front_leg").Rotation.X, 4);
            Assert.Contains("walk", EntityAnimationRegistry.Clips(typeId));
        }
    }
    [Fact]
    public void HorseWalkUsesTheReferenceEquinePhases()
    {
        var state = new Sharpcraft.Client.World.EntityRenderState(1, HorseModelDefinition.HorseEntityTypeId, 0f, 0f, 0f, 0f, 0f)
        {
            WalkAnimationPos = 0f,
            WalkAnimationSpeed = 1f
        };
        var pose = new EntityPose();
        ApplyAnimation(state, pose);

        Assert.Equal(0.8f, pose.Get("right_front_leg").Rotation.X, 4);
        Assert.Equal(-0.8f, pose.Get("left_front_leg").Rotation.X, 4);
        Assert.Equal(0.5f, pose.Get("left_hind_leg").Rotation.X, 4);
        Assert.Equal(-0.5f, pose.Get("right_hind_leg").Rotation.X, 4);
        Assert.Contains("walk", EntityAnimationRegistry.Clips(HorseModelDefinition.HorseEntityTypeId));
    }
    [Fact]
    public void ChickenWingsAndSquidTentaclesFollowTheirState()
    {
        var chicken = new Sharpcraft.Client.World.EntityRenderState(1, 26, 0f, 0f, 0f, 0f, 0f)
        {
            WalkAnimationPos = 0f,
            WalkAnimationSpeed = 1f,
            Flap = 0f,
            FlapSpeed = 1f
        };
        var chickenPose = new EntityPose();
        ApplyAnimation(chicken, chickenPose);
        Assert.Equal(1f, chickenPose.Get("right_wing").Rotation.Z, 4);
        Assert.Equal(-1f, chickenPose.Get("left_wing").Rotation.Z, 4);
        Assert.Equal(1.4f, chickenPose.Get("right_leg").Rotation.X, 4);

        var squid = new Sharpcraft.Client.World.EntityRenderState(1, 130, 0f, 0f, 0f, 0f, 0f)
        {
            TentacleAngle = 0.5f
        };
        var squidPose = new EntityPose();
        ApplyAnimation(squid, squidPose);
        Assert.Equal(0.5f, squidPose.Get("tentacle0").Rotation.X, 4);
        Assert.Equal(0.5f, squidPose.Get("tentacle7").Rotation.X, 4);
    }
    [Fact]
    public void BatFlyingAndRestingUseThePortedKeyframes()
    {
        var pose = new EntityPose();
        BatAnimations.FlyingBaked.Apply(0f, 1f, pose);
        Assert.Equal(40f * (MathF.PI / 180f), pose.Get("body").Rotation.X, 4);

        pose.Clear();
        BatAnimations.RestingBaked.Apply(0f, 1f, pose);
        Assert.Equal(MathF.PI, pose.Get("body").Rotation.X, 4);
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

    private static void ApplyAnimation(EntityRenderState state, EntityPose pose)
    {
        var provider = EntityAnimationRegistry.For(state.TypeId);
        Assert.NotNull(provider);
        provider!.Apply(provider.CreateState(), EntityAnimationFrame.From(state), pose);
    }
}











