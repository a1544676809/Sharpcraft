// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Panda animation coverage ported from <c>PandaModel.setupAnim</c>. Pins the head look + quadruped leg walk, the
/// unhappy head shake / front-leg wave (UnhappyCounter &gt; 0), the sneeze head tilt from SneezeCounter, the integer Sneeze
/// branch, and the part-name contract. The sitting/eating/scared, lie-on-back and roll branches are not derivable from
/// the frame and are intentionally not faked.
/// </summary>
public sealed class PandaAnimationTests
{
    private static readonly int TypeId = PandaModelDefinition.PandaEntityTypeId;

    private const float Deg = MathF.PI / 180f;
    private const float QuarterPi = MathF.PI / 4f;

    private static EntityAnimationFrame Frame(
        float ageInTicks = 0f,
        float pitch = 0f,
        float yaw = 0f,
        float pos = 0f,
        float speed = 0f,
        int unhappyCounter = 0,
        int sneezeCounter = 0,
        float sitAmount = 0f,
        float lieOnBackAmount = 0f,
        float rollAmount = 0f,
        bool isEating = false) =>
        new(
            TypeId,
            ageInTicks,
            pos,
            speed,
            pitch,
            yaw,
            0f,
            0f,
            0f,
            false,
            false,
            false,
            UnhappyCounter: unhappyCounter,
            SneezeCounter: sneezeCounter,
            IsEating: isEating,
            SitAmount: sitAmount,
            LieOnBackAmount: lieOnBackAmount,
            RollAmount: rollAmount);

    private static EntityPose Apply(IEntityAnimationState state, IEntityAnimationProvider provider, in EntityAnimationFrame frame)
    {
        var pose = new EntityPose();
        provider.Apply(state, in frame, pose);
        return pose;
    }

    [Fact]
    public void ProviderExposesClipsAndItsTypeId()
    {
        var provider = new PandaAnimationProvider(TypeId);
        Assert.Equal(97, provider.TypeId);
        Assert.Equal(new[] { "idle", "walk", "unhappy", "sneeze", "sit", "eat", "lie", "roll" }, provider.Clips);
    }

    [Fact]
    public void HeadLooksAlongPitchAndYawWhenIdle()
    {
        var provider = new PandaAnimationProvider(TypeId);
        var pose = Apply(provider.CreateState(), provider, Frame(pitch: 30f, yaw: -45f));

        Assert.Equal(30f * Deg, pose.Get("head").Rotation.X, 4);
        Assert.Equal(-45f * Deg, pose.Get("head").Rotation.Y, 4);
        Assert.Equal(0f, pose.Get("head").Rotation.Z, 6);
        Assert.Equal(0f, pose.Get("right_front_leg").Rotation.X, 6);
    }

    [Fact]
    public void UnhappyShakesTheHeadAndWavesTheFrontLegs()
    {
        var provider = new PandaAnimationProvider(TypeId);
        var pose = Apply(provider.CreateState(), provider, Frame(ageInTicks: 2f, yaw: 30f, unhappyCounter: 5));

        var shake = 0.35f * MathF.Sin(0.6f * 2f);
        Assert.Equal(shake, pose.Get("head").Rotation.Y, 4);
        Assert.Equal(shake, pose.Get("head").Rotation.Z, 4);
        // Unhappy overwrites the yaw with the shake.
        Assert.NotEqual(30f * Deg, pose.Get("head").Rotation.Y);

        Assert.Equal(-0.75f * MathF.Sin(0.3f * 2f), pose.Get("right_front_leg").Rotation.X, 4);
        Assert.Equal(0.75f * MathF.Sin(0.3f * 2f), pose.Get("left_front_leg").Rotation.X, 4);
    }

    [Fact]
    public void SneezeTiltsTheHeadDuringTheFirstPhase()
    {
        var provider = new PandaAnimationProvider(TypeId);
        var pose = Apply(provider.CreateState(), provider, Frame(pitch: 10f, sneezeCounter: 7));

        Assert.Equal((-QuarterPi * 7f) / 14f, pose.Get("head").Rotation.X, 4);
    }

    [Fact]
    public void SneezeMiddlePhaseUsesTheReferenceIntegerDivision()
    {
        var provider = new PandaAnimationProvider(TypeId);
        var pose = Apply(provider.CreateState(), provider, Frame(pitch: 10f, sneezeCounter: 17));

        // (17 - 15) / 5 == 0 in the reference → head.xRot = -π/4.
        Assert.Equal(-QuarterPi, pose.Get("head").Rotation.X, 4);
    }

    [Fact]
    public void SneezeAtTwentyLeavesTheHeadAtThePitch()
    {
        var provider = new PandaAnimationProvider(TypeId);
        var pose = Apply(provider.CreateState(), provider, Frame(pitch: 10f, sneezeCounter: 20));

        Assert.Equal(10f * Deg, pose.Get("head").Rotation.X, 4);
    }

    [Fact]
    public void UnhappyAndSneezeCompose()
    {
        var provider = new PandaAnimationProvider(TypeId);
        var pose = Apply(provider.CreateState(), provider, Frame(ageInTicks: 1f, unhappyCounter: 3, sneezeCounter: 14));

        var shake = 0.35f * MathF.Sin(0.6f);
        Assert.Equal(shake, pose.Get("head").Rotation.Y, 4);
        Assert.Equal(shake, pose.Get("head").Rotation.Z, 4);
        Assert.Equal(-QuarterPi, pose.Get("head").Rotation.X, 4);
    }

    [Fact]
    public void QuadrupedLegWalkSwingIsApplied()
    {
        var provider = new PandaAnimationProvider(TypeId);
        var pose = Apply(provider.CreateState(), provider, Frame(pos: 1f, speed: 1f));

        var rightHind = MathF.Cos(1f * 0.6662f) * 1.4f;
        var leftHind = MathF.Cos((1f * 0.6662f) + MathF.PI) * 1.4f;
        Assert.Equal(rightHind, pose.Get("right_hind_leg").Rotation.X, 4);
        Assert.Equal(leftHind, pose.Get("left_hind_leg").Rotation.X, 4);
        Assert.Equal(leftHind, pose.Get("right_front_leg").Rotation.X, 4);
        Assert.Equal(rightHind, pose.Get("left_front_leg").Rotation.X, 4);
    }

    [Fact]
    public void ApplyClipReconstructsIdleWalkUnhappyAndSneeze()
    {
        var provider = new PandaAnimationProvider(TypeId);
        var state = provider.CreateState();
        var frame = Frame(pitch: 5f);

        var idle = new EntityPose();
        provider.ApplyClip(state, frame, "idle", 0f, idle);
        Assert.Equal(5f * Deg, idle.Get("head").Rotation.X, 4);

        var walk = new EntityPose();
        provider.ApplyClip(state, frame, "walk", 0.1f, walk); // pos 1, speed 1
        Assert.NotEqual(0f, walk.Get("right_hind_leg").Rotation.X);

        var unhappy = new EntityPose();
        provider.ApplyClip(state, frame, "unhappy", 0.1f, unhappy); // age 2
        Assert.Equal(0.35f * MathF.Sin(0.6f * 2f), unhappy.Get("head").Rotation.Z, 4);

        var sneeze = new EntityPose();
        provider.ApplyClip(state, frame, "sneeze", 0.35f, sneeze); // 7 ticks
        Assert.Equal((-QuarterPi * 7f) / 14f, sneeze.Get("head").Rotation.X, 4);

        var unknown = new EntityPose();
        provider.ApplyClip(state, frame, "not_a_clip", 1f, unknown);
        Assert.Empty(unknown.Offsets);
    }

    [Fact]
    public void EveryAnimatedPartExistsInThePandaModel()
    {
        var modelParts = PandaModelDefinition.Definition.Parts
            .Select(part => part.Name)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var part in new[] { "head", "right_hind_leg", "left_hind_leg", "right_front_leg", "left_front_leg" })
        {
            Assert.Contains(part, modelParts);
        }
    }

    [Fact]
    public void SitBranchLerpsBodyAndHeadAndSplaysLegs()
    {
        var provider = new PandaAnimationProvider(TypeId);
        var pose = Apply(provider.CreateState(), provider, Frame(sitAmount: 0.5f));

        // body offset = rotLerpRad(0.5, π/2, 1.7407963) − π/2 (0.5 of a small delta).
        Assert.Equal(0.5f * (1.7407963f - (MathF.PI / 2f)), pose.Get("body").Rotation.X, 4);
        // head = rotLerpRad(0.5, 0, π/2) = π/4.
        Assert.Equal(QuarterPi, pose.Get("head").Rotation.X, 4);
        Assert.Equal(-0.27079642f, pose.Get("right_front_leg").Rotation.Z, 4);
        Assert.Equal(0.27079642f, pose.Get("left_front_leg").Rotation.Z, 4);
        Assert.Equal(0.5707964f, pose.Get("right_hind_leg").Rotation.Z, 4);
        Assert.Equal(-0.5707964f, pose.Get("left_hind_leg").Rotation.Z, 4);
    }

    [Fact]
    public void EatingOverridesTheHeadAndFrontLegsWhileSitting()
    {
        var provider = new PandaAnimationProvider(TypeId);
        var pose = Apply(provider.CreateState(), provider, Frame(ageInTicks: 0f, sitAmount: 1f, isEating: true));

        // age 0 → sin = 0.
        Assert.Equal(MathF.PI / 2f, pose.Get("head").Rotation.X, 4);
        Assert.Equal(-0.4f, pose.Get("right_front_leg").Rotation.X, 4);
        Assert.Equal(-0.4f, pose.Get("left_front_leg").Rotation.X, 4);
        // Sitting zRot splay is still applied.
        Assert.Equal(-0.27079642f, pose.Get("right_front_leg").Rotation.Z, 4);
    }

    [Fact]
    public void LieOnBackSwaysTheLegsAndTiltsTheHead()
    {
        var provider = new PandaAnimationProvider(TypeId);
        var pose = Apply(provider.CreateState(), provider, Frame(ageInTicks: 2f, lieOnBackAmount: 1f));

        Assert.Equal(-0.6f * MathF.Sin(2f * 0.15f), pose.Get("right_hind_leg").Rotation.X, 4);
        Assert.Equal(0.6f * MathF.Sin(2f * 0.15f), pose.Get("left_hind_leg").Rotation.X, 4);
        Assert.Equal(0.3f * MathF.Sin(2f * 0.25f), pose.Get("right_front_leg").Rotation.X, 4);
        Assert.Equal(-0.3f * MathF.Sin(2f * 0.25f), pose.Get("left_front_leg").Rotation.X, 4);
        Assert.Equal(MathF.PI / 2f, pose.Get("head").Rotation.X, 4);
    }

    [Fact]
    public void RollBranchTiltsTheHeadAndSwingsTheLegs()
    {
        var provider = new PandaAnimationProvider(TypeId);
        var pose = Apply(provider.CreateState(), provider, Frame(ageInTicks: 2f, rollAmount: 1f));

        // head = rotLerpRad(1, 0, 2.0561945); the delta is within (−π, π) so no wrap.
        Assert.Equal(2.0561945f, pose.Get("head").Rotation.X, 4);
        Assert.Equal(-0.5f * MathF.Sin(2f * 0.5f), pose.Get("right_hind_leg").Rotation.X, 4);
        Assert.Equal(0.5f * MathF.Sin(2f * 0.5f), pose.Get("left_hind_leg").Rotation.X, 4);
        Assert.Equal(0.5f * MathF.Sin(2f * 0.5f), pose.Get("right_front_leg").Rotation.X, 4);
        Assert.Equal(-0.5f * MathF.Sin(2f * 0.5f), pose.Get("left_front_leg").Rotation.X, 4);
    }

    [Fact]
    public void ApplyClipReconstructsSitEatLieAndRoll()
    {
        var provider = new PandaAnimationProvider(TypeId);
        var state = provider.CreateState();
        var frame = Frame();

        var sit = new EntityPose();
        provider.ApplyClip(state, frame, "sit", 0f, sit);
        Assert.Equal(1.7407963f - (MathF.PI / 2f), sit.Get("body").Rotation.X, 4);

        var eat = new EntityPose();
        provider.ApplyClip(state, frame, "eat", 0f, eat);
        Assert.Equal(MathF.PI / 2f, eat.Get("head").Rotation.X, 4);

        var lie = new EntityPose();
        provider.ApplyClip(state, frame, "lie", 0f, lie);
        Assert.Equal(MathF.PI / 2f, lie.Get("head").Rotation.X, 4);

        var roll = new EntityPose();
        provider.ApplyClip(state, frame, "roll", 0f, roll);
        Assert.Equal(2.0561945f, roll.Get("head").Rotation.X, 4);
    }
}
