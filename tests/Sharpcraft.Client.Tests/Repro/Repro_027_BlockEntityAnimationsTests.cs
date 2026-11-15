// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.World;

namespace Sharpcraft.Client.Tests.Repro;

/// <summary>
/// Issue 27: the lid opening has to interpolate between game ticks, so a frame that lands partway
/// through a tick shows the value between the previous step and the current one.
/// </summary>
public sealed class Repro_027_BlockEntityAnimationsTests
{
    [Fact]
    public void AFramePartwayThroughATickSeesTheValueBetweenSteps()
    {
        var animations = new BlockEntityAnimations();
        animations.Apply(0, 64, 0, BlockEntityAnimations.ChestOpenEvent, parameter: 1);

        // One tick moves the lid from zero to one step; the previous value is still zero.
        animations.Tick();

        Assert.Equal(0f, animations.GetOpenness(0, 64, 0, partialTicks: 0f), 6);
        Assert.Equal(0.05f, animations.GetOpenness(0, 64, 0, partialTicks: 0.5f), 6);
        Assert.Equal(0.1f, animations.GetOpenness(0, 64, 0, partialTicks: 1f), 6);
    }

    [Fact]
    public void ClosingInterpolatesFromTheOpenValue()
    {
        var animations = new BlockEntityAnimations();
        animations.Apply(0, 64, 0, BlockEntityAnimations.ChestOpenEvent, parameter: 1);
        animations.Tick();
        animations.Tick();
        Assert.Equal(0.2f, animations.GetOpenness(0, 64, 0, partialTicks: 1f), 6);

        animations.Apply(0, 64, 0, BlockEntityAnimations.ChestOpenEvent, parameter: 0);
        animations.Tick();

        Assert.Equal(0.2f, animations.GetOpenness(0, 64, 0, partialTicks: 0f), 6);
        Assert.Equal(0.15f, animations.GetOpenness(0, 64, 0, partialTicks: 0.5f), 6);
        Assert.Equal(0.1f, animations.GetOpenness(0, 64, 0, partialTicks: 1f), 6);
    }

    [Fact]
    public void AnUntrackedPositionReportsZero()
    {
        var animations = new BlockEntityAnimations();

        Assert.Equal(0f, animations.GetOpenness(0, 64, 0, partialTicks: 0.5f));
    }

    [Fact]
    public void TheLidStopsAtFullyOpen()
    {
        var animations = new BlockEntityAnimations();
        animations.Apply(0, 64, 0, BlockEntityAnimations.ChestOpenEvent, parameter: 1);

        for (var i = 0; i < 20; i++)
        {
            animations.Tick();
        }

        Assert.Equal(1f, animations.GetOpenness(0, 64, 0, partialTicks: 1f), 6);
    }
}
