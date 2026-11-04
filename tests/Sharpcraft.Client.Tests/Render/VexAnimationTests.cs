// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Vex procedural animation coverage ported from <c>VexModel.setupAnim</c> / <c>setArmsCharging</c>. Pins the head
/// look, the cosine arm Z-bob, the idle vs charging body tilt, all <c>setArmsCharging</c> branches, the wing flap, the
/// synced flags-byte routing (index 16, serializer 0, bit 0 = charging) and the part-name contract.
/// </summary>
public sealed class VexAnimationTests
{
    private static readonly int TypeId = VexModelDefinition.VexEntityTypeId;

    private const float Deg = MathF.PI / 180f;

    private static float Rad(float degrees) => degrees * Deg;

    private static float Bob(float ageInTicks) => MathF.Cos(ageInTicks * 5.5f * Deg) * 0.1f;

    private static float WingFlap(float ageInTicks) => MathF.Cos(ageInTicks * 45.836624f * Deg) * Deg * 16.2f;

    private static EntityAnimationFrame Frame(
        float ageInTicks = 0f,
        float pitch = 0f,
        float yaw = 0f,
        bool mainHandItem = false,
        bool offHandItem = false) =>
        new(
            TypeId,
            ageInTicks,
            0f,
            0f,
            pitch,
            yaw,
            0f,
            0f,
            0f,
            false,
            false,
            false,
            0f,
            mainHandItem,
            offHandItem);

    private static EntityPose Apply(IEntityAnimationState state, IEntityAnimationProvider provider, in EntityAnimationFrame frame)
    {
        var pose = new EntityPose();
        provider.Apply(state, in frame, pose);
        return pose;
    }

    private static void SetCharging(IEntityAnimationState state, bool charging) =>
        ((IEntitySyncedDataAnimationState)state).OnSyncedData(16, 0, charging ? 1 : 0);

    private static void AssertVec(Vector3 expected, Vector3 actual, int precision = 4)
    {
        Assert.Equal(expected.X, actual.X, precision);
        Assert.Equal(expected.Y, actual.Y, precision);
        Assert.Equal(expected.Z, actual.Z, precision);
    }

    [Fact]
    public void ProviderExposesIdleChargingAndItsTypeId()
    {
        var provider = new VexAnimationProvider(TypeId);
        Assert.Equal(142, provider.TypeId);
        Assert.Equal(new[] { "idle", "charging" }, provider.Clips);
    }

    [Fact]
    public void HeadLooksAlongPitchAndYaw()
    {
        var provider = new VexAnimationProvider(TypeId);
        var pose = Apply(provider.CreateState(), provider, Frame(pitch: 30f, yaw: -45f));
        AssertVec(new Vector3(Rad(30f), Rad(-45f), 0f), pose.Get("head").Rotation);
    }

    [Fact]
    public void ArmsRestAtPiOverFivePlusTheCosineBob()
    {
        var provider = new VexAnimationProvider(TypeId);
        var state = provider.CreateState();

        var atZero = Apply(state, provider, Frame(ageInTicks: 0f));
        AssertVec(new Vector3(0f, 0f, (MathF.PI / 5f) + 0.1f), atZero.Get("right_arm").Rotation);
        AssertVec(new Vector3(0f, 0f, -((MathF.PI / 5f) + 0.1f)), atZero.Get("left_arm").Rotation);

        var later = Apply(state, provider, Frame(ageInTicks: 9f));
        AssertVec(new Vector3(0f, 0f, (MathF.PI / 5f) + Bob(9f)), later.Get("right_arm").Rotation);
        AssertVec(new Vector3(0f, 0f, -((MathF.PI / 5f) + Bob(9f))), later.Get("left_arm").Rotation);
    }

    [Fact]
    public void WingsFlapFromAge()
    {
        var provider = new VexAnimationProvider(TypeId);
        var state = provider.CreateState();

        var atZero = Apply(state, provider, Frame(ageInTicks: 0f));
        var leftY = 1.0995574f + (Deg * 16.2f);
        Assert.Equal(leftY, atZero.Get("left_wing").Rotation.Y, 4);
        Assert.Equal(-leftY, atZero.Get("right_wing").Rotation.Y, 4);
        Assert.Equal(0.47123888f, atZero.Get("left_wing").Rotation.X, 6);
        Assert.Equal(-0.47123888f, atZero.Get("left_wing").Rotation.Z, 6);
        Assert.Equal(0.47123888f, atZero.Get("right_wing").Rotation.X, 6);
        Assert.Equal(0.47123888f, atZero.Get("right_wing").Rotation.Z, 6);

        var later = Apply(state, provider, Frame(ageInTicks: 7f));
        Assert.Equal(1.0995574f + WingFlap(7f), later.Get("left_wing").Rotation.Y, 4);
        Assert.Equal(-(1.0995574f + WingFlap(7f)), later.Get("right_wing").Rotation.Y, 4);
    }

    [Fact]
    public void IdleTiltsBodyChargingLevelsIt()
    {
        var provider = new VexAnimationProvider(TypeId);
        var state = provider.CreateState();

        var idle = Apply(state, provider, Frame(ageInTicks: 0f));
        AssertVec(new Vector3(MathF.PI / 20f, 0f, 0f), idle.Get("body").Rotation);

        SetCharging(state, true);
        var charging = Apply(state, provider, Frame(ageInTicks: 0f));
        AssertVec(Vector3.Zero, charging.Get("body").Rotation);
    }

    [Fact]
    public void ChargingWithBothHandsEmptyUsesTheSymmetricRaisedPose()
    {
        var provider = new VexAnimationProvider(TypeId);
        var state = provider.CreateState();
        SetCharging(state, true);

        var pose = Apply(state, provider, Frame(ageInTicks: 0f));
        var bob = 0.1f;
        AssertVec(new Vector3(-1.2217305f, MathF.PI / 12f, -0.47123888f - bob), pose.Get("right_arm").Rotation);
        AssertVec(new Vector3(-1.2217305f, -MathF.PI / 12f, 0.47123888f + bob), pose.Get("left_arm").Rotation);
    }

    [Fact]
    public void ChargingWithMainHandItemOnlyRaisesTheRightArm()
    {
        var provider = new VexAnimationProvider(TypeId);
        var state = provider.CreateState();
        SetCharging(state, true);

        var pose = Apply(state, provider, Frame(ageInTicks: 0f, mainHandItem: true));
        AssertVec(new Vector3(MathF.PI * 7f / 6f, MathF.PI / 12f, -0.47123888f - 0.1f), pose.Get("right_arm").Rotation);
        // The empty off hand keeps the resting spread.
        AssertVec(new Vector3(0f, 0f, -((MathF.PI / 5f) + 0.1f)), pose.Get("left_arm").Rotation);
    }

    [Fact]
    public void ChargingWithOffHandItemOnlyRaisesTheLeftArm()
    {
        var provider = new VexAnimationProvider(TypeId);
        var state = provider.CreateState();
        SetCharging(state, true);

        var pose = Apply(state, provider, Frame(ageInTicks: 0f, offHandItem: true));
        AssertVec(new Vector3(MathF.PI * 7f / 6f, -MathF.PI / 12f, 0.47123888f + 0.1f), pose.Get("left_arm").Rotation);
        AssertVec(new Vector3(0f, 0f, (MathF.PI / 5f) + 0.1f), pose.Get("right_arm").Rotation);
    }

    [Fact]
    public void ChargingWithBothHandsHoldingItemsRaisesBothArms()
    {
        var provider = new VexAnimationProvider(TypeId);
        var state = provider.CreateState();
        SetCharging(state, true);

        var pose = Apply(state, provider, Frame(ageInTicks: 0f, mainHandItem: true, offHandItem: true));
        AssertVec(new Vector3(MathF.PI * 7f / 6f, MathF.PI / 12f, -0.47123888f - 0.1f), pose.Get("right_arm").Rotation);
        AssertVec(new Vector3(MathF.PI * 7f / 6f, -MathF.PI / 12f, 0.47123888f + 0.1f), pose.Get("left_arm").Rotation);
    }

    [Fact]
    public void SyncedFlagsByteBitZeroDrivesCharging()
    {
        var provider = new VexAnimationProvider(TypeId);

        var idleState = provider.CreateState();
        var idle = Apply(idleState, provider, Frame(ageInTicks: 0f));
        AssertVec(new Vector3(MathF.PI / 20f, 0f, 0f), idle.Get("body").Rotation);

        var chargingState = provider.CreateState();
        ((IEntitySyncedDataAnimationState)chargingState).OnSyncedData(16, 0, 1); // FLAG_IS_CHARGING
        var charging = Apply(chargingState, provider, Frame(ageInTicks: 0f));
        AssertVec(Vector3.Zero, charging.Get("body").Rotation);

        // Non-charging bits (e.g. only bit 1) stay idle.
        var otherBits = provider.CreateState();
        ((IEntitySyncedDataAnimationState)otherBits).OnSyncedData(16, 0, 2);
        AssertVec(new Vector3(MathF.PI / 20f, 0f, 0f), Apply(otherBits, provider, Frame(ageInTicks: 0f)).Get("body").Rotation);

        // The right index with the wrong serializer (AgeableMob baby boolean uses serializer 8 at index 16) is ignored.
        var wrongSerializer = provider.CreateState();
        ((IEntitySyncedDataAnimationState)wrongSerializer).OnSyncedData(16, 8, 1);
        AssertVec(new Vector3(MathF.PI / 20f, 0f, 0f), Apply(wrongSerializer, provider, Frame(ageInTicks: 0f)).Get("body").Rotation);

        // The right serializer at the wrong index is ignored too.
        var wrongIndex = provider.CreateState();
        ((IEntitySyncedDataAnimationState)wrongIndex).OnSyncedData(0, 0, 1);
        AssertVec(new Vector3(MathF.PI / 20f, 0f, 0f), Apply(wrongIndex, provider, Frame(ageInTicks: 0f)).Get("body").Rotation);
    }

    [Fact]
    public void ApplyClipReconstructsIdleAndCharging()
    {
        var provider = new VexAnimationProvider(TypeId);
        var state = provider.CreateState();
        var frame = Frame(ageInTicks: 3f, pitch: 10f, yaw: 20f);

        var idle = new EntityPose();
        provider.ApplyClip(state, frame, "idle", 0.5f, idle);
        AssertVec(new Vector3(MathF.PI / 20f, 0f, 0f), idle.Get("body").Rotation);
        Assert.Equal(1.0995574f + WingFlap(10f), idle.Get("left_wing").Rotation.Y, 4);

        var charging = new EntityPose();
        provider.ApplyClip(state, frame, "charging", 0f, charging);
        AssertVec(Vector3.Zero, charging.Get("body").Rotation);
        var bob = 0.1f;
        AssertVec(new Vector3(-1.2217305f, MathF.PI / 12f, -0.47123888f - bob), charging.Get("right_arm").Rotation);

        var unknown = new EntityPose();
        provider.ApplyClip(state, frame, "not_a_clip", 1f, unknown);
        Assert.Empty(unknown.Offsets);
    }

    [Fact]
    public void EveryAnimatedPartExistsInTheVexModel()
    {
        var modelParts = VexModelDefinition.Definition.Parts
            .Select(part => part.Name)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var partName in new[] { "head", "body", "right_arm", "left_arm", "left_wing", "right_wing" })
        {
            Assert.Contains(partName, modelParts);
        }
    }
}
