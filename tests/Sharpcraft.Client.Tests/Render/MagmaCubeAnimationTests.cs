// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>MagmaCube squish: the stacked cubes spread by <c>(i - 4) * max(0, squish) * 1.7</c> (y-up).</summary>
public sealed class MagmaCubeAnimationTests
{
    private static EntityAnimationFrame Frame(int typeId, float squish) => new(
        TypeId: typeId,
        AgeInTicks: 0f,
        WalkAnimationPos: 0f,
        WalkAnimationSpeed: 0f,
        Pitch: 0f,
        Yaw: 0f,
        Flap: 0f,
        FlapSpeed: 0f,
        TentacleAngle: 0f,
        IsResting: false,
        IsBaby: false,
        IsHidingInShell: false,
        PartialTick: 0f,
        Squish: squish,
        CubeSize: 1);

    [Fact]
    public void PositiveSquishSpreadsTheStack()
    {
        var provider = new MagmaCubeAnimationProvider(MagmaCubeModelDefinition.MagmaCubeEntityTypeId);
        var pose = new EntityPose();
        provider.Apply(provider.CreateState(), Frame(MagmaCubeModelDefinition.MagmaCubeEntityTypeId, 1f), pose);

        Assert.Equal(-4 * 1.7f, pose.Get("cube0").Position.Y, 5);
        Assert.Equal(0f, pose.Get("cube4").Position.Y, 5);
        Assert.Equal(3 * 1.7f, pose.Get("cube7").Position.Y, 5);
    }

    [Fact]
    public void NegativeSquishClampsToZero()
    {
        var provider = new MagmaCubeAnimationProvider(MagmaCubeModelDefinition.MagmaCubeEntityTypeId);
        var pose = new EntityPose();
        provider.Apply(provider.CreateState(), Frame(MagmaCubeModelDefinition.MagmaCubeEntityTypeId, -0.5f), pose);

        Assert.Equal(0f, pose.Get("cube0").Position.Y, 5);
        Assert.Equal(0f, pose.Get("cube7").Position.Y, 5);
    }
}
