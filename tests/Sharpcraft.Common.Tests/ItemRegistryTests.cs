// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Registries;
using Xunit;

namespace Sharpcraft.Common.Tests;

/// <summary>Vanilla item registry order and id contract (docs/analysis/common/item-registry.md).</summary>
public sealed class ItemRegistryTests
{
    private static ItemRegistry LoadSnapshot() => ItemRegistry.LoadEmbedded();

    [Fact]
    public void SnapshotHasTheBaselineItemCountAndAirAtZero()
    {
        var registry = LoadSnapshot();

        Assert.Equal(1658, registry.Count);
        Assert.Equal(Identifier.Parse("minecraft:air"), registry.ById(0)!.Id);
    }

    [Fact]
    public void NumericIdsMatchTheReferenceRegistrationOrder()
    {
        var registry = LoadSnapshot();

        Assert.Equal("minecraft:saddle", registry.ById(949)!.Id.ToString());
        Assert.Equal("minecraft:white_carpet", registry.ById(580)!.Id.ToString());
        Assert.Equal("minecraft:red_harness", registry.ById(964)!.Id.ToString());
        Assert.Equal("minecraft:wolf_armor", registry.ById(1004)!.Id.ToString());
        Assert.Equal("minecraft:iron_horse_armor", registry.ById(1407)!.Id.ToString());
        Assert.Equal("minecraft:iron_nautilus_armor", registry.ById(1485)!.Id.ToString());
    }

    [Fact]
    public void LookupAndEnumerationUseTheSameOrderedRegistry()
    {
        var registry = LoadSnapshot();

        Assert.True(registry.TryGet(Identifier.Parse("minecraft:diamond"), out var diamond));
        Assert.Equal("minecraft:air", registry.Items[0].Id.ToString());
        Assert.Equal("minecraft:diamond", diamond!.Id.ToString());
        Assert.Equal(registry.Count, registry.Items.Count);
    }

    [Fact]
    public void MalformedSnapshotFailsAtLoad()
    {
        Assert.Throws<InvalidDataException>(() => ItemRegistry.FromLines(["# empty"]));
        Assert.Throws<InvalidDataException>(() => ItemRegistry.FromLines(["minecraft:stone"]));
    }
}


