// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Registries;
using Sharpcraft.Common.World;
using Xunit;

namespace Sharpcraft.Common.Tests.World;

/// <summary>
/// Fluid-state decoding equivalence (docs/modules/block-state.md step A, docs/specs/render/fluid-rendering.md):
/// the block <c>level</c> 0..15 maps to amount/falling and a surface height of amount/9.
/// </summary>
public sealed class FluidStateTests
{
    private static BlockStateLayout Layout() => new(
    [
        (new Identifier("minecraft", "stone"), 1),
        (new Identifier("minecraft", "water"), FluidStateCodec.FluidStateCount),
        (new Identifier("minecraft", "lava"), FluidStateCodec.FluidStateCount)
    ]);

    [Fact]
    public void LevelZeroIsAFullSource()
    {
        var state = FluidStateCodec.Decode(FluidKind.Water, 0);

        Assert.Equal(8, state.Amount);
        Assert.False(state.Falling);
        Assert.True(state.IsSource);
        Assert.Equal(8f / 9f, state.OwnHeight, 5);
    }

    [Theory]
    [InlineData(1, 7)]
    [InlineData(4, 4)]
    [InlineData(7, 1)]
    public void FlowingLevelsShrinkTheAmount(int level, int expectedAmount)
    {
        var state = FluidStateCodec.Decode(FluidKind.Water, level);

        Assert.Equal(expectedAmount, state.Amount);
        Assert.False(state.Falling);
        Assert.False(state.IsSource);
    }

    [Theory]
    [InlineData(8, 8)]
    [InlineData(12, 4)]
    [InlineData(15, 1)]
    public void FallingLevelsAreFallingAndNotSources(int level, int expectedAmount)
    {
        var state = FluidStateCodec.Decode(FluidKind.Water, level);

        // Even the full-amount falling level (8) is not a source.
        Assert.Equal(expectedAmount, state.Amount);
        Assert.True(state.Falling);
        Assert.False(state.IsSource);
    }

    [Fact]
    public void FromBlockStateMapsWaterAndLavaRanges()
    {
        var layout = Layout();

        var waterSource = FluidStateCodec.FromBlockState(layout.GetBaseId(new Identifier("minecraft", "water")), layout);
        Assert.Equal(FluidKind.Water, waterSource.Kind);
        Assert.True(waterSource.IsSource);

        var lavaFalling = FluidStateCodec.FromBlockState(layout.GetBaseId(new Identifier("minecraft", "lava")) + 9, layout);
        Assert.Equal(FluidKind.Lava, lavaFalling.Kind);
        Assert.True(lavaFalling.Falling);
        Assert.Equal(7, lavaFalling.Amount); // level 9 -> 16-9 = 7

        var stone = FluidStateCodec.FromBlockState(0, layout);
        Assert.True(stone.IsEmpty);
    }

    [Fact]
    public void RealRegistryWaterAndLavaOwnSixteenStates()
    {
        var layout = VanillaBlockRegistry.Layout;

        Assert.True(layout.TryGetBaseId(new Identifier("minecraft", "water"), out var waterBase));
        Assert.True(layout.TryGetBaseId(new Identifier("minecraft", "lava"), out var lavaBase));

        var water = FluidStateCodec.FromBlockState(waterBase, layout);
        Assert.Equal(FluidKind.Water, water.Kind);
        Assert.True(water.IsSource);

        var lavaTop = FluidStateCodec.FromBlockState(lavaBase + 15, layout);
        Assert.Equal(FluidKind.Lava, lavaTop.Kind);
        Assert.Equal(1, lavaTop.Amount);
        Assert.True(lavaTop.Falling);
    }
}
