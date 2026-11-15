// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;

namespace Sharpcraft.Client.Tests.Repro;

/// <summary>
/// Issue 27: block entity animation values must be interpolated between game ticks, with the
/// double chest combining its halves by the larger value as the reference does.
/// </summary>
public sealed class Repro_027_BlockEntityAnimationTests
{
    [Theory]
    [InlineData(0f, 1f, 0f, 0f)]
    [InlineData(0f, 1f, 0.25f, 0.25f)]
    [InlineData(0f, 1f, 0.5f, 0.5f)]
    [InlineData(0f, 1f, 0.75f, 0.75f)]
    [InlineData(0f, 1f, 1f, 1f)]
    public void LerpMatchesTheReference(float previous, float current, float partialTick, float expected)
    {
        Assert.Equal(expected, BlockEntityAnimation.Lerp(previous, current, partialTick), 6);
    }

    [Fact]
    public void PartialTickIsClamped()
    {
        Assert.Equal(0f, BlockEntityAnimation.Lerp(0f, 1f, -1f));
        Assert.Equal(1f, BlockEntityAnimation.Lerp(0f, 1f, 2f));
    }

    [Fact]
    public void ASingleChestUsesItsOwnValue()
    {
        Assert.Equal(0.5f, BlockEntityAnimation.Openness(0f, 1f, 0.5f, 0f, 0f, doubleChest: false), 6);
    }

    [Fact]
    public void ADoubleChestUsesTheLargerHalf()
    {
        // First half closed, second half open: the pair opens as far as the more open half.
        Assert.Equal(0.5f, BlockEntityAnimation.Openness(0f, 0f, 0.5f, 0f, 1f, doubleChest: true), 6);
        Assert.Equal(0.25f, BlockEntityAnimation.Openness(0f, 0.5f, 0.5f, 0f, 0f, doubleChest: true), 6);
    }
}
