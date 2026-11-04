// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Registries;
using Xunit;

namespace Sharpcraft.Common.Tests.Registries;

/// <summary>Entity dimensions/eye heights from the vanilla registration-order snapshot.</summary>
public sealed class EntityDimensionsRegistryTests
{
    [Fact]
    public void ResolvesPlayerAndWolfEyeHeights()
    {
        Assert.Equal(1.62f, EntityDimensionsRegistry.Get(159)!.Value.EyeHeight, 4);
        Assert.Equal(0.68f, EntityDimensionsRegistry.Get(152)!.Value.EyeHeight, 4);
    }

    [Fact]
    public void UnknownTypeIsNull()
    {
        Assert.Null(EntityDimensionsRegistry.Get(1000));
    }
}
