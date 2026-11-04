// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Ravager procedural animation ported from the reference RavagerModel.setupAnim. These tests pin the four-leg walk
/// phases, head look, the attack neck/mouth pose at specific remaining ticks, the stunned neck sway, the roar mouth, and
/// the client-side entity-event → counter routing that feeds those poses.
/// </summary>
public sealed class RavagerAnimationTests
{
    private const float ToRad = MathF.PI / 180f;
    private const float NeckRotWhenStunned = 0.21991149f;

    private static EntityAnimationFrame Frame(
        float pitch = 0f,
        float yaw = 0f,
        float walkPos = 0f,
        float walkSpeed = 0f,
        float partialTick = 0f) => new(
        TypeId: RavagerModelDefinition.RavagerEntityTypeId,
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
        IsHidingInShell: false,
        PartialTick: partialTick);

    [Fact]
    public void ProviderExposesReferenceTypeIdAndClips()
    {
        var provider = new RavagerAnimationProvider(RavagerModelDefinition.RavagerEntityTypeId);
        Assert.Equal(112, provider.TypeId);
        Assert.Equal(new[] { "idle", "walk", "attack", "stun", "roar" }, provider.Clips);
    }

    [Fact]
    public void FourLegsFollowTheWalkCosinePhases()
    {
        var provider = new RavagerAnimationProvider(RavagerModelDefinition.RavagerEntityTypeId);
        var pose = new EntityPose();
        provider.Apply(provider.CreateState(), Frame(walkPos: 1f, walkSpeed: 1f), pose);

        var phase = 1f * 0.6662f;
        var legRot = 0.4f * 1f;
        var swing = MathF.Cos(phase) * legRot;

        Assert.Equal(swing, pose.Get("right_hind_leg").Rotation.X, 4);
        Assert.Equal(-swing, pose.Get("left_hind_leg").Rotation.X, 4);
        Assert.Equal(-swing, pose.Get("right_front_leg").Rotation.X, 4);
        Assert.Equal(swing, pose.Get("left_front_leg").Rotation.X, 4);
    }

    [Fact]
    public void LegAmplitudeScalesWithWalkSpeed()
    {
        var provider = new RavagerAnimationProvider(RavagerModelDefinition.RavagerEntityTypeId);
        var fast = new EntityPose();
        var slow = new EntityPose();
        provider.Apply(provider.CreateState(), Frame(walkPos: 2f, walkSpeed: 1f), fast);
        provider.Apply(provider.CreateState(), Frame(walkPos: 2f, walkSpeed: 0.5f), slow);

        Assert.Equal(fast.Get("right_hind_leg").Rotation.X * 0.5f, slow.Get("right_hind_leg").Rotation.X, 4);
    }

    [Fact]
    public void HeadLooksAlongPitchAndYawInRadians()
    {
        var provider = new RavagerAnimationProvider(RavagerModelDefinition.RavagerEntityTypeId);
        var pose = new EntityPose();
        provider.Apply(provider.CreateState(), Frame(pitch: 30f, yaw: -45f), pose);

        Assert.Equal(30f * ToRad, pose.Get("head").Rotation.X, 4);
        Assert.Equal(-45f * ToRad, pose.Get("head").Rotation.Y, 4);
        Assert.Equal(0f, pose.Get("head").Rotation.Z, 4);
        Assert.Equal(Vector3.Zero, pose.Get("head").Position);
    }

    [Fact]
    public void AttackRaisesNeckAndOpensMouthOverItsTenTicks()
    {
        var provider = new RavagerAnimationProvider(RavagerModelDefinition.RavagerEntityTypeId);
        var state = provider.CreateState();
        var events = (IEntityEventAnimationState)state;
        var ticks = (IEntityTickAnimationState)state;

        events.OnEntityEvent(4, 0); // attack -> 10 ticks remaining
        var start = new EntityPose();
        provider.Apply(state, Frame(), start);
        // triangleWave(10, 10) = 1 -> headPos 12 -> neck z back at its bind offset 0.
        Assert.Equal(0f, start.Get("neck").Position.Z, 4);
        Assert.Equal(0f, start.Get("neck").Rotation.X, 4);
        Assert.Equal(MathF.Sin((-4f + 10f) / 4f) * MathF.PI * 0.4f, start.Get("mouth").Rotation.X, 4);

        for (var i = 0; i < 5; i++)
        {
            ticks.OnTick(new EntityAnimationTickContext(i, 0f));
        }

        var mid = new EntityPose();
        provider.Apply(state, Frame(), mid);
        // triangleWave(5, 10) = -1 -> headPos 0 -> neck z offset -12 (dips down).
        Assert.Equal(-12f, mid.Get("neck").Position.Z, 4);
        Assert.Equal((MathF.PI / 20f) * MathF.Sin(MathF.PI * 5f / 10f), mid.Get("mouth").Rotation.X, 4);

        // Five more ticks end the attack and return to the idle mouth.
        for (var i = 0; i < 5; i++)
        {
            ticks.OnTick(new EntityAnimationTickContext(5 + i, 0f));
        }

        var done = new EntityPose();
        provider.Apply(state, Frame(), done);
        Assert.Equal(0f, done.Get("neck").Position.Z, 4);
        Assert.Equal(MathF.PI * 0.01f, done.Get("mouth").Rotation.X, 4);
    }

    [Fact]
    public void AttackUsesPartialTickToInterpolateTheNeckLift()
    {
        var provider = new RavagerAnimationProvider(RavagerModelDefinition.RavagerEntityTypeId);
        var state = provider.CreateState();
        ((IEntityEventAnimationState)state).OnEntityEvent(4, 0);

        var pose = new EntityPose();
        provider.Apply(state, Frame(partialTick: 0.5f), pose);

        // attackRemaining = 9.5 -> triangleWave 0.8 -> scaled 0.9 -> headPos 8.748 -> neck z offset -3.252.
        Assert.Equal(-3.252f, pose.Get("neck").Position.Z, 3);
    }

    [Fact]
    public void StunTiltsAndSwaysTheNeck()
    {
        var provider = new RavagerAnimationProvider(RavagerModelDefinition.RavagerEntityTypeId);
        var state = provider.CreateState();
        var events = (IEntityEventAnimationState)state;
        var ticks = (IEntityTickAnimationState)state;

        events.OnEntityEvent(39, 0); // stunned -> 40 ticks remaining
        var start = new EntityPose();
        provider.Apply(state, Frame(), start);
        Assert.Equal(NeckRotWhenStunned, start.Get("neck").Rotation.X, 5);
        Assert.Equal(MathF.Sin(1f * 10f) * 3f, start.Get("neck").Position.X, 4);
        Assert.Equal(MathF.PI * 0.05f, start.Get("mouth").Rotation.X, 4);

        for (var i = 0; i < 20; i++)
        {
            ticks.OnTick(new EntityAnimationTickContext(i, 0f));
        }

        var halfway = new EntityPose();
        provider.Apply(state, Frame(), halfway);
        // stunnedTick 20 -> speed 0.5.
        Assert.Equal(NeckRotWhenStunned, halfway.Get("neck").Rotation.X, 5);
        Assert.Equal(MathF.Sin(0.5f * 10f) * 3f, halfway.Get("neck").Position.X, 4);
    }

    [Fact]
    public void FinishingAStunStartsTheRoarMouth()
    {
        var provider = new RavagerAnimationProvider(RavagerModelDefinition.RavagerEntityTypeId);
        var state = provider.CreateState();
        var events = (IEntityEventAnimationState)state;
        var ticks = (IEntityTickAnimationState)state;

        events.OnEntityEvent(39, 0);
        for (var i = 0; i < 40; i++)
        {
            ticks.OnTick(new EntityAnimationTickContext(i, 0f));
        }

        // The stun's final tick sets roarTick = 20, so roarAnimation is 0 on that frame: idle mouth, neck level.
        var justAfterStun = new EntityPose();
        provider.Apply(state, Frame(), justAfterStun);
        Assert.Equal(0f, justAfterStun.Get("neck").Rotation.X, 4);
        Assert.Equal(Vector3.Zero, justAfterStun.Get("neck").Position);
        Assert.Equal(MathF.PI * 0.01f, justAfterStun.Get("mouth").Rotation.X, 4);

        for (var i = 0; i < 5; i++)
        {
            ticks.OnTick(new EntityAnimationTickContext(40 + i, 0f));
        }

        // roarTick 15 -> roarAnimation (20 - 15) / 20 = 0.25.
        var roaring = new EntityPose();
        provider.Apply(state, Frame(), roaring);
        Assert.Equal((MathF.PI / 2f) * MathF.Sin(0.25f * MathF.PI * 0.25f), roaring.Get("mouth").Rotation.X, 4);
    }

    [Fact]
    public void UnknownEventAndMissingClipLeaveTheRestingPose()
    {
        var provider = new RavagerAnimationProvider(RavagerModelDefinition.RavagerEntityTypeId);
        var state = provider.CreateState();
        ((IEntityEventAnimationState)state).OnEntityEvent(69, 0); // roar effects: no counter change
        ((IEntityEventAnimationState)state).OnEntityEvent(123, 0);

        var pose = new EntityPose();
        provider.Apply(state, Frame(), pose);
        Assert.Equal(MathF.PI * 0.01f, pose.Get("mouth").Rotation.X, 4);
        Assert.Equal(Vector3.Zero, pose.Get("neck").Position);

        var unknown = new EntityPose();
        provider.ApplyClip(state, Frame(), "not_a_clip", 1f, unknown);
        Assert.Empty(unknown.Offsets);
    }

    [Fact]
    public void ApplyClipReconstructsEachConceptualClip()
    {
        var provider = new RavagerAnimationProvider(RavagerModelDefinition.RavagerEntityTypeId);
        var state = provider.CreateState();
        var frame = Frame();

        var idle = new EntityPose();
        provider.ApplyClip(state, frame, "idle", 0f, idle);
        Assert.Equal(MathF.PI * 0.01f, idle.Get("mouth").Rotation.X, 4);
        Assert.Equal(0f, idle.Get("right_hind_leg").Rotation.X, 4);

        var walk = new EntityPose();
        provider.ApplyClip(state, frame, "walk", 0.25f, walk);
        Assert.Equal(MathF.Cos(2.5f * 0.6662f) * 0.4f, walk.Get("right_hind_leg").Rotation.X, 4);

        var attack = new EntityPose();
        provider.ApplyClip(state, frame, "attack", 0.25f, attack); // remaining 10 - 5 = 5 ticks
        Assert.Equal(-12f, attack.Get("neck").Position.Z, 4);
        Assert.Equal((MathF.PI / 20f) * MathF.Sin(MathF.PI * 5f / 10f), attack.Get("mouth").Rotation.X, 4);

        var stun = new EntityPose();
        provider.ApplyClip(state, frame, "stun", 1f, stun); // remaining 40 - 20 = 20 ticks
        Assert.Equal(NeckRotWhenStunned, stun.Get("neck").Rotation.X, 5);
        Assert.Equal(MathF.Sin(0.5f * 10f) * 3f, stun.Get("neck").Position.X, 4);

        var roar = new EntityPose();
        provider.ApplyClip(state, frame, "roar", 0.25f, roar);
        Assert.Equal((MathF.PI / 2f) * MathF.Sin(0.25f * MathF.PI * 0.25f), roar.Get("mouth").Rotation.X, 4);
    }

    [Fact]
    public void EveryProceduralPartExistsInTheRavagerModel()
    {
        var modelParts = RavagerModelDefinition.Definition.Parts.Select(part => part.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var part in new[]
                 {
                     "neck", "head", "mouth", "right_hind_leg", "left_hind_leg", "right_front_leg", "left_front_leg"
                 })
        {
            Assert.Contains(part, modelParts);
        }
    }
}
