// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Allay animation ported from <c>AllayModel.setupAnim</c> plus the client derivations (<c>Allay.aiStep</c> counters and
/// the DATA_DANCING synced flag). Covers the wing flap, body tilt, arm idle/holding poses, dance/spin routing via the
/// state capabilities, clip reconstruction and part coverage.
/// </summary>
public sealed class AllayAnimationTests
{
    private const float ToRad = MathF.PI / 180f;

    private static EntityAnimationFrame Frame(
        float age = 0f,
        float pitch = 0f,
        float yaw = 0f,
        float walkPos = 0f,
        float walkSpeed = 0f,
        bool hasMainHand = false,
        float partialTick = 0f) => new(
        TypeId: AllayModelDefinition.AllayEntityTypeId,
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
        HasMainHandItem: hasMainHand);

    private static EntityPose Apply(AllayAnimationProvider provider, IEntityAnimationState state, in EntityAnimationFrame frame)
    {
        var pose = new EntityPose();
        provider.Apply(state, in frame, pose);
        return pose;
    }

    private static void AddTicks(IEntityAnimationState state, int count)
    {
        var tick = (IEntityTickAnimationState)state;
        for (var i = 0; i < count; i++)
        {
            tick.OnTick(new EntityAnimationTickContext(i, 0f));
        }
    }

    [Fact]
    public void ProviderAdvertisesTheThreeClipsAndTypeId()
    {
        var provider = new AllayAnimationProvider(AllayModelDefinition.AllayEntityTypeId);
        Assert.Equal(AllayModelDefinition.AllayEntityTypeId, provider.TypeId);
        Assert.Equal(new[] { "idle", "dance", "spin" }, provider.Clips);
        Assert.NotNull(provider.CreateState());
    }

    [Fact]
    public void IdlePoseSetsLookWingFlapAndArmBob()
    {
        var provider = new AllayAnimationProvider(AllayModelDefinition.AllayEntityTypeId);
        var pose = Apply(provider, provider.CreateState(), Frame(age: 0f, pitch: 15f, yaw: -30f));

        Assert.Equal(15f * ToRad, pose.Get("head").Rotation.X, 5);
        Assert.Equal(-30f * ToRad, pose.Get("head").Rotation.Y, 5);

        // flapAmount = cos(0) * π * 0.15 + 0 = 0.15π.
        var flapAmount = MathF.PI * 0.15f;
        Assert.Equal(0.43633232f, pose.Get("right_wing").Rotation.X, 5);
        Assert.Equal((-MathF.PI / 4f) + flapAmount, pose.Get("right_wing").Rotation.Y, 5);
        Assert.Equal((MathF.PI / 4f) - flapAmount, pose.Get("left_wing").Rotation.Y, 5);

        // Not flying: body upright, idle bob at cos(0) = 1 -> root y offset -0.25 (y-up).
        Assert.Equal(0f, pose.Get("body").Rotation.X, 5);
        Assert.Equal(-0.25f, pose.Get("root").Position.Y, 5);

        // Idle arms: no pitch, no yaw, z = ±0.43633232 (the bob term vanishes at idleBobSpeed 0).
        Assert.Equal(0f, pose.Get("right_arm").Rotation.X, 5);
        Assert.Equal(0f, pose.Get("right_arm").Rotation.Y, 5);
        Assert.Equal(0.43633232f, pose.Get("right_arm").Rotation.Z, 5);
        Assert.Equal(-0.43633232f, pose.Get("left_arm").Rotation.Z, 5);
    }

    [Fact]
    public void WingFlapFollowsTheCosineOfTheFlapSpeed()
    {
        var provider = new AllayAnimationProvider(AllayModelDefinition.AllayEntityTypeId);
        // age * 20 * deg2rad = π/2 -> cos = 0 -> flapAmount = walkSpeed = 0.
        var pose = Apply(provider, provider.CreateState(), Frame(age: 4.5f));

        Assert.Equal(-MathF.PI / 4f, pose.Get("right_wing").Rotation.Y, 3);
        Assert.Equal(MathF.PI / 4f, pose.Get("left_wing").Rotation.Y, 3);
    }

    [Fact]
    public void FlyingFactorTiltsTheBodyAndFlattensWings()
    {
        var provider = new AllayAnimationProvider(AllayModelDefinition.AllayEntityTypeId);
        // walkSpeed 0.15 -> flyingFactor = min(0.15 / 0.3, 1) = 0.5.
        var pose = Apply(provider, provider.CreateState(), Frame(age: 0f, walkSpeed: 0.15f));

        Assert.Equal(MathF.PI / 8f, pose.Get("body").Rotation.X, 5);
        Assert.Equal(0.43633232f * 0.5f, pose.Get("right_wing").Rotation.X, 5);
        Assert.Equal(0.43633232f * 0.5f, pose.Get("left_wing").Rotation.X, 5);
        // idleBobFactor = 0.5 -> root y offset = -1 * 0.25 * 0.5.
        Assert.Equal(-0.125f, pose.Get("root").Position.Y, 5);
    }

    [Fact]
    public void DancingDrivesTheSwayAndSpinFromStateCounters()
    {
        var provider = new AllayAnimationProvider(AllayModelDefinition.AllayEntityTypeId);
        var state = provider.CreateState();
        ((IEntitySyncedDataAnimationState)state).OnSyncedData(AllayAnimationProvider.DancingDataIndex, AllayAnimationProvider.BooleanSerializerId, 1);
        AddTicks(state, 2);

        var pose = Apply(provider, state, Frame(age: 0f, walkSpeed: 0f));

        // After 2 ticks: spinning ticks 1 -> 2, so progress = lerp(0, 1, 2) / 15 = 1/15; still in the spin window.
        var spinProgress = 1f / 15f;
        var sway = 1f - spinProgress;

        Assert.Equal(0f, pose.Get("head").Rotation.X, 5);
        Assert.Equal(30f * ToRad * sway, pose.Get("head").Rotation.Y, 4);
        Assert.Equal(14f * ToRad * sway, pose.Get("head").Rotation.Z, 4);
        Assert.Equal(MathF.PI * 4f * spinProgress, pose.Get("root").Rotation.Y, 4);
        Assert.Equal(16f * ToRad * sway, pose.Get("root").Rotation.Z, 4);
    }

    [Fact]
    public void DancingWithoutSpinningKeepsTheSwayButNoRootYaw()
    {
        var provider = new AllayAnimationProvider(AllayModelDefinition.AllayEntityTypeId);
        var state = provider.CreateState();
        ((IEntitySyncedDataAnimationState)state).OnSyncedData(AllayAnimationProvider.DancingDataIndex, AllayAnimationProvider.BooleanSerializerId, 1);
        // 15 ticks puts dancingTicks at 15 -> 15 % 55 < 15 is false, so the allay stops spinning.
        AddTicks(state, 15);

        var pose = Apply(provider, state, Frame(age: 0f, walkSpeed: 0f));

        // spinning ticks 14 -> 13 -> progress = 14/15.
        var spinProgress = 14f / 15f;
        Assert.Equal(0f, pose.Get("root").Rotation.Y, 5);
        Assert.Equal(16f * ToRad * (1f - spinProgress), pose.Get("root").Rotation.Z, 4);
        Assert.Equal(30f * ToRad * (1f - spinProgress), pose.Get("head").Rotation.Y, 4);
    }

    [Fact]
    public void HoldingAnItemFoldsTheArmsUp()
    {
        var provider = new AllayAnimationProvider(AllayModelDefinition.AllayEntityTypeId);
        var state = provider.CreateState();

        // The item flag reaches the state through Apply; then the holding counter climbs to its cap of 5.
        Apply(provider, state, Frame(age: 0f, hasMainHand: true));
        AddTicks(state, 6);

        var pose = Apply(provider, state, Frame(age: 0f, hasMainHand: true));

        // holdingFactor = 1, flyingFactor = 0 -> arm pitch = max(lerp(0, -π/3, -1.134464)).
        Assert.Equal(-MathF.PI / 3f, pose.Get("right_arm").Rotation.X, 4);
        Assert.Equal(-MathF.PI / 3f, pose.Get("left_arm").Rotation.X, 4);
        Assert.Equal(0.27925268f, pose.Get("right_arm").Rotation.Y, 4);
        Assert.Equal(-0.27925268f, pose.Get("left_arm").Rotation.Y, 4);
        // The idle bob term is zeroed by holdingFactor 1, leaving the bare baseline.
        Assert.Equal(0.43633232f, pose.Get("right_arm").Rotation.Z, 5);
    }

    [Fact]
    public void ApplyClipReconstructsIdleDanceAndSpin()
    {
        var provider = new AllayAnimationProvider(AllayModelDefinition.AllayEntityTypeId);
        var state = provider.CreateState();
        var frame = Frame(age: 0f);
        var pose = new EntityPose();

        provider.ApplyClip(state, in frame, "dance", 0f, pose);
        Assert.Equal(0f, pose.Get("root").Rotation.Y, 5);
        Assert.Equal(16f * ToRad, pose.Get("root").Rotation.Z, 4);
        Assert.Equal(30f * ToRad, pose.Get("head").Rotation.Y, 4);
        Assert.Equal(14f * ToRad, pose.Get("head").Rotation.Z, 4);

        pose.Clear();
        provider.ApplyClip(state, in frame, "spin", 0f, pose);
        Assert.Equal(2f * MathF.PI, pose.Get("root").Rotation.Y, 4);
        Assert.Equal(8f * ToRad, pose.Get("root").Rotation.Z, 4);
        Assert.Equal(15f * ToRad, pose.Get("head").Rotation.Y, 4);

        pose.Clear();
        provider.ApplyClip(state, in frame, "idle", 0f, pose);
        Assert.Equal(0f, pose.Get("root").Rotation.Y, 5);
        Assert.Equal(0f, pose.Get("root").Rotation.Z, 5);
    }

    [Fact]
    public void EveryAnimatedPartExistsInTheAllayModel()
    {
        var provider = new AllayAnimationProvider(AllayModelDefinition.AllayEntityTypeId);
        var modelParts = AllayModelDefinition.Definition.Parts.Select(part => part.Name).ToHashSet(StringComparer.Ordinal);
        var pose = Apply(provider, provider.CreateState(), Frame(age: 1.7f, walkSpeed: 0.1f));

        Assert.NotEmpty(pose.Offsets);
        foreach (var partName in pose.Offsets.Keys)
        {
            Assert.Contains(partName, modelParts);
        }

        Assert.Contains("root", modelParts);
        Assert.Contains("head", modelParts);
        Assert.Contains("body", modelParts);
        Assert.Contains("right_arm", modelParts);
        Assert.Contains("left_arm", modelParts);
        Assert.Contains("right_wing", modelParts);
        Assert.Contains("left_wing", modelParts);
    }
}
