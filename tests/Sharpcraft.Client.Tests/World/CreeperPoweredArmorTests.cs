// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Common.Network;
using Xunit;

namespace Sharpcraft.Client.Tests.World;

/// <summary>task-254: Creeper DATA_IS_POWERED drives the raw CreeperPowerLayer only.</summary>
public sealed class CreeperPoweredArmorTests
{
    private static AddedEntity Creeper(int id) =>
        new(id, Guid.Empty, CreeperModelDefinition.CreeperEntityTypeId, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    [Fact]
    public void PoweredBooleanAccessorReachesRenderState()
    {
        var tracker = new EntityTracker();
        tracker.Add(Creeper(5));

        Assert.True(tracker.ApplyEntityData(5, [new EntityDataItem(17, 8, 1)]));
        Assert.True(tracker.Interpolate(0f)[0].IsPowered);

        Assert.True(tracker.ApplyEntityData(5, [new EntityDataItem(17, 8, 0)]));
        Assert.False(tracker.Interpolate(0f)[0].IsPowered);
    }

    [Fact]
    public void OtherSerializerAtThePoweredIndexIsIgnored()
    {
        var tracker = new EntityTracker();
        tracker.Add(Creeper(5));

        Assert.False(tracker.ApplyEntityData(5, [new EntityDataItem(17, 1, 1)]));
        Assert.False(tracker.Interpolate(0f)[0].IsPowered);
    }

    [Fact]
    public void CreeperPowerLayerIsRawAndOnlyVisibleWhenPowered()
    {
        var creeper = EntityModelResolver.Resolve(CreeperModelDefinition.CreeperEntityTypeId);
        var layer = creeper.RenderLayers!.Single(layer => layer.Name == "creeper_power");
        Assert.True(layer.Model!.RawSourceFrame);
        Assert.Equal(0f, layer.Model.BaselinePx);
        Assert.Equal(144, layer.Model.Compiled!.Cubes.Sum(cube => 24));
        Assert.NotNull(layer.VisibleWhen);

        var state = new EntityRenderState(1, CreeperModelDefinition.CreeperEntityTypeId, 0f, 0f, 0f, 0f, 0f);
        Assert.False(layer.VisibleWhen!(state));
        Assert.True(layer.VisibleWhen!(state with { IsPowered = true }));
    }

    [Fact]
    public void CreeperBodyGeometryIsUnchanged()
    {
        var creeper = EntityModelResolver.Resolve(CreeperModelDefinition.CreeperEntityTypeId);
        Assert.Equal(144, creeper.Compiled!.Cubes.Sum(cube => 24));
    }
}
