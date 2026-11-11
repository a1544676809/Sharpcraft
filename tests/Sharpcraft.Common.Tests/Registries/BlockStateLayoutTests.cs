// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Registries;
using Xunit;

namespace Sharpcraft.Common.Tests.Registries;

/// <summary>
/// Locks what <see cref="BlockStateLayout.BlockAt"/> answers, because it is read once per block face while meshing and
/// was a linear scan until a profile put it at over half of sampled CPU (T-825). The lookup that replaced it has to
/// return exactly what the scan returned, including the null for a state id no block owns.
/// </summary>
public sealed class BlockStateLayoutTests
{
    private static BlockStateLayout Build() => new(
    [
        (new Identifier("minecraft", "stone"), 1),
        (new Identifier("minecraft", "dirt"), 1),
        (new Identifier("minecraft", "oak_log"), 12),
        (new Identifier("minecraft", "water"), 16)
    ]);

    [Fact]
    public void EveryStateInARangeResolvesToItsBlock()
    {
        var layout = Build();

        // stone 0, dirt 1, oak_log 2..13, water 14..29.
        Assert.Equal("stone", layout.BlockAt(0)?.Path);
        Assert.Equal("dirt", layout.BlockAt(1)?.Path);
        for (var state = 2; state < 14; state++)
        {
            Assert.Equal("oak_log", layout.BlockAt(state)?.Path);
        }

        for (var state = 14; state < 30; state++)
        {
            Assert.Equal("water", layout.BlockAt(state)?.Path);
        }
    }

    [Fact]
    public void BothEndsOfEveryRangeAreInclusiveAndExclusive()
    {
        var layout = Build();

        Assert.Equal("oak_log", layout.BlockAt(2)?.Path);
        Assert.Equal("oak_log", layout.BlockAt(13)?.Path);
        Assert.Equal("water", layout.BlockAt(14)?.Path);
        Assert.Null(layout.BlockAt(30));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-4096)]
    [InlineData(30)]
    [InlineData(1_000_000)]
    public void StatesNoBlockOwnsResolveToNull(int stateId)
    {
        Assert.Null(Build().BlockAt(stateId));
    }

    [Fact]
    public void AnEmptyLayoutResolvesNothing()
    {
        var layout = new BlockStateLayout([]);

        Assert.Equal(0, layout.TotalStates);
        Assert.Null(layout.BlockAt(0));
        Assert.Null(layout.BlockAt(-1));
    }

    [Fact]
    public void TotalStatesCoversEveryRange()
    {
        var layout = Build();

        Assert.Equal(30, layout.TotalStates);
        Assert.Equal(4, layout.Blocks.Count);
    }
}
