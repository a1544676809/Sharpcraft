// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Wolf procedural animation ported from the reference AdultWolfModel.setupAnim. These tests pin the derivable parts:
/// the tail yaw/pitch, the four-leg walk swing, the sitting pose, head look — plus the deliberate omissions for the
/// blocked local render-state fields (tame tail angle, shake body roll, head roll).
/// </summary>
public sealed class WolfAnimationTests
{
    private const float ToRad = MathF.PI / 180f;
    private const float RefTailBindXRot = MathF.PI / 5f;

    private static EntityAnimationFrame Frame(
        float pitch = 0f,
        float yaw = 0f,
        float walkPos = 0f,
        float walkSpeed = 0f,
        bool isAngry = false,
        bool isSitting = false,
        bool isTame = false,
        bool isBaby = false,
        float health = 0f,
        float maxHealth = 0f) => new(
        TypeId: WolfModelDefinition.WolfEntityTypeId,
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
        IsTame: isTame,
        IsSitting: isSitting,
        IsAngry: isAngry,
        Health: health,
        MaxHealth: maxHealth);

    [Fact]
    public void ProviderExposesReferenceTypeIdAndClips()
    {
        var provider = new WolfAnimationProvider(WolfModelDefinition.WolfEntityTypeId);
        Assert.Equal(152, provider.TypeId);
        Assert.Equal(new[] { "idle", "walk", "sit" }, provider.Clips);
    }

    [Fact]
    public void TailWagsWithTheWalkPhaseUnlessAngry()
    {
        var provider = new WolfAnimationProvider(WolfModelDefinition.WolfEntityTypeId);
        var phase = 1f * 0.6662f;
        var expected = MathF.Cos(phase) * 1.4f * 1f;

        var calm = new EntityPose();
        provider.Apply(provider.CreateState(), Frame(walkPos: 1f, walkSpeed: 1f), calm);
        Assert.Equal(expected, calm.Get("tail").Rotation.Y, 4);

        var angry = new EntityPose();
        provider.Apply(provider.CreateState(), Frame(walkPos: 1f, walkSpeed: 1f, isAngry: true), angry);
        Assert.Equal(0f, angry.Get("tail").Rotation.Y, 4);
    }

    [Fact]
    public void FourLegsFollowTheWalkCosinePhases()
    {
        var provider = new WolfAnimationProvider(WolfModelDefinition.WolfEntityTypeId);
        var pose = new EntityPose();
        provider.Apply(provider.CreateState(), Frame(walkPos: 1f, walkSpeed: 1f), pose);

        var phase = 1f * 0.6662f;
        var amplitude = 1.4f * 1f;
        var hind = MathF.Cos(phase) * amplitude;
        var hindOpposite = MathF.Cos(phase + MathF.PI) * amplitude;

        Assert.Equal(hind, pose.Get("right_hind_leg").Rotation.X, 4);
        Assert.Equal(hindOpposite, pose.Get("left_hind_leg").Rotation.X, 4);
        Assert.Equal(hindOpposite, pose.Get("right_front_leg").Rotation.X, 4);
        Assert.Equal(hind, pose.Get("left_front_leg").Rotation.X, 4);
    }

    [Fact]
    public void HeadLooksAlongPitchAndYawInRadians()
    {
        var provider = new WolfAnimationProvider(WolfModelDefinition.WolfEntityTypeId);
        var pose = new EntityPose();
        provider.Apply(provider.CreateState(), Frame(pitch: 30f, yaw: -45f), pose);

        Assert.Equal(30f * ToRad, pose.Get("head").Rotation.X, 4);
        Assert.Equal(-45f * ToRad, pose.Get("head").Rotation.Y, 4);
        Assert.Equal(0f, pose.Get("head").Rotation.Z, 4);
    }

    [Fact]
    public void SittingPoseMatchesTheReference()
    {
        var provider = new WolfAnimationProvider(WolfModelDefinition.WolfEntityTypeId);
        var pose = new EntityPose();
        provider.Apply(provider.CreateState(), Frame(isSitting: true), pose);

        // Body: y += 4, z -= 2, xRot assigned PI/4 (bind PI/2 -> delta -PI/4).
        Assert.Equal(new Vector3(0f, 4f, -2f), pose.Get("body").Position);
        Assert.Equal((MathF.PI / 4f) - (MathF.PI / 2f), pose.Get("body").Rotation.X, 4);

        // Upper body: y += 2 (unscaled), xRot assigned 2PI/5 (bind PI/2 -> delta -PI/10).
        Assert.Equal(new Vector3(0f, 2f, 0f), pose.Get("upper_body").Position);
        Assert.Equal((MathF.PI * 2f / 5f) - (MathF.PI / 2f), pose.Get("upper_body").Rotation.X, 4);

        // Tail: y += 9, z -= 2; wild tail pitch is the bind (delta 0), yaw 0 while still.
        Assert.Equal(new Vector3(0f, 9f, -2f), pose.Get("tail").Position);
        Assert.Equal(0f, pose.Get("tail").Rotation.X, 4);
        Assert.Equal(0f, pose.Get("tail").Rotation.Y, 4);

        // Hind legs: y += 6.7, z -= 5, xRot assigned 3PI/2.
        Assert.Equal(new Vector3(0f, 6.7f, -5f), pose.Get("right_hind_leg").Position);
        Assert.Equal(MathF.PI * 3f / 2f, pose.Get("right_hind_leg").Rotation.X, 4);
        Assert.Equal(new Vector3(0f, 6.7f, -5f), pose.Get("left_hind_leg").Position);
        Assert.Equal(MathF.PI * 3f / 2f, pose.Get("left_hind_leg").Rotation.X, 4);

        // Front legs: x += 0.01 / -0.01, y += 1, xRot assigned 5.811947.
        Assert.Equal(new Vector3(0.01f, 1f, 0f), pose.Get("right_front_leg").Position);
        Assert.Equal(5.811947f, pose.Get("right_front_leg").Rotation.X, 4);
        Assert.Equal(new Vector3(-0.01f, 1f, 0f), pose.Get("left_front_leg").Position);
        Assert.Equal(5.811947f, pose.Get("left_front_leg").Rotation.X, 4);
    }

    [Fact]
    public void SittingPoseScalesByTheBabyAgeFactor()
    {
        var provider = new WolfAnimationProvider(WolfModelDefinition.WolfEntityTypeId);
        var pose = new EntityPose();
        provider.Apply(provider.CreateState(), Frame(isSitting: true, isBaby: true), pose);

        Assert.Equal(new Vector3(0f, 2f, -1f), pose.Get("body").Position);
        Assert.Equal(new Vector3(0f, 4.5f, -1f), pose.Get("tail").Position);
        Assert.Equal(new Vector3(0f, 3.35f, -2.5f), pose.Get("right_hind_leg").Position);
    }

    [Fact]
    public void TailPitchUsesAngryWildAndHealthRatio()
    {
        var provider = new WolfAnimationProvider(WolfModelDefinition.WolfEntityTypeId);

        var angry = new EntityPose();
        provider.Apply(provider.CreateState(), Frame(isAngry: true), angry);
        Assert.Equal(1.5393804f - RefTailBindXRot, angry.Get("tail").Rotation.X, 4);

        var wild = new EntityPose();
        provider.Apply(provider.CreateState(), Frame(), wild);
        Assert.Equal(0f, wild.Get("tail").Rotation.X, 4);

        // Tame: (0.55 - (8-4)/8 * 0.4) * pi - bind.
        var tame = new EntityPose();
        provider.Apply(provider.CreateState(), Frame(isTame: true, health: 4f, maxHealth: 8f), tame);
        Assert.Equal((0.55f - (0.5f * 0.4f)) * MathF.PI - RefTailBindXRot, tame.Get("tail").Rotation.X, 4);
    }

    [Fact]
    public void StandingPoseLeavesTheBlockedShakeRollsUntouched()
    {
        var provider = new WolfAnimationProvider(WolfModelDefinition.WolfEntityTypeId);
        var pose = new EntityPose();
        provider.Apply(provider.CreateState(), Frame(), pose);

        // body.zRot and the real_head/upper_body/real_tail z-rolls need shakeAnim, which the frame lacks.
        Assert.Equal(Vector3.Zero, pose.Get("body").Rotation);
        Assert.Equal(Vector3.Zero, pose.Get("real_head").Rotation);
        Assert.Equal(Vector3.Zero, pose.Get("upper_body").Rotation);
        Assert.Equal(Vector3.Zero, pose.Get("real_tail").Rotation);
    }

    [Fact]
    public void ApplyClipReconstructsEachConceptualClip()
    {
        var provider = new WolfAnimationProvider(WolfModelDefinition.WolfEntityTypeId);
        var state = provider.CreateState();
        var frame = Frame();

        var idle = new EntityPose();
        provider.ApplyClip(state, frame, "idle", 0f, idle);
        Assert.Equal(0f, idle.Get("right_hind_leg").Rotation.X, 4);

        var walk = new EntityPose();
        provider.ApplyClip(state, frame, "walk", 0.25f, walk); // walkPos = 2.5, speed 1
        Assert.Equal(MathF.Cos(2.5f * 0.6662f) * 1.4f, walk.Get("right_hind_leg").Rotation.X, 4);
        Assert.Equal(0f, walk.Get("right_hind_leg").Position.X, 4);

        var sit = new EntityPose();
        provider.ApplyClip(state, frame, "sit", 0f, sit);
        Assert.Equal(new Vector3(0f, 4f, -2f), sit.Get("body").Position);
        Assert.Equal((MathF.PI / 4f) - (MathF.PI / 2f), sit.Get("body").Rotation.X, 4);

        var unknown = new EntityPose();
        provider.ApplyClip(state, frame, "not_a_clip", 1f, unknown);
        Assert.Empty(unknown.Offsets);
    }

    [Fact]
    public void EveryAnimatedPartExistsInTheWolfModel()
    {
        var modelParts = WolfModelDefinition.Definition.Parts.Select(part => part.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var part in new[]
                 {
                     "head", "body", "upper_body", "tail",
                     "right_hind_leg", "left_hind_leg", "right_front_leg", "left_front_leg"
                 })
        {
            Assert.Contains(part, modelParts);
        }
    }
}
