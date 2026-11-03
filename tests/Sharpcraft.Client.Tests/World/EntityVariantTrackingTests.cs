// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Common.Network;
using Xunit;

namespace Sharpcraft.Client.Tests.World;

/// <summary>The variant field carried by synced entity data, and that it reaches the render state.</summary>
public sealed class EntityVariantTrackingTests
{
    private static AddedEntity Pig(int id) =>
        new(id, Guid.Empty, PigModelDefinition.PigEntityTypeId, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    [Fact]
    public void AVariantValueIsStoredAndExposedOnTheRenderState()
    {
        var tracker = new EntityTracker();
        tracker.Add(Pig(5));

        var applied = tracker.ApplyEntityData(5, [new EntityDataItem(19, 28, 2)]);

        Assert.True(applied);
        Assert.Equal(1, tracker.VariantUpdates);
        var state = tracker.Interpolate(0f)[0];
        Assert.Equal(2, state.VariantId);
    }

    [Fact]
    public void AnItemThatIsNotTheVariantIndexIsIgnored()
    {
        var tracker = new EntityTracker();
        tracker.Add(Pig(5));

        var applied = tracker.ApplyEntityData(5, [new EntityDataItem(18, 1, 100)]);

        Assert.False(applied);
        Assert.Equal(0, tracker.Interpolate(0f)[0].VariantId);
    }

    [Fact]
    public void ATypeWithoutVariantsIgnoresTheUpdate()
    {
        var tracker = new EntityTracker();
        tracker.Add(new AddedEntity(6, Guid.Empty, CreeperModelDefinition.CreeperEntityTypeId, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0));

        var applied = tracker.ApplyEntityData(6, [new EntityDataItem(19, 28, 2)]);

        Assert.False(applied);
    }

    [Fact]
    public void WolfFlagsAndAngerEndTimeBecomeRenderStateBooleans()
    {
        var tracker = new EntityTracker { GameTime = 100 };
        tracker.Add(new AddedEntity(7, Guid.Empty, 152, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0));

        Assert.True(tracker.ApplyEntityData(7, [new EntityDataItem(18, 0, 4)]));
        Assert.True(tracker.Interpolate(0f)[0].IsTame);

        tracker.ApplyEntityData(7, [new EntityDataItem(22, 2, EntityDataItem.NotAnInteger, LongValue: 500)]);
        Assert.True(tracker.Interpolate(0f)[0].IsAngry);

        tracker.GameTime = 600;
        Assert.False(tracker.Interpolate(0f)[0].IsAngry);
    }

    [Fact]
    public void FoxSleepingAndBeeNectarFlagsAreExposed()
    {
        var tracker = new EntityTracker();
        tracker.Add(new AddedEntity(8, Guid.Empty, 55, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0));
        tracker.Add(new AddedEntity(9, Guid.Empty, 11, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0));

        tracker.ApplyEntityData(8, [new EntityDataItem(19, 0, 32)]);
        tracker.ApplyEntityData(9, [new EntityDataItem(18, 0, 8)]);

        Assert.True(tracker.Interpolate(0f)[0].IsSleeping);
        Assert.True(tracker.Interpolate(0f)[1].HasNectar);
    }

    [Fact]
    public void AnUpdateForAnUnknownEntityIsCounted()
    {
        var tracker = new EntityTracker();

        var applied = tracker.ApplyEntityData(404, [new EntityDataItem(19, 28, 1)]);

        Assert.False(applied);
        Assert.Equal(1, tracker.UnknownUpdates);
    }
}
