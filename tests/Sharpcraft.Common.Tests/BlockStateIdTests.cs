// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Globalization;
using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Registries;
using Xunit;

namespace Sharpcraft.Common.Tests;

/// <summary>
/// Global block-state ids (T-010): property ordering, cartesian indexing and the
/// registration-order id layout, verified against vanilla samples
/// (docs/analysis/common/registry-order.md 5, 7.1).
/// </summary>
public sealed class BlockStateIdTests
{
    [Fact]
    public void PropertyOrderIsSortedByNameWhileValuesKeepDeclarationOrder()
    {
        var definition = new BlockStateDefinition(
            Identifier.Parse("minecraft:oak_stairs"),
            [
                new BlockStateProperty("waterlogged", ["true", "false"]),
                new BlockStateProperty("shape", ["straight", "inner_left"]),
                new BlockStateProperty("facing", ["north", "south"])
            ]);

        Assert.Equal(["facing", "shape", "waterlogged"], definition.Properties.Select(property => property.Name));
        Assert.Equal(2 * 2 * 2, definition.StateCount);
    }

    [Fact]
    public void LastPropertyVariesFastest()
    {
        var definition = new BlockStateDefinition(
            Identifier.Parse("minecraft:test"),
            [
                new BlockStateProperty("a", ["0", "1"]),
                new BlockStateProperty("b", ["0", "1"])
            ]);

        Assert.Equal(0, definition.IndexOf(["0", "0"]));
        Assert.Equal(1, definition.IndexOf(["0", "1"]));
        Assert.Equal(2, definition.IndexOf(["1", "0"]));
        Assert.Equal(3, definition.IndexOf(["1", "1"]));
    }

    [Fact]
    public void InvalidValuesAndArityAreRejected()
    {
        var definition = new BlockStateDefinition(
            Identifier.Parse("minecraft:test"),
            [new BlockStateProperty("a", ["0", "1"])]);

        Assert.Throws<ArgumentException>(() => definition.IndexOf(["2"]));
        Assert.Throws<ArgumentException>(() => definition.IndexOf([]));
    }

    [Fact]
    public void LayoutAssignsContiguousRangesInRegistrationOrder()
    {
        var stone = Identifier.Parse("minecraft:stone");
        var grass = Identifier.Parse("minecraft:grass_block");
        var stairs = Identifier.Parse("minecraft:oak_stairs");

        var layout = new BlockStateLayout([(stone, 1), (grass, 2), (stairs, 40)]);

        Assert.Equal(0, layout.GetBaseId(stone));
        Assert.Equal(1, layout.GetBaseId(grass));
        Assert.Equal(3, layout.GetBaseId(stairs));
        Assert.Equal(43, layout.TotalStates);
        Assert.Throws<KeyNotFoundException>(() => layout.GetBaseId(Identifier.Parse("minecraft:unknown")));
    }

    [Fact]
    public void DuplicateOrEmptyBlocksAreRejected()
    {
        var stone = Identifier.Parse("minecraft:stone");

        Assert.Throws<InvalidOperationException>(() => new BlockStateLayout([(stone, 1), (stone, 1)]));
        Assert.Throws<ArgumentException>(() => new BlockStateLayout([(stone, 0)]));
    }

    [Fact]
    public void VanillaLayoutAndSamplesAreReproduced()
    {
        var layout = LoadVanillaLayout();
        var samples = LoadVanillaSamples();

        Assert.Equal(1286, layout.Blocks.Count);
        Assert.Equal(35723, layout.TotalStates);

        // grass_block is the anchor recorded during analysis: its two states are ids 8 and 9.
        Assert.Equal(8, layout.GetBaseId(Identifier.Parse("minecraft:grass_block")));

        var checkedStates = 0;
        foreach (var (definition, states) in samples)
        {
            foreach (var (values, expectedId) in states)
            {
                Assert.Equal(expectedId, layout.GetStateId(definition, values));
                checkedStates++;
            }
        }

        Assert.Equal(1442, checkedStates);
    }

    private static BlockStateLayout LoadVanillaLayout()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "vanilla-block-state-counts.txt");
        Assert.True(File.Exists(path), $"layout snapshot missing from test output: {path}");

        var blocks = File.ReadAllLines(path)
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .Select(line => line.Split('\t'))
            .Select(parts => (Id: Identifier.Parse(parts[1]), Count: int.Parse(parts[2], CultureInfo.InvariantCulture)));

        return new BlockStateLayout(blocks);
    }

    private static List<(BlockStateDefinition Definition, List<(string[] Values, int Id)> States)> LoadVanillaSamples()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "vanilla-block-state-samples.txt");
        Assert.True(File.Exists(path), $"state samples missing from test output: {path}");

        var definitions = new Dictionary<string, BlockStateDefinition>(StringComparer.Ordinal);
        var states = new Dictionary<string, List<(string[] Values, int Id)>>(StringComparer.Ordinal);

        foreach (var line in File.ReadAllLines(path).Where(line => line.Length > 0 && !line.StartsWith('#')))
        {
            var parts = line.Split('\t');
            switch (parts[0])
            {
                case "DEF":
                {
                    var id = Identifier.Parse(parts[1]);
                    var properties = parts.Skip(2).Select(ParseProperty).ToArray();
                    definitions[parts[1]] = new BlockStateDefinition(id, properties);
                    states[parts[1]] = [];
                    break;
                }

                case "STATEID":
                {
                    var values = parts[3].Split(',').Select(pair => pair.Split('=', 2)[1]).ToArray();
                    states[parts[1]].Add((values, int.Parse(parts[2], CultureInfo.InvariantCulture)));
                    break;
                }
            }
        }

        return definitions.Select(pair => (pair.Value, states[pair.Key])).ToList();
    }

    private static BlockStateProperty ParseProperty(string text)
    {
        var separator = text.IndexOf('=');
        var name = text[..separator];
        var values = text[(separator + 1)..].Split(',');
        return new BlockStateProperty(name, values);
    }

    /// <summary>Values at offset zero are the first value of every property.</summary>
    [Fact]
    public void ValuesAtZeroTakesTheFirstValueOfEachProperty()
    {
        var definition = new BlockStateDefinition(
            Identifier.Parse("minecraft:test"),
            [
                new BlockStateProperty("a", ["0", "1", "2"]),
                new BlockStateProperty("b", ["x", "y"])
            ]);

        Assert.Equal(["0", "x"], definition.ValuesAt(0));
    }

    /// <summary>The last offset takes the last value of every property.</summary>
    [Fact]
    public void ValuesAtTheLastOffsetTakesTheLastValueOfEachProperty()
    {
        var definition = new BlockStateDefinition(
            Identifier.Parse("minecraft:test"),
            [
                new BlockStateProperty("a", ["0", "1", "2"]),
                new BlockStateProperty("b", ["x", "y"])
            ]);

        Assert.Equal(["2", "y"], definition.ValuesAt(definition.StateCount - 1));
    }

    /// <summary>Decoding then encoding must return the same offset for every state.</summary>
    [Fact]
    public void ValuesAtIsTheInverseOfIndexOf()
    {
        var definition = new BlockStateDefinition(
            Identifier.Parse("minecraft:chest"),
            [
                new BlockStateProperty("facing", ["north", "south", "west", "east"]),
                new BlockStateProperty("type", ["single", "left", "right"]),
                new BlockStateProperty("waterlogged", ["true", "false"])
            ]);

        Assert.Equal(24, definition.StateCount);
        for (var offset = 0; offset < definition.StateCount; offset++)
        {
            Assert.Equal(offset, definition.IndexOf(definition.ValuesAt(offset)));
        }
    }

    /// <summary>A chest state six offsets in has advanced one step along the slowest property.</summary>
    [Fact]
    public void TheSlowestPropertyAdvancesOnceEveryFullTurnOfTheRest()
    {
        var definition = new BlockStateDefinition(
            Identifier.Parse("minecraft:chest"),
            [
                new BlockStateProperty("facing", ["north", "south", "west", "east"]),
                new BlockStateProperty("type", ["single", "left", "right"]),
                new BlockStateProperty("waterlogged", ["true", "false"])
            ]);

        Assert.Equal(["south", "single", "true"], definition.ValuesAt(6));
        Assert.Equal(["east", "right", "false"], definition.ValuesAt(23));
    }

    /// <summary>An offset outside the block is rejected rather than wrapped.</summary>
    [Fact]
    public void ValuesAtRejectsOffsetsOutsideTheBlock()
    {
        var definition = new BlockStateDefinition(
            Identifier.Parse("minecraft:test"),
            [new BlockStateProperty("a", ["0", "1"])]);

        Assert.Throws<ArgumentOutOfRangeException>(() => definition.ValuesAt(2));
        Assert.Throws<ArgumentOutOfRangeException>(() => definition.ValuesAt(-1));
    }
}
