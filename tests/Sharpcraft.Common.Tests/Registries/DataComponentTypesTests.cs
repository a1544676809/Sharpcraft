// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Registries;
using Xunit;

namespace Sharpcraft.Common.Tests.Registries;

/// <summary>The component id snapshot is loaded from the registration-order metadata.</summary>
public sealed class DataComponentTypesTests
{
    [Fact]
    public void ResolvesTheCommonEquipmentComponents()
    {
        Assert.Equal(3, DataComponentTypes.GetId("damage"));
        Assert.Equal(4, DataComponentTypes.GetId("unbreakable"));
        Assert.Equal(12, DataComponentTypes.GetId("enchantments"));
        Assert.Equal(18, DataComponentTypes.GetId("repair_cost"));
        Assert.Equal(49, DataComponentTypes.GetId("charged_projectiles"));
    }

    [Fact]
    public void UnknownComponentIsMinusOne()
    {
        Assert.Equal(-1, DataComponentTypes.GetId("not_a_component"));
    }
}
