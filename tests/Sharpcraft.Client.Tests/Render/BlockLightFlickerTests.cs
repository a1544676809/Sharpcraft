// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Collections.Generic;
using Sharpcraft.Client.Render;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>Block-light flicker (docs/analysis/render/lighting-color-space.md #8): random-walk step then 0.9 decay.</summary>
public sealed class BlockLightFlickerTests
{
    private static BlockLightFlicker FromSequence(params float[] values)
    {
        var queue = new Queue<float>(values);
        return new BlockLightFlicker(() => queue.Dequeue());
    }

    [Fact]
    public void TickAppliesRandomWalkThenDecay()
    {
        // delta = (0.5 - 0.0) * 1.0 * 1.0 * 0.1 = 0.05; value = 0.05 * 0.9 = 0.045.
        var flicker = FromSequence(0.5f, 0.0f, 1.0f, 1.0f);

        flicker.Tick();

        Assert.Equal(0.045f, flicker.Value, 5);
        Assert.Equal(1.445f, flicker.BlockFactor, 5);
    }

    [Fact]
    public void SuccessiveTicksKeepDecaying()
    {
        // Second step: delta = (0 - 0) = 0; value = 0.045 * 0.9 = 0.0405.
        var flicker = FromSequence(0.5f, 0.0f, 1.0f, 1.0f, 0.0f, 0.0f, 1.0f, 1.0f);

        flicker.Tick();
        flicker.Tick();

        Assert.Equal(0.0405f, flicker.Value, 5);
    }

    [Fact]
    public void StartsAtThePlainBlockFactor()
    {
        var flicker = FromSequence();
        Assert.Equal(0f, flicker.Value);
        Assert.Equal(1.4f, flicker.BlockFactor, 5);
    }
}
