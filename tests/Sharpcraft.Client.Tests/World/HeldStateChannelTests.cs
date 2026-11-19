// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Common.Network;
using Xunit;

namespace Sharpcraft.Client.Tests.World;

/// <summary>task-265: held-item render-state channels, defaults and metadata routing.</summary>
public sealed class HeldStateChannelTests
{
    private static AddedEntity Player(int id) =>
        new(id, Guid.Empty, EntityPlaceholderMesher.PlayerEntityTypeId, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    private static AddedEntity Witch(int id) =>
        new(id, Guid.Empty, WitchModelDefinition.WitchEntityTypeId, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    [Fact]
    public void WitchHeldPotionColorIsCapturedAsStateOnly()
    {
        // B-4 T4 (R-046): PotionContents.custom_color is decoded into ItemComponentInfo.PotionColor and
        // carried to EntityRenderState.HeldPotionColor. This is state capture ONLY - no renderer consumes
        // it yet (held_potion still uses the flat quad). The test must not be read as tint/render coverage.
        var potionId = EntityModelResolver.Items?.GetId(
            Sharpcraft.Common.Identifiers.Identifier.Parse("minecraft:potion")) ?? -1;
        if (potionId <= 0)
        {
            return; // embedded registry unavailable in this host
        }

        const int capturedColor = unchecked((int)0xFF2A7F1E);
        var tracker = new EntityTracker();
        tracker.Add(Witch(5));
        tracker.ApplyEquipment(5, [new EquipmentItem(0, potionId, 1, new ItemComponentInfo(PotionColor: capturedColor))]);
        var state = tracker.Interpolate(0f)[0];

        Assert.True(state.IsHoldingPotion);
        Assert.Equal(capturedColor, state.HeldPotionColor);

        // A non-witch holding the same potion must not carry the potion tint channel.
        tracker.Add(Player(6));
        tracker.ApplyEquipment(6, [new EquipmentItem(0, potionId, 1, new ItemComponentInfo(PotionColor: capturedColor))]);
        var playerState = tracker.Interpolate(0f)[1];
        Assert.False(playerState.IsHoldingPotion);
        Assert.Null(playerState.HeldPotionColor);
    }

    [Fact]
    public void NewChannelsHaveSourceDefaults()
    {
        var state = new EntityRenderState(1, EntityPlaceholderMesher.PlayerEntityTypeId, 0f, 0f, 0f, 0f, 0f);
        Assert.Equal(1, state.MainArm); // HumanoidArm.RIGHT
        Assert.Equal(0, state.LeftTicksUsingItem);
        Assert.Equal(0, state.RightTicksUsingItem);
        Assert.Equal(0f, state.SwingDurationTicks);
        Assert.False(state.IsSwinging);
        Assert.Equal(-1, state.HeldOnHeadItemId);
    }

    [Fact]
    public void PlayerMainArmMetadataRoundTrips()
    {
        var tracker = new EntityTracker();
        tracker.Add(Player(5));
        Assert.True(tracker.ApplyEntityData(5, [new EntityDataItem(15, 42, 0)]));
        var state = tracker.Interpolate(0f)[0];
        Assert.Equal(0, state.MainArm); // LEFT

        Assert.True(tracker.ApplyEntityData(5, [new EntityDataItem(15, 42, 1)]));
        Assert.Equal(1, tracker.Interpolate(0f)[0].MainArm); // RIGHT
    }

    [Fact]
    public void WrongIndexOrSerializerDoesNotWriteMainArm()
    {
        var tracker = new EntityTracker();
        tracker.Add(Player(5));
        Assert.False(tracker.ApplyEntityData(5, [new EntityDataItem(16, 42, 0)]));
        Assert.Equal(1, tracker.Interpolate(0f)[0].MainArm);
        Assert.False(tracker.ApplyEntityData(5, [new EntityDataItem(15, 41, 0)]));
        Assert.Equal(1, tracker.Interpolate(0f)[0].MainArm);
    }

    [Fact]
    public void UsingItemDerivesPerArmTicksAndSpyglassHeldOnHead()
    {
        var spyglassId = EntityModelResolver.Items?.GetId(
            Sharpcraft.Common.Identifiers.Identifier.Parse("minecraft:spyglass")) ?? -1;
        if (spyglassId <= 0)
        {
            return; // embedded registry unavailable in this host
        }

        var tracker = new EntityTracker();
        tracker.Add(Player(5));
        tracker.ApplyEquipment(5, [new EquipmentItem(1, spyglassId, 1)]);
        // main arm LEFT (id 0); use the OFF hand, so the using arm is RIGHT.
        Assert.True(tracker.ApplyEntityData(5, [new EntityDataItem(15, 42, 0)]));
        Assert.True(tracker.ApplyEntityData(5, [new EntityDataItem(8, 0, 1 | 2)]));
        tracker.Tick();
        tracker.Tick();
        var state = tracker.Interpolate(0.5f)[0];
        Assert.Equal(0, state.MainArm);
        Assert.Equal(1, state.UseItemHand);
        Assert.Equal(2.5f, state.TicksUsingItem);
        Assert.Equal(0f, state.LeftTicksUsingItem);
        Assert.Equal(2.5f, state.RightTicksUsingItem);
        Assert.Equal(spyglassId, state.HeldOnHeadItemId);
    }

    [Fact]
    public void NonSpyglassUseItemDoesNotFillHeldOnHead()
    {
        var appleId = EntityModelResolver.Items?.GetId(
            Sharpcraft.Common.Identifiers.Identifier.Parse("minecraft:apple")) ?? -1;
        if (appleId <= 0)
        {
            return;
        }

        var tracker = new EntityTracker();
        tracker.Add(Player(5));
        tracker.ApplyEquipment(5, [new EquipmentItem(1, appleId, 1)]);
        Assert.True(tracker.ApplyEntityData(5, [new EntityDataItem(8, 0, 1 | 2)]));
        var state = tracker.Interpolate(0f)[0];
        Assert.Equal(-1, state.HeldOnHeadItemId);
    }

    [Fact]
    public void SwingDescriptionExposesDurationAndType()
    {
        var tracker = new EntityTracker();
        tracker.Add(Player(5));
        Assert.True(tracker.ApplySwingAnimation(5, hand: 0, animationType: 2, durationTicks: 10));
        var state = tracker.Interpolate(0f)[0];
        Assert.Equal(0, state.SwingHand);
        Assert.Equal(2, state.SwingType);
        Assert.Equal(10f, state.SwingDurationTicks);
        Assert.True(state.IsSwinging);
    }
}
