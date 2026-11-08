// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Nbt;
using Sharpcraft.Common.Network;
using Sharpcraft.Common.Registries;
using Xunit;

namespace Sharpcraft.Common.Tests.Registries;

/// <summary>Trim holder ids follow the connection's registry order, not a hard-coded bootstrap order.</summary>
[Collection("TrimRegistry")]
public sealed class TrimRegistryTableTests
{
    [Fact]
    public void ConfigureUsesServerRegistryOrder()
    {
        var registries = new DynamicRegistries();
        registries.Apply(
            new Identifier("minecraft", "trim_material"),
            [
                new DynamicRegistries.Entry(new Identifier("minecraft", "gold"), null),
                new DynamicRegistries.Entry(new Identifier("minecraft", "iron"), null)
            ]);
        registries.Apply(
            new Identifier("minecraft", "trim_pattern"),
            [
                new DynamicRegistries.Entry(new Identifier("minecraft", "coast"), null),
                new DynamicRegistries.Entry(new Identifier("minecraft", "bolt"), null)
            ]);

        try
        {
            TrimRegistryTable.Configure(registries);
            Assert.Equal("minecraft:gold", TrimRegistryTable.MaterialIdAt(0));
            Assert.Equal("minecraft:iron", TrimRegistryTable.MaterialIdAt(1));
            Assert.Equal("minecraft:coast", TrimRegistryTable.PatternIdAt(0));
            Assert.Equal("minecraft:bolt", TrimRegistryTable.PatternIdAt(1));
        }
        finally
        {
            TrimRegistryTable.Configure(null);
        }
    }

    [Fact]
    public void DynamicEntriesReadPaletteAssetAndDecalFromNbt()
    {
        var materialData = new NbtCompound().Set("palette_id", new NbtString("custom:gold_palette"));
        var patternData = new NbtCompound()
            .Set("asset_id", new NbtString("custom:rune"))
            .Set("decal", new NbtByte(1));
        var registries = new DynamicRegistries();
        registries.Apply(
            new Identifier("minecraft", "trim_material"),
            [new DynamicRegistries.Entry(new Identifier("custom", "gold"), materialData)]);
        registries.Apply(
            new Identifier("minecraft", "trim_pattern"),
            [new DynamicRegistries.Entry(new Identifier("custom", "rune"), patternData)]);

        try
        {
            TrimRegistryTable.Configure(registries);
            Assert.Equal("custom:gold_palette", TrimRegistryTable.PaletteIdFor("custom:gold"));
            var pattern = TrimRegistryTable.PatternForId("custom:rune");
            Assert.NotNull(pattern);
            Assert.Equal("custom:rune", pattern!.Value.AssetId);
            Assert.True(pattern.Value.Decal);
        }
        finally
        {
            TrimRegistryTable.Configure(null);
        }
    }

    [Fact]
    public void MissingRegistriesFallBackToBootstrapOrder()
    {
        try
        {
            TrimRegistryTable.Configure(null);
            Assert.Equal("minecraft:quartz", TrimRegistryTable.MaterialIdAt(0));
            Assert.Equal("minecraft:sentry", TrimRegistryTable.PatternIdAt(0));
        }
        finally
        {
            TrimRegistryTable.Configure(null);
        }
    }
}
