// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Shulker animation ported from <c>ShulkerModel.setupAnim</c> plus the <c>Shulker.updatePeekAmount</c> clock. Covers the
/// lid height/rotation at closed/half/open peek, the extra term for <c>bs &gt; π</c>, the head look
/// (<c>HeadYaw − 180 − Yaw</c>), synced-byte → peek routing, clip reconstruction and part coverage.
/// </summary>
public sealed class ShulkerAnimationTests
{
    private const float ToRad = MathF.PI / 180f;

    private static EntityAnimationFrame Frame(
        float pitch = 0f,
        float yaw = 0f,
        float headYaw = 0f,
        float age = 0f,
        float partialTick = 0f) => new(
        TypeId: ShulkerModelDefinition.ShulkerEntityTypeId,
        AgeInTicks: age,
        WalkAnimationPos: 0f,
        WalkAnimationSpeed: 0f,
        Pitch: pitch,
        Yaw: yaw,
        Flap: 0f,
        FlapSpeed: 0f,
        TentacleAngle: 0f,
        IsResting: false,
        IsBaby: false,
        IsHidingInShell: false,
        PartialTick: partialTick,
        HeadYaw: headYaw);

    private static float LidY(float peek, float age)
    {
        var bs = (0.5f + peek) * MathF.PI;
        var extra = bs > MathF.PI ? MathF.Sin(age * 0.1f) * 0.7f : 0f;
        return 8f - (MathF.Sin(bs) * 8f) - extra;
    }

    private static void DrivePeek(IEntityAnimationState state, int raw, int ticks)
    {
        ((IEntitySyncedDataAnimationState)state).OnSyncedData(ShulkerAnimationProvider.PeekDataIndex, ShulkerAnimationProvider.ByteSerializerId, raw);
        var tick = (IEntityTickAnimationState)state;
        for (var i = 0; i < ticks; i++)
        {
            tick.OnTick(new EntityAnimationTickContext(i, 0f));
        }
    }

    private static EntityPose ApplyWithState(ShulkerAnimationProvider provider, IEntityAnimationState state, in EntityAnimationFrame frame)
    {
        var pose = new EntityPose();
        provider.Apply(state, in frame, pose);
        return pose;
    }

    [Fact]
    public void ProviderAdvertisesTheTwoClipsAndTypeId()
    {
        var provider = new ShulkerAnimationProvider(ShulkerModelDefinition.ShulkerEntityTypeId);
        Assert.Equal(ShulkerModelDefinition.ShulkerEntityTypeId, provider.TypeId);
        Assert.Equal(new[] { "closed", "open" }, provider.Clips);
        Assert.Equal(17, ShulkerAnimationProvider.PeekDataIndex);
        Assert.Equal(0, ShulkerAnimationProvider.ByteSerializerId);
        Assert.NotNull(provider.CreateState());
    }

    [Fact]
    public void ClosedLidSitsAtBindAndHeadLooksAround()
    {
        var provider = new ShulkerAnimationProvider(ShulkerModelDefinition.ShulkerEntityTypeId);
        var frame = Frame(pitch: 15f, yaw: 30f, headYaw: 200f);
        var pose = new EntityPose();
        provider.ApplyClip(provider.CreateState(), in frame, "closed", 0f, pose);

        // peek 0 -> bs = π/2, sin = 1 -> lid y offset 8 - 8 = 0; no yaw at peek < 0.3.
        Assert.Equal(0f, pose.Get("lid").Position.Y, 5);
        Assert.Equal(0f, pose.Get("lid").Rotation.Y, 6);

        // head: xRot = pitch, yRot = (HeadYaw - 180 - Yaw).
        Assert.Equal(15f * ToRad, pose.Get("head").Rotation.X, 5);
        Assert.Equal((200f - 180f - 30f) * ToRad, pose.Get("head").Rotation.Y, 5);
    }

    [Fact]
    public void HalfPeekRaisesTheLidAndStartsTheYaw()
    {
        var provider = new ShulkerAnimationProvider(ShulkerModelDefinition.ShulkerEntityTypeId);
        var state = provider.CreateState();
        DrivePeek(state, raw: 50, ticks: 11); // current and previous both 0.5
        var pose = ApplyWithState(provider, state, Frame());

        // peek 0.5 -> bs = π, sin = 0 -> lid y = 8; q = -1 -> q^4 = 1 -> yRot = π/8.
        Assert.Equal(8f, pose.Get("lid").Position.Y, 4);
        Assert.Equal(MathF.PI * 0.125f, pose.Get("lid").Rotation.Y, 4);
    }

    [Fact]
    public void FullyOpenLidReachesTheTopAndFullYaw()
    {
        var provider = new ShulkerAnimationProvider(ShulkerModelDefinition.ShulkerEntityTypeId);
        var state = provider.CreateState();
        DrivePeek(state, raw: 100, ticks: 21); // clamps to 1.0
        var pose = ApplyWithState(provider, state, Frame());

        // peek 1 -> bs = 3π/2, sin = -1 -> lid y = 16; q = -2 -> q^4 = 16 -> yRot = 2π.
        Assert.Equal(16f, pose.Get("lid").Position.Y, 4);
        Assert.Equal(2f * MathF.PI, pose.Get("lid").Rotation.Y, 4);
    }

    [Fact]
    public void ExtraWobbleAppliesOnlyAboveBootstrapPi()
    {
        var provider = new ShulkerAnimationProvider(ShulkerModelDefinition.ShulkerEntityTypeId);
        var state = provider.CreateState();
        DrivePeek(state, raw: 75, ticks: 16); // current and previous both 0.75

        var atZero = ApplyWithState(provider, state, Frame(age: 0f));
        var atPeak = ApplyWithState(provider, state, Frame(age: 5f * MathF.PI)); // sin(0.1 * age) = 1 -> extra 0.7

        Assert.Equal(LidY(0.75f, 0f), atZero.Get("lid").Position.Y, 4);
        Assert.Equal(LidY(0.75f, 5f * MathF.PI), atPeak.Get("lid").Position.Y, 4);
        // The wobble shifts the lid by exactly the extra term (0.7).
        Assert.Equal(0.7f, atZero.Get("lid").Position.Y - atPeak.Get("lid").Position.Y, 4);

        // At peek 0.5 (bs == π) the extra term must be zero even at the same age.
        var halfState = provider.CreateState();
        DrivePeek(halfState, raw: 50, ticks: 11);
        var half = ApplyWithState(provider, halfState, Frame(age: 5f * MathF.PI));
        Assert.Equal(8f, half.Get("lid").Position.Y, 4);
    }

    [Fact]
    public void LidYawOnlyActivatesAboveThePointThreeThreshold()
    {
        var provider = new ShulkerAnimationProvider(ShulkerModelDefinition.ShulkerEntityTypeId);

        var lowState = provider.CreateState();
        DrivePeek(lowState, raw: 20, ticks: 5); // peek 0.2
        Assert.Equal(0f, ApplyWithState(provider, lowState, Frame()).Get("lid").Rotation.Y, 6);

        var highState = provider.CreateState();
        DrivePeek(highState, raw: 40, ticks: 9); // peek 0.4
        Assert.NotEqual(0f, ApplyWithState(provider, highState, Frame()).Get("lid").Rotation.Y);
    }

    [Fact]
    public void SyncedByteRoutesThroughThePeekClockAndPartialTick()
    {
        var provider = new ShulkerAnimationProvider(ShulkerModelDefinition.ShulkerEntityTypeId);
        var state = provider.CreateState();
        DrivePeek(state, raw: 100, ticks: 1); // current 0.05, previous 0

        // Partial tick 0 -> previous (0) gives the closed lid, partial tick 1 -> current (0.05).
        Assert.Equal(0f, ApplyWithState(provider, state, Frame(partialTick: 0f)).Get("lid").Position.Y, 6);
        var current = ApplyWithState(provider, state, Frame(partialTick: 1f));
        Assert.Equal(LidY(0.05f, 0f), current.Get("lid").Position.Y, 4);

        // A raw packet of 0 decays the peek back toward zero.
        DrivePeek(state, raw: 0, ticks: 1);
        Assert.True(ApplyWithState(provider, state, Frame(partialTick: 1f)).Get("lid").Position.Y < LidY(0.05f, 0f));
    }

    [Fact]
    public void ApplyClipOpenMatchesPeekOne()
    {
        var provider = new ShulkerAnimationProvider(ShulkerModelDefinition.ShulkerEntityTypeId);
        var frame = Frame();
        var pose = new EntityPose();
        provider.ApplyClip(provider.CreateState(), in frame, "open", 0f, pose);

        Assert.Equal(16f, pose.Get("lid").Position.Y, 4);
        Assert.Equal(2f * MathF.PI, pose.Get("lid").Rotation.Y, 4);
    }

    [Fact]
    public void EveryAnimatedPartExistsInTheShulkerModel()
    {
        var modelParts = ShulkerModelDefinition.Definition.Parts.Select(part => part.Name).ToHashSet(StringComparer.Ordinal);
        var provider = new ShulkerAnimationProvider(ShulkerModelDefinition.ShulkerEntityTypeId);
        var state = provider.CreateState();
        DrivePeek(state, raw: 60, ticks: 13);
        var pose = ApplyWithState(provider, state, Frame());

        Assert.NotEmpty(pose.Offsets);
        foreach (var partName in pose.Offsets.Keys)
        {
            Assert.Contains(partName, modelParts);
        }

        Assert.Contains("lid", modelParts);
        Assert.Contains("head", modelParts);
        Assert.Contains("base", modelParts);
    }
}
