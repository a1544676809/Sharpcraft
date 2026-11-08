// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Network;
using Sharpcraft.Common.Registries;
using Xunit;

namespace Sharpcraft.Common.Tests.Registries;

/// <summary>Trim holder ids follow the connection's registry order, not a hard-coded bootstrap order.</summary>
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
