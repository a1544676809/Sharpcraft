// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Illager animation ported from <c>IllagerModel.setupAnim</c> plus the per-subclass <c>getArmPose</c>. Covers
/// walk/riding limbs, head look, each arm-pose branch and selection (from synced flags + equipment), the crossed vs
/// separate arm visibility mapping, clip reconstruction and part coverage.
/// </summary>
public sealed class IllagerAnimationTests
{
    private const float ToRad = MathF.PI / 180f;

    private static EntityAnimationFrame Frame(
        int typeId,
        float pitch = 0f,
        float yaw = 0f,
        float walkPos = 0f,
        float walkSpeed = 0f,
        float age = 0f,
        float swing = 0f,
        bool aggressive = false,
        int mainHandItemId = -1,
        bool hasMainHand = false,
        bool passenger = false) => new(
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
        SwingAnimation: swing,
        IsAggressive: aggressive,
        MainHandItemId: mainHandItemId,
        HasMainHandItem: hasMainHand,
        IsPassenger: passenger);

    private static EntityPose Apply(IllagerAnimationProvider provider, IEntityAnimationState state, in EntityAnimationFrame frame)
    {
        var pose = new EntityPose();
        provider.Apply(state, in frame, pose);
        return pose;
    }

    private static int ArmPoseOf(IEntityAnimationState state) => ((IEntityArmPoseAnimationState)state).ArmPose;

    private static void AssertVec(Vector3 expected, Vector3 actual, int precision = 4)
    {
        Assert.Equal(expected.X, actual.X, precision);
        Assert.Equal(expected.Y, actual.Y, precision);
        Assert.Equal(expected.Z, actual.Z, precision);
    }

    [Fact]
    public void ProviderAdvertisesTheClipsAndTypeId()
    {
        var provider = new IllagerAnimationProvider(IllagerModelDefinition.VindicatorEntityTypeId);
        Assert.Equal(IllagerModelDefinition.VindicatorEntityTypeId, provider.TypeId);
        Assert.Equal(
            new[] { "crossed", "attacking", "spellcasting", "bow", "crossbow_hold", "crossbow_charge", "celebrating", "neutral" },
            provider.Clips);
        Assert.Equal(0, (int)IllagerArmPose.Crossed);
        Assert.NotNull(provider.CreateState());
        Assert.True(IllagerAnimationProvider.CrossbowItemId > 0, "crossbow item id should resolve from the item registry");
    }

    [Fact]
    public void HeadLookAndWalkLimbsMatchTheReference()
    {
        var provider = new IllagerAnimationProvider(IllagerModelDefinition.PillagerEntityTypeId);
        var state = provider.CreateState();
        var pose = Apply(provider, state, Frame(IllagerModelDefinition.PillagerEntityTypeId, pitch: 15f, yaw: -30f, walkPos: 0f, walkSpeed: 1f));

        Assert.Equal(15f * ToRad, pose.Get("head").Rotation.X, 5);
        Assert.Equal(-30f * ToRad, pose.Get("head").Rotation.Y, 5);

        // NEUTRAL: baseline walk limbs. right_leg +0.7, left_leg -0.7; right_arm -1, left_arm +1 (2*1*0.5).
        Assert.Equal(0.7f, pose.Get("right_leg").Rotation.X, 5);
        Assert.Equal(-0.7f, pose.Get("left_leg").Rotation.X, 5);
        Assert.Equal(-1f, pose.Get("right_arm").Rotation.X, 5);
        Assert.Equal(1f, pose.Get("left_arm").Rotation.X, 5);
        Assert.Equal((int)IllagerArmPose.Neutral, ArmPoseOf(state));
    }

    [Fact]
    public void RidingUsesTheSeatedArmAndLegPose()
    {
        var provider = new IllagerAnimationProvider(IllagerModelDefinition.VindicatorEntityTypeId);
        var pose = Apply(provider, provider.CreateState(), Frame(IllagerModelDefinition.VindicatorEntityTypeId, passenger: true));

        Assert.Equal(-MathF.PI / 5f, pose.Get("right_arm").Rotation.X, 5);
        Assert.Equal(-MathF.PI / 5f, pose.Get("left_arm").Rotation.X, 5);
        Assert.Equal(-1.4137167f, pose.Get("right_leg").Rotation.X, 5);
        Assert.Equal(-1.4137167f, pose.Get("left_leg").Rotation.X, 5);
        Assert.Equal(MathF.PI / 10f, pose.Get("right_leg").Rotation.Y, 5);
        Assert.Equal(-MathF.PI / 10f, pose.Get("left_leg").Rotation.Y, 5);
        Assert.Equal(0.07853982f, pose.Get("right_leg").Rotation.Z, 5);
        Assert.Equal(-0.07853982f, pose.Get("left_leg").Rotation.Z, 5);
    }

    [Fact]
    public void ArmPoseSelectionPerSubclass()
    {
        // Vindicator.
        Expect(IllagerModelDefinition.VindicatorEntityTypeId, Frame(IllagerModelDefinition.VindicatorEntityTypeId, aggressive: true), IllagerArmPose.Attacking);
        Expect(IllagerModelDefinition.VindicatorEntityTypeId, Frame(IllagerModelDefinition.VindicatorEntityTypeId), IllagerArmPose.Crossed);
        ExpectSynced(IllagerModelDefinition.VindicatorEntityTypeId, 16, 8, 1, Frame(IllagerModelDefinition.VindicatorEntityTypeId), IllagerArmPose.Celebrating);

        // Evoker.
        Expect(IllagerModelDefinition.EvokerEntityTypeId, Frame(IllagerModelDefinition.EvokerEntityTypeId), IllagerArmPose.Crossed);
        ExpectSynced(IllagerModelDefinition.EvokerEntityTypeId, 17, 0, 1, Frame(IllagerModelDefinition.EvokerEntityTypeId), IllagerArmPose.Spellcasting);

        // Pillager.
        Expect(IllagerModelDefinition.PillagerEntityTypeId, Frame(IllagerModelDefinition.PillagerEntityTypeId), IllagerArmPose.Neutral);
        Expect(IllagerModelDefinition.PillagerEntityTypeId, Frame(IllagerModelDefinition.PillagerEntityTypeId, aggressive: true), IllagerArmPose.Attacking);
        ExpectSynced(IllagerModelDefinition.PillagerEntityTypeId, 16, 8, 1, Frame(IllagerModelDefinition.PillagerEntityTypeId), IllagerArmPose.CrossbowCharge);
        Expect(
            IllagerModelDefinition.PillagerEntityTypeId,
            Frame(IllagerModelDefinition.PillagerEntityTypeId, mainHandItemId: IllagerAnimationProvider.CrossbowItemId, hasMainHand: true),
            IllagerArmPose.CrossbowHold);

        // Illusioner.
        Expect(IllagerModelDefinition.IllusionerEntityTypeId, Frame(IllagerModelDefinition.IllusionerEntityTypeId), IllagerArmPose.Crossed);
        Expect(IllagerModelDefinition.IllusionerEntityTypeId, Frame(IllagerModelDefinition.IllusionerEntityTypeId, aggressive: true), IllagerArmPose.BowAndArrow);
        ExpectSynced(IllagerModelDefinition.IllusionerEntityTypeId, 17, 0, 1, Frame(IllagerModelDefinition.IllusionerEntityTypeId), IllagerArmPose.Spellcasting);
    }

    private static void Expect(int typeId, in EntityAnimationFrame frame, IllagerArmPose expected)
    {
        var provider = new IllagerAnimationProvider(typeId);
        var state = provider.CreateState();
        Apply(provider, state, in frame);
        Assert.Equal((int)expected, ArmPoseOf(state));
    }

    private static void ExpectSynced(int typeId, int index, int serializer, int value, in EntityAnimationFrame frame, IllagerArmPose expected)
    {
        var provider = new IllagerAnimationProvider(typeId);
        var state = provider.CreateState();
        ((IEntitySyncedDataAnimationState)state).OnSyncedData(index, serializer, value);
        Apply(provider, state, in frame);
        Assert.Equal((int)expected, ArmPoseOf(state));
    }

    [Fact]
    public void AttackingEmptyHandedUsesZombieArmsAndBob()
    {
        var provider = new IllagerAnimationProvider(IllagerModelDefinition.PillagerEntityTypeId);
        var pose = Apply(provider, provider.CreateState(), Frame(IllagerModelDefinition.PillagerEntityTypeId, aggressive: true, swing: 0f, age: 0f));

        // swing 0 -> attackY = attackX = 0; xRot = -π/1.5; yRot = 0.1; bob z = 0.1, bob x = 0.
        Assert.Equal(-MathF.PI / 1.5f, pose.Get("right_arm").Rotation.X, 5);
        Assert.Equal(-0.1f, pose.Get("right_arm").Rotation.Y, 5);
        Assert.Equal(0.1f, pose.Get("right_arm").Rotation.Z, 5);
        Assert.Equal(-MathF.PI / 1.5f, pose.Get("left_arm").Rotation.X, 5);
        Assert.Equal(0.1f, pose.Get("left_arm").Rotation.Y, 5);
        Assert.Equal(-0.1f, pose.Get("left_arm").Rotation.Z, 5);
    }

    [Fact]
    public void AttackingWithWeaponSwingsTheRightArmDown()
    {
        var provider = new IllagerAnimationProvider(IllagerModelDefinition.PillagerEntityTypeId);
        var pose = Apply(provider, provider.CreateState(), Frame(
            IllagerModelDefinition.PillagerEntityTypeId, aggressive: true, swing: 0f, age: 0f, hasMainHand: true, mainHandItemId: 999));

        // swing 0 -> attack2 = attack = 0; bob z = 0.1, bob x = 0.
        Assert.Equal(MathF.PI / 20f, pose.Get("right_arm").Rotation.Y, 5);
        Assert.Equal(-MathF.PI / 20f, pose.Get("left_arm").Rotation.Y, 5);
        Assert.Equal(-1.8849558f + 0.15f, pose.Get("right_arm").Rotation.X, 4);
        Assert.Equal(0.5f, pose.Get("left_arm").Rotation.X, 4);
    }

    [Fact]
    public void SpellcastingRaisesBothArmsSideways()
    {
        var provider = new IllagerAnimationProvider(IllagerModelDefinition.EvokerEntityTypeId);
        var state = provider.CreateState();
        ((IEntitySyncedDataAnimationState)state).OnSyncedData(17, 0, 1);
        var pose = Apply(provider, state, Frame(IllagerModelDefinition.EvokerEntityTypeId, age: 0f));

        Assert.Equal(0.25f, pose.Get("right_arm").Rotation.X, 5);
        Assert.Equal(0.25f, pose.Get("left_arm").Rotation.X, 5);
        Assert.Equal(MathF.PI * 3f / 4f, pose.Get("right_arm").Rotation.Z, 5);
        Assert.Equal(-MathF.PI * 3f / 4f, pose.Get("left_arm").Rotation.Z, 5);
    }

    [Fact]
    public void BowAndCrossbowPosesTrackTheHead()
    {
        var provider = new IllagerAnimationProvider(IllagerModelDefinition.IllusionerEntityTypeId);
        var bow = Apply(provider, provider.CreateState(), Frame(IllagerModelDefinition.IllusionerEntityTypeId, pitch: 15f, yaw: -30f, aggressive: true));
        var hp = 15f * ToRad;
        var hy = -30f * ToRad;
        Assert.Equal(-0.1f + hy, bow.Get("right_arm").Rotation.Y, 5);
        Assert.Equal((-MathF.PI / 2f) + hp, bow.Get("right_arm").Rotation.X, 5);
        Assert.Equal(-0.9424779f + hp, bow.Get("left_arm").Rotation.X, 5);
        Assert.Equal(hy - 0.4f, bow.Get("left_arm").Rotation.Y, 5);
        Assert.Equal(MathF.PI / 2f, bow.Get("left_arm").Rotation.Z, 5);

        var pillager = new IllagerAnimationProvider(IllagerModelDefinition.PillagerEntityTypeId);
        var hold = Apply(pillager, pillager.CreateState(), Frame(
            IllagerModelDefinition.PillagerEntityTypeId, pitch: 15f, yaw: -30f, mainHandItemId: IllagerAnimationProvider.CrossbowItemId, hasMainHand: true));
        Assert.Equal(-0.3f + hy, hold.Get("right_arm").Rotation.Y, 5);
        Assert.Equal(0.6f + hy, hold.Get("left_arm").Rotation.Y, 5);
        Assert.Equal((-MathF.PI / 2f) + hp + 0.1f, hold.Get("right_arm").Rotation.X, 5);
        Assert.Equal(-1.5f + hp, hold.Get("left_arm").Rotation.X, 5);
    }

    [Fact]
    public void CrossbowChargeUsesTheDocumentedDrawStartPose()
    {
        // Charge progress is blocked (no ticksUsingItem on the frame); the draw-start pose is reproduced.
        var provider = new IllagerAnimationProvider(IllagerModelDefinition.PillagerEntityTypeId);
        var state = provider.CreateState();
        ((IEntitySyncedDataAnimationState)state).OnSyncedData(16, 8, 1);
        var pose = Apply(provider, state, Frame(IllagerModelDefinition.PillagerEntityTypeId));

        Assert.Equal((int)IllagerArmPose.CrossbowCharge, ArmPoseOf(state));
        Assert.Equal(-0.8f, pose.Get("right_arm").Rotation.Y, 5);
        Assert.Equal(-0.97079635f, pose.Get("right_arm").Rotation.X, 5);
        Assert.Equal(-0.97079635f, pose.Get("left_arm").Rotation.X, 5);
        Assert.Equal(0.4f, pose.Get("left_arm").Rotation.Y, 5);
    }

    [Fact]
    public void CelebratingThrowsTheArmsUp()
    {
        var provider = new IllagerAnimationProvider(IllagerModelDefinition.VindicatorEntityTypeId);
        var state = provider.CreateState();
        ((IEntitySyncedDataAnimationState)state).OnSyncedData(16, 8, 1);
        var pose = Apply(provider, state, Frame(IllagerModelDefinition.VindicatorEntityTypeId, age: 0f));

        Assert.Equal(2.670354f, pose.Get("right_arm").Rotation.Z, 4);
        Assert.Equal(-MathF.PI * 3f / 4f, pose.Get("left_arm").Rotation.Z, 5);
        Assert.Equal(0.05f, pose.Get("right_arm").Rotation.X, 5);
        Assert.Equal(0.05f, pose.Get("left_arm").Rotation.X, 5);
    }

    [Fact]
    public void ArmPoseOrdinalsDriveCrossedVersusSeparateVisibility()
    {
        var model = IllagerModelDefinition.Vindicator;
        var armsRight = model.Parts.First(part => part.Name == "arms_right").VisibleWhen!;
        var rightArm = model.Parts.First(part => part.Name == "right_arm").VisibleWhen!;

        var crossed = new EntityRenderState(Id: 1, TypeId: IllagerModelDefinition.VindicatorEntityTypeId, X: 0f, Y: 0f, Z: 0f, Yaw: 0f, Pitch: 0f, ArmPose: (int)IllagerArmPose.Crossed);
        var attacking = crossed with { ArmPose = (int)IllagerArmPose.Attacking };

        Assert.True(armsRight(crossed));
        Assert.False(rightArm(crossed));
        Assert.False(armsRight(attacking));
        Assert.True(rightArm(attacking));
    }

    [Fact]
    public void ApplyClipReconstructsEveryPose()
    {
        var provider = new IllagerAnimationProvider(IllagerModelDefinition.PillagerEntityTypeId);
        var state = provider.CreateState();
        var frame = Frame(IllagerModelDefinition.PillagerEntityTypeId);
        var pose = new EntityPose();

        provider.ApplyClip(state, in frame, "spellcasting", 0f, pose);
        Assert.Equal(MathF.PI * 3f / 4f, pose.Get("right_arm").Rotation.Z, 5);

        pose.Clear();
        provider.ApplyClip(state, in frame, "celebrating", 0f, pose);
        Assert.Equal(2.670354f, pose.Get("right_arm").Rotation.Z, 4);

        pose.Clear();
        provider.ApplyClip(state, in frame, "neutral", 0f, pose);
        Assert.Equal(0f, pose.Get("right_arm").Rotation.X, 5);
    }

    [Fact]
    public void EveryAnimatedPartExistsInAllIllagerModels()
    {
        var expected = new[]
        {
            "head", "nose", "body", "body_outer", "arms_right", "arms_cross", "arms_left",
            "right_arm", "left_arm", "right_leg", "left_leg"
        };
        var provider = new IllagerAnimationProvider(IllagerModelDefinition.VindicatorEntityTypeId);
        var pose = Apply(provider, provider.CreateState(), Frame(IllagerModelDefinition.VindicatorEntityTypeId, walkSpeed: 1f));

        foreach (var definition in new[]
                 {
                     IllagerModelDefinition.Vindicator, IllagerModelDefinition.Evoker,
                     IllagerModelDefinition.Pillager, IllagerModelDefinition.Illusioner
                 })
        {
            var parts = definition.Parts.Select(part => part.Name).ToHashSet(StringComparer.Ordinal);
            foreach (var name in expected)
            {
                Assert.Contains(name, parts);
            }
        }

        var vindicatorParts = IllagerModelDefinition.Vindicator.Parts.Select(part => part.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var partName in pose.Offsets.Keys)
        {
            Assert.Contains(partName, vindicatorParts);
        }
    }
}
