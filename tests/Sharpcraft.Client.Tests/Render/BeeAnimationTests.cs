// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Bee animation coverage ported from <c>BeeModel.setupAnim</c> + <c>Bee.rollAmount</c>. Pins the wing flap, the
/// airborne leg tuck, the calm-flight bob (body + legs + antennae), the roll rotation driven by the synced flags byte
/// (bit 2), and the part-name contract. Stinger visibility is blocked (no visibility channel) and is not faked.
/// </summary>
public sealed class BeeAnimationTests
{
    private static readonly int TypeId = BeeModelDefinition.BeeEntityTypeId;

    private const float Deg = MathF.PI / 180f;

    private static EntityAnimationFrame Frame(
        float ageInTicks = 0f,
        bool onGround = false,
        bool isAngry = false,
        float partial = 0f) =>
        new(
            TypeId,
            ageInTicks,
            0f,
            0f,
            0f,
            0f,
            0f,
            0f,
            0f,
            false,
            false,
            false,
            PartialTick: partial,
            IsOnGround: onGround,
            IsAngry: isAngry);

    private static EntityPose Apply(IEntityAnimationState state, IEntityAnimationProvider provider, in EntityAnimationFrame frame)
    {
        var pose = new EntityPose();
        provider.Apply(state, in frame, pose);
        return pose;
    }

    private static void Tick(IEntityAnimationState state) =>
        ((IEntityTickAnimationState)state).OnTick(new EntityAnimationTickContext(1, 0f, false));

    [Fact]
    public void ProviderExposesClipsAndItsTypeId()
    {
        var provider = new BeeAnimationProvider(TypeId);
        Assert.Equal(11, provider.TypeId);
        Assert.Equal(new[] { "idle", "fly", "roll" }, provider.Clips);
    }

    [Fact]
    public void WingsFlapOnZRotAndZeroTheirBindYRotWhileFlying()
    {
        var provider = new BeeAnimationProvider(TypeId);
        var pose = Apply(provider.CreateState(), provider, Frame(ageInTicks: 0f, onGround: false, isAngry: true));

        var flap = MathF.PI * 0.15f; // cos(0) = 1
        Assert.Equal(flap, pose.Get("right_wing").Rotation.Z, 4);
        Assert.Equal(-flap, pose.Get("left_wing").Rotation.Z, 4);
        // Bind wing yRot is ±0.2618 and the reference zeroes it in flight.
        Assert.Equal(0.2618f, pose.Get("right_wing").Rotation.Y, 4);
        Assert.Equal(-0.2618f, pose.Get("left_wing").Rotation.Y, 4);

        // At age where the flap phase is π/2 the zRot is zero.
        var quarterAge = (MathF.PI / 2f) / (120.32113f * Deg);
        var quarter = Apply(provider.CreateState(), provider, Frame(ageInTicks: quarterAge, onGround: false, isAngry: true));
        Assert.Equal(0f, quarter.Get("right_wing").Rotation.Z, 3);
    }

    [Fact]
    public void AirborneLegsTuckToPiOverFour()
    {
        var provider = new BeeAnimationProvider(TypeId);
        var pose = Apply(provider.CreateState(), provider, Frame(onGround: false, isAngry: true));

        Assert.Equal(MathF.PI / 4f, pose.Get("front_legs").Rotation.X, 4);
        Assert.Equal(MathF.PI / 4f, pose.Get("middle_legs").Rotation.X, 4);
        Assert.Equal(MathF.PI / 4f, pose.Get("back_legs").Rotation.X, 4);
    }

    [Fact]
    public void CalmFlightBobsTheBodyLegsAndAntennae()
    {
        var provider = new BeeAnimationProvider(TypeId);
        var pose = Apply(provider.CreateState(), provider, Frame(ageInTicks: 0f, onGround: false, isAngry: false));

        // age 0 → cos = 1.
        Assert.Equal(0.1f + (MathF.PI * 0.025f), pose.Get("bone").Rotation.X, 4);
        Assert.Equal(0.9f, pose.Get("bone").Position.Y, 4);
        Assert.Equal((MathF.PI / 8f) - (MathF.PI * 0.1f), pose.Get("front_legs").Rotation.X, 4);
        Assert.Equal((MathF.PI / 4f) - (MathF.PI * 0.05f), pose.Get("back_legs").Rotation.X, 4);
        Assert.Equal(MathF.PI / 4f, pose.Get("middle_legs").Rotation.X, 4);
        Assert.Equal(MathF.PI * 0.03f, pose.Get("left_antenna").Rotation.X, 4);
        Assert.Equal(MathF.PI * 0.03f, pose.Get("right_antenna").Rotation.X, 4);
    }

    [Fact]
    public void AngryFlightSkipsTheBob()
    {
        var provider = new BeeAnimationProvider(TypeId);
        var pose = Apply(provider.CreateState(), provider, Frame(ageInTicks: 0f, onGround: false, isAngry: true));

        Assert.Equal(0f, pose.Get("bone").Rotation.X, 6);
        Assert.Equal(0f, pose.Get("bone").Position.Y, 6);
        Assert.False(pose.Offsets.ContainsKey("left_antenna"));
    }

    [Fact]
    public void GroundedBeeRestsAllAppendages()
    {
        var provider = new BeeAnimationProvider(TypeId);
        var pose = Apply(provider.CreateState(), provider, Frame(ageInTicks: 3f, onGround: true, isAngry: false));

        Assert.Single(pose.Offsets);
        Assert.True(pose.Offsets.ContainsKey("bone"));
        Assert.Equal(0f, pose.Get("bone").Rotation.X, 6);
        Assert.False(pose.Offsets.ContainsKey("right_wing"));
        Assert.False(pose.Offsets.ContainsKey("front_legs"));
    }

    [Fact]
    public void RollingFlagDrivesTheRollAmountClock()
    {
        var provider = new BeeAnimationProvider(TypeId);
        var state = provider.CreateState();
        ((IEntitySyncedDataAnimationState)state).OnSyncedData(18, 0, 2); // FLAG_ROLL (bit 2)

        for (var i = 0; i < 5; i++)
        {
            Tick(state);
        }

        // 5 ticks × +0.2 clamps to 1 → bone.xRot = rotLerpRad(1, 0, 3.0915928). partial 1 reads the current value.
        var full = Apply(state, provider, Frame(ageInTicks: 0f, onGround: false, isAngry: true, partial: 1f));
        Assert.Equal(3.0915928f, full.Get("bone").Rotation.X, 4);

        // Three ticks gives rollAmount 0.6 → bone.xRot = 0.6 · 3.0915928.
        var rampState = provider.CreateState();
        ((IEntitySyncedDataAnimationState)rampState).OnSyncedData(18, 0, 2);
        for (var i = 0; i < 3; i++)
        {
            Tick(rampState);
        }

        var ramp = Apply(rampState, provider, Frame(ageInTicks: 0f, onGround: false, isAngry: true, partial: 1f));
        Assert.Equal(0.6f * 3.0915928f, ramp.Get("bone").Rotation.X, 3);
    }

    [Fact]
    public void RollingDecaysWhenTheFlagClears()
    {
        var provider = new BeeAnimationProvider(TypeId);
        var state = provider.CreateState();
        var synced = (IEntitySyncedDataAnimationState)state;

        synced.OnSyncedData(18, 0, 2); // rolling on
        for (var i = 0; i < 5; i++)
        {
            Tick(state);
        }

        synced.OnSyncedData(18, 0, 0); // rolling off
        for (var i = 0; i < 5; i++)
        {
            Tick(state);
        }

        var pose = Apply(state, provider, Frame(ageInTicks: 0f, onGround: false, isAngry: true, partial: 1f));
        Assert.Equal(0f, pose.Get("bone").Rotation.X, 4);
    }

    [Fact]
    public void WrongIndexOrSerializerDoesNotDriveRolling()
    {
        var provider = new BeeAnimationProvider(TypeId);

        var wrongIndex = provider.CreateState();
        ((IEntitySyncedDataAnimationState)wrongIndex).OnSyncedData(17, 0, 2); // wrong index
        for (var i = 0; i < 5; i++)
        {
            Tick(wrongIndex);
        }

        Assert.Equal(0f, Apply(wrongIndex, provider, Frame(onGround: false, isAngry: true)).Get("bone").Rotation.X, 6);

        var wrongSerializer = provider.CreateState();
        ((IEntitySyncedDataAnimationState)wrongSerializer).OnSyncedData(18, 8, 2); // boolean serializer, not byte
        for (var i = 0; i < 5; i++)
        {
            Tick(wrongSerializer);
        }

        Assert.Equal(0f, Apply(wrongSerializer, provider, Frame(onGround: false, isAngry: true)).Get("bone").Rotation.X, 6);
    }

    [Fact]
    public void ApplyClipReconstructsIdleFlyAndRoll()
    {
        var provider = new BeeAnimationProvider(TypeId);
        var state = provider.CreateState();

        var idle = new EntityPose();
        provider.ApplyClip(state, Frame(), "idle", 0f, idle);
        Assert.Single(idle.Offsets);

        var fly = new EntityPose();
        provider.ApplyClip(state, Frame(), "fly", 0f, fly);
        Assert.Equal(MathF.PI * 0.15f, fly.Get("right_wing").Rotation.Z, 4);

        var roll = new EntityPose();
        provider.ApplyClip(state, Frame(), "roll", 0f, roll);
        Assert.Equal(3.0915928f, roll.Get("bone").Rotation.X, 4);

        var unknown = new EntityPose();
        provider.ApplyClip(state, Frame(), "not_a_clip", 1f, unknown);
        Assert.Empty(unknown.Offsets);
    }

    [Fact]
    public void EveryAnimatedPartExistsAndStingerIsNotFaked()
    {
        var modelParts = BeeModelDefinition.Definition.Parts
            .Select(part => part.Name)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var part in new[]
                 {
                     "bone", "right_wing", "left_wing", "front_legs", "middle_legs", "back_legs",
                     "left_antenna", "right_antenna", "stinger"
                 })
        {
            Assert.Contains(part, modelParts);
        }

        var provider = new BeeAnimationProvider(TypeId);
        var pose = Apply(provider.CreateState(), provider, Frame(onGround: false, isAngry: false));
        // Stinger visibility is blocked; the stinger must not receive a fake transform.
        Assert.False(pose.Offsets.ContainsKey("stinger"));
    }
}
