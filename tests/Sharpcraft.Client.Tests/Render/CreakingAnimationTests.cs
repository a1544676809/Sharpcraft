// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Creaking animation clips ported from the reference CreakingAnimation data
/// (docs/analysis/render/entity-animation.md). The clips are keyframe data, so these tests pin the reference
/// lengths/looping and representative keyframes, plus the provider's head-look and walk mapping.
/// </summary>
public sealed class CreakingAnimationTests
{
    private const float ToRad = MathF.PI / 180f;

    private static EntityPose Apply(KeyframeAnimation animation, float seconds)
    {
        var pose = new EntityPose();
        animation.Apply(seconds, 1f, pose);
        return pose;
    }

    private static EntityAnimationFrame Frame(
        float pitch = 0f,
        float yaw = 0f,
        float walkPos = 0f,
        float walkSpeed = 0f) => new(
        TypeId: CreakingModelDefinition.CreakingEntityTypeId,
        AgeInTicks: 0f,
        WalkAnimationPos: walkPos,
        WalkAnimationSpeed: walkSpeed,
        Pitch: pitch,
        Yaw: yaw,
        Flap: 0f,
        FlapSpeed: 0f,
        TentacleAngle: 0f,
        IsResting: false,
        IsBaby: false,
        IsHidingInShell: false);

    [Fact]
    public void ClipsDeclareReferenceLengthsAndLooping()
    {
        Assert.Equal(1.125f, CreakingAnimations.Walk.LengthInSeconds);
        Assert.True(CreakingAnimations.Walk.Looping);

        Assert.Equal(0.7083f, CreakingAnimations.Attack.LengthInSeconds);
        Assert.True(CreakingAnimations.Attack.Looping);

        Assert.Equal(0.2917f, CreakingAnimations.Invulnerable.LengthInSeconds);
        Assert.False(CreakingAnimations.Invulnerable.Looping);

        Assert.Equal(2.25f, CreakingAnimations.Death.LengthInSeconds);
        Assert.False(CreakingAnimations.Death.Looping);
    }

    [Fact]
    public void WalkFirstAndMidCycleMatchTheReferenceKeyframes()
    {
        var start = Apply(CreakingAnimations.WalkBaked, 0f);
        Assert.Equal(26.8802f * ToRad, start.Get("upper_body").Rotation.X, 4);
        Assert.Equal(-23.399f * ToRad, start.Get("upper_body").Rotation.Y, 4);
        Assert.Equal(12.5f * ToRad, start.Get("right_arm").Rotation.X, 4);
        Assert.Equal(-15f * ToRad, start.Get("left_arm").Rotation.X, 4);
        Assert.Equal(new Vector3(0f, 0f, 2f), start.Get("left_leg").Position);
        Assert.Equal(new Vector3(0f, -0.9674f, -3.6578f), start.Get("right_leg").Position);

        var quarter = Apply(CreakingAnimations.WalkBaked, 0.25f);
        Assert.Equal(-32f * ToRad, quarter.Get("right_arm").Rotation.X, 4);
        Assert.Equal(30f * ToRad, quarter.Get("left_leg").Rotation.X, 4);
        Assert.Equal(new Vector3(0f, -0.1846f, 0.5979f), quarter.Get("left_leg").Position);
        // right_leg passes through its -49.5628 deg swing at 0.125 s and returns to neutral at 0.25 s.
        Assert.Equal(-49.5628f * ToRad, Apply(CreakingAnimations.WalkBaked, 0.125f).Get("right_leg").Rotation.X, 4);
        Assert.Equal(0f, quarter.Get("right_leg").Rotation.X, 4);
    }

    [Fact]
    public void WalkLoopsInsteadOfClampingPastTheEnd()
    {
        // A non-looping clip would clamp to the 1.125 s keyframe (right_arm -15 deg). Looping wraps to 0.25 s (-32).
        var wrapped = Apply(CreakingAnimations.WalkBaked, CreakingAnimations.Walk.LengthInSeconds + 0.25f);
        Assert.Equal(-32f * ToRad, wrapped.Get("right_arm").Rotation.X, 3);
    }

    [Fact]
    public void AttackFirstAndMidCycleMatchTheReferenceKeyframes()
    {
        var start = Apply(CreakingAnimations.AttackBaked, 0f);
        Assert.Equal(Vector3.Zero, start.Get("upper_body").Rotation);
        Assert.Equal(Vector3.Zero, start.Get("head").Rotation);
        Assert.Equal(0f, start.Get("upper_body").Scale.X, 4);
        Assert.Equal(0f, start.Get("upper_body").Scale.Y, 4);

        var windup = Apply(CreakingAnimations.AttackBaked, 0.1667f);
        Assert.Equal(-115f * ToRad, windup.Get("upper_body").Rotation.X, 4);
        Assert.Equal(67.5f * ToRad, windup.Get("upper_body").Rotation.Y, 4);
        Assert.Equal(-90f * ToRad, windup.Get("upper_body").Rotation.Z, 4);
        Assert.Equal(-45f * ToRad, windup.Get("head").Rotation.Y, 4);
        Assert.Equal(45f * ToRad, windup.Get("right_leg").Rotation.Y, 4);
        Assert.Equal(new Vector3(0.7071f, 0f, 0f), windup.Get("right_leg").Position);
        Assert.Equal(new Vector3(0f, 0f, -2f), windup.Get("left_leg").Position);

        var stretch = Apply(CreakingAnimations.AttackBaked, 0.5f);
        Assert.Equal(0.3f, stretch.Get("head").Scale.Y, 4);
        Assert.Equal(new Vector3(0.3827f, -0.5133f, -0.7682f), stretch.Get("head").Position);
    }

    [Fact]
    public void InvulnerableRecoilMatchesTheReferenceKeyframes()
    {
        var recoil = Apply(CreakingAnimations.InvulnerableBaked, 0.0833f);
        Assert.Equal(-5f * ToRad, recoil.Get("upper_body").Rotation.X, 4);
        Assert.Equal(17.5f * ToRad, recoil.Get("right_arm").Rotation.X, 4);
        Assert.Equal(20f * ToRad, recoil.Get("left_arm").Rotation.X, 4);

        var swing = Apply(CreakingAnimations.InvulnerableBaked, 0.1667f);
        Assert.Equal(5f * ToRad, swing.Get("upper_body").Rotation.X, 4);
        Assert.Equal(-15f * ToRad, swing.Get("right_arm").Rotation.X, 4);
        Assert.Equal(-15f * ToRad, swing.Get("left_arm").Rotation.X, 4);
    }

    [Fact]
    public void DeathFirstPoseAndCompressionMatchTheReferenceKeyframes()
    {
        var start = Apply(CreakingAnimations.DeathBaked, 0f);
        Assert.Equal(Vector3.Zero, start.Get("upper_body").Rotation);
        Assert.Equal(0f, start.Get("upper_body").Scale.Y, 4);

        var jolt = Apply(CreakingAnimations.DeathBaked, 0.0833f);
        Assert.Equal(-40f * ToRad, jolt.Get("upper_body").Rotation.X, 4);
        Assert.Equal(new Vector3(0f, -0.557f, 1.2659f), jolt.Get("upper_body").Position);
        Assert.Equal(0.1f, jolt.Get("upper_body").Scale.Y, 4);
        Assert.Equal(-5f * ToRad, jolt.Get("head").Rotation.X, 4);

        var compressed = Apply(CreakingAnimations.DeathBaked, 0.1667f);
        Assert.Equal(-0.1f, compressed.Get("upper_body").Scale.Y, 4);
        Assert.Equal(-47.64f * ToRad, Apply(CreakingAnimations.DeathBaked, 1.0417f).Get("upper_body").Rotation.X, 4);
    }

    [Fact]
    public void ProviderLooksAlongPitchAndYawInRadians()
    {
        var provider = new CreakingAnimationProvider();
        var pose = new EntityPose();
        var frame = Frame(pitch: 30f, yaw: -45f);

        provider.Apply(new State(), in frame, pose);

        Assert.Equal(30f * ToRad, pose.Get("head").Rotation.X, 4);
        Assert.Equal(-45f * ToRad, pose.Get("head").Rotation.Y, 4);
        Assert.Equal(0f, pose.Get("head").Rotation.Z, 4);
    }

    [Fact]
    public void ProviderWalksOnlyWhenWalkSpeedIsPositive()
    {
        var provider = new CreakingAnimationProvider();
        var state = new State();

        var stillFrame = Frame(walkPos: 5f, walkSpeed: 0f);
        var still = new EntityPose();
        provider.Apply(state, in stillFrame, still);
        Assert.Equal(Vector3.Zero, still.Get("right_arm").Rotation);
        Assert.Equal(Vector3.Zero, still.Get("left_leg").Rotation);

        var movingFrame = Frame(walkPos: 5f, walkSpeed: 1f);
        var moving = new EntityPose();
        provider.Apply(state, in movingFrame, moving);
        // walkPos 5 -> 0.25 s into the clip at speed 1, matching the reference applyWalk mapping.
        Assert.Equal(-32f * ToRad, moving.Get("right_arm").Rotation.X, 4);
        Assert.Equal(30f * ToRad, moving.Get("left_leg").Rotation.X, 4);
    }

    [Fact]
    public void ApplyClipPlaysEveryAdvertisedClipAtExplicitSeconds()
    {
        var provider = new CreakingAnimationProvider();
        var state = new State();
        var frame = Frame();

        Assert.Equal(new[] { "walk", "attack", "invulnerable", "death" }, provider.Clips);

        var walk = new EntityPose();
        provider.ApplyClip(state, in frame, "walk", 0.25f, walk);
        Assert.Equal(-32f * ToRad, walk.Get("right_arm").Rotation.X, 4);

        var attack = new EntityPose();
        provider.ApplyClip(state, in frame, "attack", 0.1667f, attack);
        Assert.Equal(-115f * ToRad, attack.Get("upper_body").Rotation.X, 4);

        var invulnerable = new EntityPose();
        provider.ApplyClip(state, in frame, "invulnerable", 0.0833f, invulnerable);
        Assert.Equal(17.5f * ToRad, invulnerable.Get("right_arm").Rotation.X, 4);

        var death = new EntityPose();
        provider.ApplyClip(state, in frame, "death", 0.0833f, death);
        Assert.Equal(-40f * ToRad, death.Get("upper_body").Rotation.X, 4);

        var unknown = new EntityPose();
        provider.ApplyClip(state, in frame, "missing", 0.5f, unknown);
        Assert.Empty(unknown.Offsets);
    }

    [Fact]
    public void EveryAnimatedPartExistsInTheCreakingModel()
    {
        var modelParts = CreakingModelDefinition.Definition.Parts.Select(part => part.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var definition in new[]
                 {
                     CreakingAnimations.Walk,
                     CreakingAnimations.Attack,
                     CreakingAnimations.Invulnerable,
                     CreakingAnimations.Death
                 })
        {
            foreach (var partName in definition.BoneAnimations.Keys)
            {
                Assert.Contains(partName, modelParts);
            }
        }
    }

    private sealed class State : IEntityAnimationState
    {
    }
}
