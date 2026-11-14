// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.World;

namespace Sharpcraft.Client.Tests;

/// <summary>
/// Pins the chest lid animation to the reference controller: a tenth per tick, ten ticks to travel,
/// and a partial tick interpolation between the previous and current values.
/// </summary>
public sealed class BlockEntityAnimationsTests
{
    private const float Tolerance = 1e-5f;

    private static BlockEntityAnimations OpenedChest()
    {
        var animations = new BlockEntityAnimations();
        animations.Apply(4, 70, -9, BlockEntityAnimations.ChestOpenEvent, 1);
        return animations;
    }

    /// <summary>An untracked position is fully closed.</summary>
    [Fact]
    public void AnUntrackedChestIsClosed()
    {
        var animations = new BlockEntityAnimations();

        Assert.Equal(0f, animations.GetOpenness(4, 70, -9, 0.5f), Tolerance);
        Assert.Equal(0, animations.Count);
    }

    /// <summary>Opening takes ten ticks at a tenth each.</summary>
    [Fact]
    public void OpeningTakesTenTicks()
    {
        var animations = OpenedChest();

        for (var tick = 0; tick < 10; tick++)
        {
            animations.Tick();
        }

        Assert.Equal(1f, animations.GetOpenness(4, 70, -9), Tolerance);
    }

    /// <summary>One tick opens a tenth, not the whole lid.</summary>
    [Fact]
    public void OneTickOpensOnlyATenth()
    {
        var animations = OpenedChest();
        animations.Tick();

        Assert.Equal(0.1f, animations.GetOpenness(4, 70, -9), Tolerance);
    }

    /// <summary>The partial tick interpolates between the previous and current openness.</summary>
    [Fact]
    public void ThePartialTickInterpolatesBetweenSteps()
    {
        var animations = OpenedChest();
        animations.Tick();

        // Previous zero, current a tenth: half way through the tick reads half of that.
        Assert.Equal(0.05f, animations.GetOpenness(4, 70, -9, 0.5f), Tolerance);
        Assert.Equal(0.1f, animations.GetOpenness(4, 70, -9, 1f), Tolerance);
    }

    /// <summary>Closing travels back the same way.</summary>
    [Fact]
    public void ClosingTakesTenTicks()
    {
        var animations = OpenedChest();
        for (var tick = 0; tick < 10; tick++)
        {
            animations.Tick();
        }

        animations.Apply(4, 70, -9, BlockEntityAnimations.ChestOpenEvent, 0);
        for (var tick = 0; tick < 10; tick++)
        {
            animations.Tick();
        }

        Assert.Equal(0f, animations.GetOpenness(4, 70, -9), Tolerance);
    }

    /// <summary>A repeated open event does not restart or jump the animation.</summary>
    [Fact]
    public void RepeatedEventsDoNotJump()
    {
        var animations = OpenedChest();
        animations.Tick();
        animations.Tick();

        animations.Apply(4, 70, -9, BlockEntityAnimations.ChestOpenEvent, 3);

        Assert.Equal(0.2f, animations.GetOpenness(4, 70, -9), Tolerance);
    }

    /// <summary>Events other than the chest one are left alone for now.</summary>
    [Fact]
    public void OtherEventsAreIgnored()
    {
        var animations = new BlockEntityAnimations();
        animations.Apply(0, 0, 0, eventId: 7, parameter: 1);

        Assert.Equal(0, animations.Count);
    }

    /// <summary>Positions are keyed independently, including negative coordinates.</summary>
    [Fact]
    public void PositionsAreKeyedIndependently()
    {
        var animations = new BlockEntityAnimations();
        animations.Apply(-4, 70, -9, BlockEntityAnimations.ChestOpenEvent, 1);
        animations.Tick();

        Assert.Equal(0.1f, animations.GetOpenness(-4, 70, -9), Tolerance);
        Assert.Equal(0f, animations.GetOpenness(4, 70, -9), Tolerance);
    }
}
