// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Linq;
using Sharpcraft.Client.Render;
using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Registries;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>Humanoid armor: equipment asset/texture resolution and per-slot layer parts (docs/analysis/render/entity-equipment.md).</summary>
public sealed class HumanoidArmorTests
{
    private static ItemInfo Item(string path) => new(Identifier.Parse("minecraft:" + path));

    [Theory]
    [InlineData("iron_helmet", "entity/equipment/humanoid/iron")]
    [InlineData("diamond_chestplate", "entity/equipment/humanoid/diamond")]
    [InlineData("golden_boots", "entity/equipment/humanoid/gold")]
    [InlineData("turtle_helmet", "entity/equipment/humanoid/turtle_scute")]
    public void OuterArmorResolvesToHumanoidLayer(string item, string expected)
    {
        var texture = EquipmentTextureCatalog.Resolve(EquipmentAssetKind.HumanoidArmor, Item(item));
        Assert.NotNull(texture);
        Assert.Equal(expected, texture!.PrimaryPath);
    }

    [Theory]
    [InlineData("iron_leggings", "entity/equipment/humanoid_leggings/iron")]
    [InlineData("netherite_leggings", "entity/equipment/humanoid_leggings/netherite")]
    public void LeggingsResolveToTheLeggingsLayer(string item, string expected)
    {
        var texture = EquipmentTextureCatalog.Resolve(EquipmentAssetKind.HumanoidLeggings, Item(item));
        Assert.NotNull(texture);
        Assert.Equal(expected, texture!.PrimaryPath);
    }

    [Fact]
    public void NonArmorItemsDoNotResolve()
    {
        Assert.Null(EquipmentTextureCatalog.Resolve(EquipmentAssetKind.HumanoidArmor, Item("stick")));
        Assert.Null(EquipmentTextureCatalog.Resolve(EquipmentAssetKind.HumanoidLeggings, Item("stick")));
    }

    [Fact]
    public void HumanoidTypesGetFourArmorSlotLayers()
    {
        var parts = EntityModelResolver.Resolve(154).Parts.Select(part => part.Name).ToHashSet();

        Assert.Contains("equipment_HumanoidArmor_5_head", parts);
        Assert.Contains("equipment_HumanoidArmor_4_body", parts);
        Assert.Contains("equipment_HumanoidLeggings_3_left_leg", parts);
        Assert.Contains("equipment_HumanoidArmor_2_left_leg", parts);
        // Only the slot's own parts are cloned: the helmet layer must not carry body/legs.
        Assert.DoesNotContain("equipment_HumanoidArmor_5_body", parts);
    }
}
