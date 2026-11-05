// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Linq;
using Sharpcraft.Client.Render;
using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Registries;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>Elytra wings: equipment texture resolution and the two-wing model layer.</summary>
public sealed class ElytraLayerTests
{
    [Fact]
    public void ElytraResolvesToTheWingsTexture()
    {
        var texture = EquipmentTextureCatalog.Resolve(EquipmentAssetKind.Elytra, new ItemInfo(Identifier.Parse("minecraft:elytra")));
        Assert.NotNull(texture);
        Assert.Equal("entity/equipment/wings/elytra", texture!.PrimaryPath);
    }

    [Fact]
    public void HumanoidModelCarriesTheTwoWings()
    {
        var names = EntityModelResolver.Resolve(154).Parts.Select(part => part.Name).ToHashSet();
        Assert.Contains("equipment_Elytra_4_left_wing", names);
        Assert.Contains("equipment_Elytra_4_right_wing", names);
    }
}
