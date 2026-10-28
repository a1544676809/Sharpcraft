// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Registries;
using Xunit;

namespace Sharpcraft.Common.Tests;

/// <summary>
/// The embedded baseline block layout (T-039): registration order, state counts and the resulting
/// global state ids, pinned against the snapshot captured from the 26.3 jar.
/// </summary>
public sealed class VanillaBlockLayoutTests
{
    [Fact]
    public void SnapshotYieldsTheDocumentedTotals()
    {
        var layout = VanillaBlockRegistry.Layout;

        Assert.Equal(VanillaBlockRegistry.BlockCount, layout.Blocks.Count);
        Assert.Equal(VanillaBlockRegistry.BlockStateCount, layout.TotalStates);
    }

    [Fact]
    public void BaseIdsAccumulateInRegistrationOrder()
    {
        var layout = VanillaBlockRegistry.Layout;
        var expectedBase = 0;

        for (var index = 0; index < layout.Blocks.Count; index++)
        {
            var entry = layout.Blocks[index];
            Assert.Equal(expectedBase, entry.BaseId);
            Assert.True(entry.StateCount >= 1);
            expectedBase += entry.StateCount;
        }

        Assert.Equal(VanillaBlockRegistry.BlockStateCount, expectedBase);
    }

    [Theory]
    [InlineData("minecraft:air")]
    [InlineData("minecraft:stone")]
    [InlineData("minecraft:dirt")]
    [InlineData("minecraft:grass_block")]
    [InlineData("minecraft:cobblestone")]
    [InlineData("minecraft:oak_stairs")]
    [InlineData("minecraft:water")]
    [InlineData("minecraft:lava")]
    public void KnownBlocksResolveToPlausibleRanges(string blockId)
    {
        var layout = VanillaBlockRegistry.Layout;

        Assert.True(
            layout.TryGetBaseId(Identifier.Parse(blockId), out var baseId),
            $"{blockId} should be in the baseline registry");

        Assert.InRange(baseId, 0, VanillaBlockRegistry.BlockStateCount - 1);
    }

    [Fact]
    public void AirIsFirstAndHasASingleState()
    {
        var layout = VanillaBlockRegistry.Layout;
        var air = Identifier.Parse("minecraft:air");

        Assert.Equal(0, layout.GetBaseId(air));

        var entry = layout.Blocks[0];
        Assert.Equal(air, entry.Id);
        Assert.Equal(1, entry.StateCount);
    }

    [Fact]
    public void BlocksWithManyStatesGetProportionallyLargeRanges()
    {
        var layout = VanillaBlockRegistry.Layout;

        // A redstone wire has 1296 states in vanilla, so its range must be far larger than stone's.
        var wire = layout.Blocks.First(entry => entry.Id.ToString() == "minecraft:redstone_wire");
        var stone = layout.Blocks.First(entry => entry.Id.ToString() == "minecraft:stone");

        Assert.Equal(1296, wire.StateCount);
        Assert.Equal(1, stone.StateCount);
    }

    [Fact]
    public void EveryIdentifierIsUnique()
    {
        var layout = VanillaBlockRegistry.Layout;
        var seen = new HashSet<Identifier>();

        foreach (var entry in layout.Blocks)
        {
            Assert.True(seen.Add(entry.Id), $"duplicate block in the snapshot: {entry.Id}");
        }
    }
}
