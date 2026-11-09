// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Registries;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Piglin animation ported from <c>PiglinModel.setupAnim</c>: base head/ears/walk limbs plus DANCING, CROSSBOW_CHARGE,
/// ADMIRING_ITEM (piglin_loved tag) and ATTACKING_WITH_MELEE_WEAPON (piglin_preferred_weapons tag). CROSSBOW_HOLD stays
/// blocked on the charged item-data state.
/// </summary>
[Collection(NonParallelCollection.Name)]
public sealed class PiglinAnimationTests
{
    private const float ToRad = MathF.PI / 180f;

    private static EntityAnimationFrame Frame(
        int typeId,
        float pitch = 0f,
        float yaw = 0f,
        float walkPos = 0f,
        float walkSpeed = 0f,
        float age = 0f,
        bool aggressive = false,
        int mainHandItemId = -1,
        bool hasMainHand = false,
        bool passenger = false,
        int ticksUsingItem = 0,
        float partialTick = 0f,
        int offHandItemId = -1,
        bool chargedCrossbow = false,
        int quickChargeLevel = 0,
        bool leftHanded = false) => new(
        TypeId: typeId,
        AgeInTicks: age,
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
        PartialTick: partialTick,
        IsAggressive: aggressive,
        MainHandItemId: mainHandItemId,
        HasMainHandItem: hasMainHand,
        IsPassenger: passenger,
        // The reference exposes ticksUsingItem(partialTicks) = ticks + partialTicks, so the render state already
        // carries the partial tick; providers must not add it again.
        TicksUsingItem: ticksUsingItem + partialTick,
        OffHandItemId: offHandItemId,
        MainHandChargedCrossbow: chargedCrossbow,
        MainHandQuickChargeLevel: quickChargeLevel,
        IsLeftHanded: leftHanded);

    private static EntityPose Apply(PiglinAnimationProvider provider, IEntityAnimationState state, in EntityAnimationFrame frame)
    {
        var pose = new EntityPose();
        provider.Apply(state, in frame, pose);
        return pose;
    }

    private static int ArmPoseOf(IEntityAnimationState state) => ((IEntityArmPoseAnimationState)state).ArmPose;

    private static float Lerp(float t, float a, float b) => a + ((b - a) * t);

    [Fact]
    public void ProviderAdvertisesClipsAndTypeId()
    {
        var provider = new PiglinAnimationProvider(PiglinModelDefinition.PiglinEntityTypeId);
        Assert.Equal(PiglinModelDefinition.PiglinEntityTypeId, provider.TypeId);
        Assert.Equal(new[] { "default", "dancing", "crossbow_charge" }, provider.Clips);
        Assert.Equal(4, (int)PiglinArmPose.Dancing);
        Assert.Equal(5, (int)PiglinArmPose.Default);
        Assert.NotNull(provider.CreateState());
    }

    [Fact]
    public void BaseWalkHeadAndEarsMatchTheReference()
    {
        var provider = new PiglinAnimationProvider(PiglinModelDefinition.PiglinEntityTypeId);
        var pose = Apply(provider, provider.CreateState(), Frame(PiglinModelDefinition.PiglinEntityTypeId, pitch: 15f, yaw: -30f, walkSpeed: 1f));

        Assert.Equal(15f * ToRad, pose.Get("head").Rotation.X, 5);
        Assert.Equal(-30f * ToRad, pose.Get("head").Rotation.Y, 5);

        // Arms: cos(phase+π) / cos(phase) * 2 * speed * 0.5; phase 0.
        Assert.Equal(-1f, pose.Get("right_arm").Rotation.X, 5);
        Assert.Equal(1f, pose.Get("left_arm").Rotation.X, 5);
        // Legs: cos * 1.4 * speed.
        Assert.Equal(1.4f, pose.Get("right_leg").Rotation.X, 5);
        Assert.Equal(-1.4f, pose.Get("left_leg").Rotation.X, 5);
        Assert.Equal(0.005f, pose.Get("right_leg").Rotation.Y, 5);
        Assert.Equal(-0.005f, pose.Get("left_leg").Rotation.Y, 5);

        // Ears: amplitude = 0.08 + speed*0.4 = 0.48, freq = 0 -> left -0.48, right +0.48 (adult bind 30°).
        Assert.Equal(-0.48f, pose.Get("left_ear").Rotation.Z, 5);
        Assert.Equal(0.48f, pose.Get("right_ear").Rotation.Z, 5);
    }

    [Fact]
    public void DancingAppliesTheDancePose()
    {
        var provider = new PiglinAnimationProvider(PiglinModelDefinition.PiglinEntityTypeId);
        var state = provider.CreateState();
        ((IEntitySyncedDataAnimationState)state).OnSyncedData(19, 8, 1);
        var pose = Apply(provider, state, Frame(PiglinModelDefinition.PiglinEntityTypeId, age: 0f));

        Assert.Equal((int)PiglinArmPose.Dancing, ArmPoseOf(state));

        // age 0: dancePos 0 -> head x 0, y -(sin0 + 0.4) = -0.4; body y 0.
        Assert.Equal(0f, pose.Get("head").Position.X, 6);
        Assert.Equal(-0.4f, pose.Get("head").Position.Y, 6);
        Assert.Equal(0f, pose.Get("body").Position.Y, 6);

        // arm zRot = deg * (70 + cos0*10) = 80°; left mirrored.
        Assert.Equal(80f * ToRad, pose.Get("right_arm").Rotation.Z, 5);
        Assert.Equal(-80f * ToRad, pose.Get("left_arm").Rotation.Z, 5);
        // arm y positions: right -(0.5*0 - 0.5) = +0.5, left -(0 + 0.5) = -0.5.
        Assert.Equal(0.5f, pose.Get("right_arm").Position.Y, 6);
        Assert.Equal(-0.5f, pose.Get("left_arm").Position.Y, 6);
        // ears: right 0, left -deg*10.
        Assert.Equal(0f, pose.Get("right_ear").Rotation.Z, 5);
        Assert.Equal(-(10f * ToRad), pose.Get("left_ear").Rotation.Z, 5);
    }

    [Fact]
    public void CrossbowChargeProgressFollowsTicksUsingItem()
    {
        var provider = new PiglinAnimationProvider(PiglinModelDefinition.PiglinEntityTypeId);

        var startState = provider.CreateState();
        ((IEntitySyncedDataAnimationState)startState).OnSyncedData(18, 8, 1);
        var start = Apply(provider, startState, Frame(PiglinModelDefinition.PiglinEntityTypeId));
        Assert.Equal((int)PiglinArmPose.CrossbowCharge, ArmPoseOf(startState));
        Assert.Equal(-0.97079635f, start.Get("right_arm").Rotation.X, 5);
        Assert.Equal(-0.8f, start.Get("right_arm").Rotation.Y, 5);
        Assert.Equal(Lerp(0f, -0.97079635f, -MathF.PI / 2f), start.Get("left_arm").Rotation.X, 5);
        Assert.Equal(Lerp(0f, 0.4f, 0.85f), start.Get("left_arm").Rotation.Y, 5);

        var midState = provider.CreateState();
        ((IEntitySyncedDataAnimationState)midState).OnSyncedData(18, 8, 1);
        var mid = Apply(provider, midState, Frame(PiglinModelDefinition.PiglinEntityTypeId, ticksUsingItem: 12, partialTick: 0.5f));
        Assert.Equal(Lerp(0.5f, -0.97079635f, -MathF.PI / 2f), mid.Get("left_arm").Rotation.X, 5);
        Assert.Equal(Lerp(0.5f, 0.4f, 0.85f), mid.Get("left_arm").Rotation.Y, 5);

        var fullState = provider.CreateState();
        ((IEntitySyncedDataAnimationState)fullState).OnSyncedData(18, 8, 1);
        var full = Apply(provider, fullState, Frame(PiglinModelDefinition.PiglinEntityTypeId, ticksUsingItem: 25, partialTick: 0.9f));
        Assert.Equal(-MathF.PI / 2f, full.Get("left_arm").Rotation.X, 5);
        Assert.Equal(0.85f, full.Get("left_arm").Rotation.Y, 5);
    }

    [Fact]
    public void QuickChargeShortensChargeDuration()
    {
        var provider = new PiglinAnimationProvider(PiglinModelDefinition.PiglinEntityTypeId);
        var state = provider.CreateState();
        ((IEntitySyncedDataAnimationState)state).OnSyncedData(18, 8, 1);

        // Level II: maxCharge = 25 - 10 = 15, so 10 ticks is 2/3 of the pull.
        var pose = Apply(provider, state, Frame(PiglinModelDefinition.PiglinEntityTypeId, ticksUsingItem: 10, quickChargeLevel: 2));

        Assert.Equal(Lerp(2f / 3f, -0.97079635f, -MathF.PI / 2f), pose.Get("left_arm").Rotation.X, 5);
        Assert.Equal(Lerp(2f / 3f, 0.4f, 0.85f), pose.Get("left_arm").Rotation.Y, 5);
    }

    [Fact]
    public void BlockedPosesFallBackToDefault()
    {
        var provider = new PiglinAnimationProvider(PiglinModelDefinition.PiglinEntityTypeId);
        var state = provider.CreateState();
        // Aggressive with a melee item would be ATTACKING_WITH_MELEE_WEAPON (blocked) -> DEFAULT, no fake swing.
        var pose = Apply(provider, state, Frame(PiglinModelDefinition.PiglinEntityTypeId, walkSpeed: 1f, aggressive: true, hasMainHand: true, mainHandItemId: 999));

        Assert.Equal((int)PiglinArmPose.Default, ArmPoseOf(state));
        // DEFAULT keeps only the base arm walk rotations (no swing override, no z-rot).
        Assert.Equal(0f, pose.Get("right_arm").Rotation.Z, 5);
        Assert.Equal(-1f, pose.Get("right_arm").Rotation.X, 5);
    }

    [Fact]
    public void DancingTakesPriorityOverCharging()
    {
        var provider = new PiglinAnimationProvider(PiglinModelDefinition.PiglinEntityTypeId);
        var state = provider.CreateState();
        var synced = (IEntitySyncedDataAnimationState)state;
        synced.OnSyncedData(18, 8, 1);
        synced.OnSyncedData(19, 8, 1);
        Apply(provider, state, Frame(PiglinModelDefinition.PiglinEntityTypeId));
        Assert.Equal((int)PiglinArmPose.Dancing, ArmPoseOf(state));
    }

    [Fact]
    public void ApplyClipReconstructsEveryPose()
    {
        var provider = new PiglinAnimationProvider(PiglinModelDefinition.PiglinEntityTypeId);
        var state = provider.CreateState();
        var frame = Frame(PiglinModelDefinition.PiglinEntityTypeId, age: 0f);
        var pose = new EntityPose();

        provider.ApplyClip(state, in frame, "dancing", 0f, pose);
        Assert.Equal(80f * ToRad, pose.Get("right_arm").Rotation.Z, 5);

        pose.Clear();
        provider.ApplyClip(state, in frame, "crossbow_charge", 0f, pose);
        Assert.Equal(-0.97079635f, pose.Get("right_arm").Rotation.X, 5);

        pose.Clear();
        provider.ApplyClip(state, in frame, "default", 0f, pose);
        Assert.Equal(0f, pose.Get("right_arm").Rotation.Z, 5);
    }

    [Fact]
    public void AdmiringAndMeleePosesComeFromItemTags()
    {
        var items = EntityModelResolver.Items!;
        var gold = items.GetId(Identifier.Parse("minecraft:gold_ingot"));
        var sword = items.GetId(Identifier.Parse("minecraft:golden_sword"));
        Assert.True(gold >= 0);
        Assert.True(sword >= 0);

        var previous = EntityModelResolver.ItemTags;
        EntityModelResolver.ItemTags = ItemTagRegistry.Parse(
        [
            (Identifier.Parse("minecraft:piglin_loved"), """{"values":["minecraft:gold_ingot"]}"""),
            (Identifier.Parse("minecraft:piglin_preferred_weapons"), """{"values":["minecraft:golden_sword"]}""")
        ]);

        try
        {
            var provider = new PiglinAnimationProvider(PiglinModelDefinition.PiglinEntityTypeId);
            var state = provider.CreateState();

            var admiring = Apply(provider, state, Frame(PiglinModelDefinition.PiglinEntityTypeId, offHandItemId: gold));
            Assert.Equal(0.5f, admiring.Get("head").Rotation.X, 5);
            Assert.Equal(0.5f, admiring.Get("left_arm").Rotation.Y, 5);
            Assert.Equal(-0.9f, admiring.Get("left_arm").Rotation.X, 5);

            var leftHanded = Apply(provider, state, Frame(PiglinModelDefinition.PiglinEntityTypeId, offHandItemId: gold, leftHanded: true));
            Assert.Equal(-0.5f, leftHanded.Get("right_arm").Rotation.Y, 5);
            Assert.Equal(-0.9f, leftHanded.Get("right_arm").Rotation.X, 5);
            Assert.Equal(0f, leftHanded.Get("left_arm").Rotation.X, 5);

            var melee = Apply(provider, state, Frame(PiglinModelDefinition.PiglinEntityTypeId, aggressive: true, mainHandItemId: sword, hasMainHand: true));
            Assert.Equal(-1.8f, melee.Get("right_arm").Rotation.X, 5);
        }
        finally
        {
            EntityModelResolver.ItemTags = previous;
        }
    }

    [Fact]
    public void ChargedCrossbowSelectsCrossbowHoldPose()
    {
        var provider = new PiglinAnimationProvider(PiglinModelDefinition.PiglinEntityTypeId);
        var pose = Apply(provider, provider.CreateState(), Frame(PiglinModelDefinition.PiglinEntityTypeId, chargedCrossbow: true));

        Assert.Equal(-0.3f, pose.Get("right_arm").Rotation.Y, 5);
        Assert.Equal((-MathF.PI / 2f) + 0.1f, pose.Get("right_arm").Rotation.X, 5);
        Assert.Equal(0.6f, pose.Get("left_arm").Rotation.Y, 5);
        Assert.Equal(-1.5f, pose.Get("left_arm").Rotation.X, 5);
    }

    [Fact]
    public void EveryAnimatedPartExistsInThePiglinModels()
    {
        var expected = new[] { "head", "left_ear", "right_ear", "body", "right_arm", "left_arm", "right_leg", "left_leg" };
        var provider = new PiglinAnimationProvider(PiglinModelDefinition.PiglinEntityTypeId);
        var pose = Apply(provider, provider.CreateState(), Frame(PiglinModelDefinition.PiglinEntityTypeId, walkSpeed: 1f));

        foreach (var definition in new[] { PiglinModelDefinition.Definition, PiglinModelDefinition.Brute, PiglinModelDefinition.Zombified })
        {
            var parts = definition.Parts.Select(part => part.Name).ToHashSet(StringComparer.Ordinal);
            foreach (var name in expected)
            {
                Assert.Contains(name, parts);
            }
        }

        var modelParts = PiglinModelDefinition.Definition.Parts.Select(part => part.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var partName in pose.Offsets.Keys)
        {
            Assert.Contains(partName, modelParts);
        }
    }
}
