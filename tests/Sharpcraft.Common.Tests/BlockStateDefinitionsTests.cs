// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Registries;

namespace Sharpcraft.Common.Tests;

/// <summary>
/// Pins the block-state property schema against the baseline registry. The strongest check here is
/// the product test: for every block, the multiplied property radices must equal the state count the
/// jar snapshot records, which fails if a property is missing, extra, or has the wrong values.
/// </summary>
public sealed class BlockStateDefinitionsTests
{
    private static readonly BlockStateDefinitions Definitions = BlockStateDefinitions.Baseline;

    /// <summary>
    /// Every block that has more than one state carries a schema, and a block without one is
    /// single state. Blocks such as air or stone have no properties at all, so they contribute no
    /// rows to the schema by design.
    /// </summary>
    [Fact]
    public void EveryMultiStateBlockHasADefinition()
    {
        var layout = VanillaBlockRegistry.Layout;

        foreach (var entry in layout.Blocks)
        {
            if (Definitions.TryGet(entry.Id, out var definition))
            {
                Assert.Equal(entry.StateCount, definition.StateCount);
            }
            else
            {
                Assert.Equal(1, entry.StateCount);
            }
        }
    }

    /// <summary>The schema covers exactly the blocks that have properties.</summary>
    [Fact]
    public void TheSchemaCoversEveryBlockWithProperties()
    {
        var layout = VanillaBlockRegistry.Layout;
        var withProperties = layout.Blocks.Count(entry => entry.StateCount > 1);

        Assert.Equal(withProperties, Definitions.Count);
    }

    /// <summary>The first chest state is north, single, waterlogged.</summary>
    [Fact]
    public void TheFirstChestStateHoldsTheFirstValueOfEveryProperty()
    {
        Assert.True(Definitions.TryGetProperty(5543, "facing", out var facing));
        Assert.True(Definitions.TryGetProperty(5543, "type", out var type));
        Assert.True(Definitions.TryGetProperty(5543, "waterlogged", out var waterlogged));

        Assert.Equal("north", facing);
        Assert.Equal("single", type);
        Assert.Equal("true", waterlogged);
    }

    /// <summary>The fastest varying property flips between consecutive states.</summary>
    [Fact]
    public void ConsecutiveStatesDifferInTheFastestProperty()
    {
        Assert.True(Definitions.TryGetProperty(5544, "waterlogged", out var waterlogged));
        Assert.True(Definitions.TryGetProperty(5544, "type", out var type));

        Assert.Equal("false", waterlogged);
        Assert.Equal("single", type);
    }

    /// <summary>The last chest state has advanced through every property.</summary>
    [Fact]
    public void TheLastChestStateHoldsTheLastValueOfEveryProperty()
    {
        Assert.True(Definitions.TryGetProperty(5566, "facing", out var facing));
        Assert.True(Definitions.TryGetProperty(5566, "type", out var type));
        Assert.True(Definitions.TryGetProperty(5566, "waterlogged", out var waterlogged));

        Assert.Equal("east", facing);
        Assert.Equal("right", type);
        Assert.Equal("false", waterlogged);
    }

    /// <summary>A property the block does not have is reported as absent rather than blank.</summary>
    [Fact]
    public void UnknownPropertiesAreReportedAsAbsent()
    {
        Assert.False(Definitions.TryGetProperty(5543, "axis", out _));
        Assert.False(Definitions.TryGetProperty(0, "facing", out _));
    }
}
