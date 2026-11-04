// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Iron Golem pose coverage ported from the reference <c>IronGolemModel.setupAnim</c> and the
/// <c>IronGolem.handleEntityEvent</c> / <c>aiStep</c> counter lifecycle. Pins the walk arm/leg phases, the head look, the
/// attack and offer-flower arm poses (including the partial-tick attack countdown), the event→counter routing, and the
/// part contract.
/// </summary>
public sealed class IronGolemAnimationTests
{
    private static readonly int TypeId = IronGolemModelDefinition.IronGolemEntityTypeId;

    private static float Rad(float degrees) => degrees * (MathF.PI / 180f);

    private static EntityAnimationFrame Frame(
        float ageInTicks = 0f,
        float walkPos = 0f,
        float walkSpeed = 0f,
        float pitch = 0f,
        float yaw = 0f,
        float partial = 0f) =>
        new(
            TypeId,
            ageInTicks,
            walkPos,
            walkSpeed,
            pitch,
            yaw,
            0f,
            0f,
            0f,
            false,
            false,
            false,
            PartialTick: partial);

    private static EntityPose Apply(IronGolemAnimationProvider provider, IEntityAnimationState state, in EntityAnimationFrame frame)
    {
        var pose = new EntityPose();
        provider.Apply(state, in frame, pose);
        return pose;
    }

    private static void Tick(IEntityAnimationState state, int times)
    {
        var tick = state as IEntityTickAnimationState;
        for (var i = 0; i < times; i++)
        {
            tick!.OnTick(new EntityAnimationTickContext(i, 0f));
        }
    }

    [Fact]
    public void ProviderExposesTheIronGolemTypeIdAndClips()
    {
        var provider = new IronGolemAnimationProvider(TypeId);
        Assert.Equal(TypeId, provider.TypeId);
        Assert.Equal(new[] { "idle", "walk", "attack", "offer_flower" }, provider.Clips);
    }

    [Fact]
    public void WalkArmsAndLegsFollowTheReferenceTriangleWave()
    {
        var provider = new IronGolemAnimationProvider(TypeId);
        var state = provider.CreateState();

        // pos 0 → triangleWave(0, 13) = 1: right arm 1.3, left arm -1.7, right leg -1.5, left leg 1.5.
        var start = Apply(provider, state, Frame(walkPos: 0f, walkSpeed: 1f));
        Assert.Equal(1.3f, start.Get("right_arm").Rotation.X, 4);
        Assert.Equal(-1.7f, start.Get("left_arm").Rotation.X, 4);
        Assert.Equal(-1.5f, start.Get("right_leg").Rotation.X, 4);
        Assert.Equal(1.5f, start.Get("left_leg").Rotation.X, 4);

        // pos 6.5 → triangleWave = -1: the phases flip.
        var half = Apply(provider, state, Frame(walkPos: 6.5f, walkSpeed: 1f));
        Assert.Equal(-1.7f, half.Get("right_arm").Rotation.X, 4);
        Assert.Equal(1.3f, half.Get("left_arm").Rotation.X, 4);
        Assert.Equal(1.5f, half.Get("right_leg").Rotation.X, 4);
        Assert.Equal(-1.5f, half.Get("left_leg").Rotation.X, 4);
    }

    [Fact]
    public void IdleAtZeroWalkIsNeutralAndLegYRotsStayZero()
    {
        var provider = new IronGolemAnimationProvider(TypeId);
        var pose = Apply(provider, provider.CreateState(), Frame());

        Assert.Equal(0f, pose.Get("right_arm").Rotation.X, 4);
        Assert.Equal(0f, pose.Get("left_arm").Rotation.X, 4);
        Assert.Equal(0f, pose.Get("right_leg").Rotation.X, 4);
        Assert.Equal(0f, pose.Get("left_leg").Rotation.X, 4);
        Assert.Equal(0f, pose.Get("right_leg").Rotation.Y, 4);
        Assert.Equal(0f, pose.Get("left_leg").Rotation.Y, 4);
    }

    [Fact]
    public void HeadLooksAlongPitchAndYawInRadians()
    {
        var provider = new IronGolemAnimationProvider(TypeId);
        var pose = Apply(provider, provider.CreateState(), Frame(pitch: 30f, yaw: -45f));

        Assert.Equal(Rad(30f), pose.Get("head").Rotation.X, 4);
        Assert.Equal(Rad(-45f), pose.Get("head").Rotation.Y, 4);
        Assert.Equal(0f, pose.Get("head").Rotation.Z, 4);
    }

    [Fact]
    public void AttackEventArmsBothArmsAndDecrementsPerTick()
    {
        var provider = new IronGolemAnimationProvider(TypeId);
        var state = provider.CreateState();
        ((IEntityEventAnimationState)state).OnEntityEvent(4, gameTime: 0); // START_ATTACKING → 10

        // Counter 10, partial 0: triangleWave(10, 10) = 1 → arm = -2 + 1.5 = -0.5.
        var armed = Apply(provider, state, Frame());
        Assert.Equal(-0.5f, armed.Get("right_arm").Rotation.X, 4);
        Assert.Equal(-0.5f, armed.Get("left_arm").Rotation.X, 4);

        // Five ticks later the counter is 5: triangleWave(5, 10) = -1 → arm = -3.5.
        Tick(state, 5);
        var mid = Apply(provider, state, Frame());
        Assert.Equal(-3.5f, mid.Get("right_arm").Rotation.X, 4);
        Assert.Equal(-3.5f, mid.Get("left_arm").Rotation.X, 4);
    }

    [Fact]
    public void AttackCountdownUsesTheFramePartialTick()
    {
        var provider = new IronGolemAnimationProvider(TypeId);
        var state = provider.CreateState();
        ((IEntityEventAnimationState)state).OnEntityEvent(4, gameTime: 0);

        // attackTicksRemaining = 10 - 0.5 = 9.5; triangleWave(9.5, 10) = 0.8 → arm = -0.8.
        var pose = Apply(provider, state, Frame(partial: 0.5f));
        Assert.Equal(-0.8f, pose.Get("right_arm").Rotation.X, 4);
        Assert.Equal(-0.8f, pose.Get("left_arm").Rotation.X, 4);
    }

    [Fact]
    public void OfferFlowerEventPosesTheRightArmAndLeavesTheLeftNeutral()
    {
        var provider = new IronGolemAnimationProvider(TypeId);
        var state = provider.CreateState();
        ((IEntityEventAnimationState)state).OnEntityEvent(11, gameTime: 0); // START_OFFERING_FLOWER → 400

        var pose = Apply(provider, state, Frame());
        // triangleWave(400, 70) = -1/7 → right arm = -0.8 + 0.025 * (-1/7) ≈ -0.803571.
        Assert.Equal(-0.803571f, pose.Get("right_arm").Rotation.X, 4);
        Assert.Equal(0f, pose.Get("left_arm").Rotation.X, 4);
    }

    [Fact]
    public void StopOfferEventReturnsToTheWalkPose()
    {
        var provider = new IronGolemAnimationProvider(TypeId);
        var state = provider.CreateState();
        ((IEntityEventAnimationState)state).OnEntityEvent(11, gameTime: 0);
        ((IEntityEventAnimationState)state).OnEntityEvent(34, gameTime: 0); // STOP_OFFERING_FLOWER → 0

        var pose = Apply(provider, state, Frame(walkPos: 0f, walkSpeed: 1f));
        Assert.Equal(1.3f, pose.Get("right_arm").Rotation.X, 4);
        Assert.Equal(-1.7f, pose.Get("left_arm").Rotation.X, 4);
    }

    [Fact]
    public void AttackTakesPrecedenceOverOfferFlower()
    {
        var provider = new IronGolemAnimationProvider(TypeId);
        var state = provider.CreateState();
        ((IEntityEventAnimationState)state).OnEntityEvent(11, gameTime: 0); // offer 400
        ((IEntityEventAnimationState)state).OnEntityEvent(4, gameTime: 0);  // attack 10

        var pose = Apply(provider, state, Frame());
        Assert.Equal(-0.5f, pose.Get("right_arm").Rotation.X, 4);
        Assert.Equal(-0.5f, pose.Get("left_arm").Rotation.X, 4);
    }

    [Fact]
    public void OfferFlowerTickAlsoDecrementsPerTick()
    {
        var provider = new IronGolemAnimationProvider(TypeId);
        var state = provider.CreateState();
        ((IEntityEventAnimationState)state).OnEntityEvent(11, gameTime: 0);

        Tick(state, 1); // 400 → 399
        var pose = Apply(provider, state, Frame());
        // triangleWave(399, 70): 399 % 70 = 49 → |49-35| - 17.5 = -3.5 → /17.5 = -0.2.
        Assert.Equal(-0.8f + (0.025f * -0.2f), pose.Get("right_arm").Rotation.X, 4);
    }

    [Fact]
    public void LegsStillWalkWhileAttacking()
    {
        var provider = new IronGolemAnimationProvider(TypeId);
        var state = provider.CreateState();
        ((IEntityEventAnimationState)state).OnEntityEvent(4, gameTime: 0);

        var pose = Apply(provider, state, Frame(walkPos: 0f, walkSpeed: 1f));
        Assert.Equal(-1.5f, pose.Get("right_leg").Rotation.X, 4);
        Assert.Equal(1.5f, pose.Get("left_leg").Rotation.X, 4);
    }

    [Fact]
    public void ApplyDrivesExactlyTheReferenceParts()
    {
        var provider = new IronGolemAnimationProvider(TypeId);
        var pose = Apply(provider, provider.CreateState(), Frame(walkPos: 3f, walkSpeed: 0.5f));

        var expected = new HashSet<string>(StringComparer.Ordinal)
        {
            "head", "right_arm", "left_arm", "right_leg", "left_leg"
        };
        Assert.True(expected.SetEquals(pose.Offsets.Keys));

        var modelParts = IronGolemModelDefinition.Definition.Parts.Select(part => part.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var part in expected)
        {
            Assert.Contains(part, modelParts);
        }
    }

    [Fact]
    public void ApplyClipPreviewPlaysEachNamedClip()
    {
        var provider = new IronGolemAnimationProvider(TypeId);
        var state = provider.CreateState();
        var frame = Frame();

        var walk = new EntityPose();
        provider.ApplyClip(state, frame, "walk", seconds: 0f, walk);
        Assert.Equal(1.3f, walk.Get("right_arm").Rotation.X, 4);

        var idle = new EntityPose();
        provider.ApplyClip(state, frame, "idle", seconds: 0f, idle);
        Assert.Equal(0f, idle.Get("right_arm").Rotation.X, 4);

        var attack = new EntityPose();
        provider.ApplyClip(state, frame, "attack", seconds: 0f, attack);
        Assert.Equal(-0.5f, attack.Get("right_arm").Rotation.X, 4);
    }
}
