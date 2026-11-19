// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Linq;
using Sharpcraft.Client.Render;
using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Registries;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>Baby humanoid armor: dedicated baby mesh, non-uniform deformation, humanoid_baby texture layer.</summary>
public sealed class BabyHumanoidArmorTests
{

    [Fact]
    public void BabyArmorResolvesToTheHumanoidBabyLayer()
    {
        var texture = EquipmentTextureCatalog.Resolve(EquipmentAssetKind.HumanoidArmorBaby, new ItemInfo(Identifier.Parse("minecraft:iron_helmet")));
        Assert.NotNull(texture);
        Assert.Equal("entity/equipment/humanoid_baby/iron", texture!.PrimaryPath);
    }
}
