// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;
using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Registries;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>Controlled item → equipment texture mapping (docs/analysis/render/entity-equipment.md 3.2).</summary>
public sealed class EquipmentTextureCatalogTests
{
    private static ItemInfo Item(string path) => new(Identifier.Parse("minecraft:" + path));

    [Fact]
    public void HorseArmorMaterialsMapToHorseBodyTextures()
    {
        var expected = new Dictionary<string, string>
        {
            ["leather_horse_armor"] = "leather",
            ["iron_horse_armor"] = "iron",
            ["golden_horse_armor"] = "gold",
            ["diamond_horse_armor"] = "diamond",
            ["copper_horse_armor"] = "copper",
            ["netherite_horse_armor"] = "netherite"
        };

        foreach (var (item, material) in expected)
        {
            var texture = EquipmentTextureCatalog.Resolve(EquipmentAssetKind.HorseBody, Item(item));
            Assert.NotNull(texture);
            Assert.Equal("entity/equipment/horse_body/" + material, texture.PrimaryPath);
            Assert.Equal(material == "leather" ? "entity/equipment/horse_body/leather_overlay" : null, texture.OverlayPath);
        }
    }

    [Fact]
    public void SaddleItemsMapPerEntityFamily()
    {
        var saddle = Item("saddle");
        Assert.Equal("entity/equipment/horse_saddle/saddle", EquipmentTextureCatalog.Resolve(EquipmentAssetKind.HorseSaddle, saddle)!.PrimaryPath);
        Assert.Equal("entity/equipment/donkey_saddle/saddle", EquipmentTextureCatalog.Resolve(EquipmentAssetKind.DonkeySaddle, saddle)!.PrimaryPath);
        Assert.Equal("entity/equipment/mule_saddle/saddle", EquipmentTextureCatalog.Resolve(EquipmentAssetKind.MuleSaddle, saddle)!.PrimaryPath);
        Assert.Equal("entity/equipment/skeleton_horse_saddle/saddle", EquipmentTextureCatalog.Resolve(EquipmentAssetKind.SkeletonHorseSaddle, saddle)!.PrimaryPath);
        Assert.Equal("entity/equipment/zombie_horse_saddle/saddle", EquipmentTextureCatalog.Resolve(EquipmentAssetKind.ZombieHorseSaddle, saddle)!.PrimaryPath);
        Assert.Equal("entity/equipment/camel_saddle/saddle", EquipmentTextureCatalog.Resolve(EquipmentAssetKind.CamelSaddle, saddle)!.PrimaryPath);
        Assert.Equal("entity/equipment/pig_saddle/saddle", EquipmentTextureCatalog.Resolve(EquipmentAssetKind.PigSaddle, saddle)!.PrimaryPath);
        Assert.Equal("entity/equipment/strider_saddle/saddle", EquipmentTextureCatalog.Resolve(EquipmentAssetKind.StriderSaddle, saddle)!.PrimaryPath);
        Assert.Equal("entity/equipment/nautilus_saddle/saddle", EquipmentTextureCatalog.Resolve(EquipmentAssetKind.NautilusSaddle, saddle)!.PrimaryPath);
    }

    [Fact]
    public void LlamaCarpetsAndHappyGhastHarnessesCoverAllSixteenColors()
    {
        Assert.Equal(16, EquipmentTextureCatalog.LlamaCarpetItems.Count());
        Assert.Equal(16, EquipmentTextureCatalog.HappyGhastHarnessItems.Count());

        foreach (var color in EquipmentTextureCatalog.LlamaCarpetItems.Select(item => item[..^"_carpet".Length]))
        {
            Assert.Equal("entity/equipment/llama_body/" + color,
                EquipmentTextureCatalog.Resolve(EquipmentAssetKind.LlamaBody, Item(color + "_carpet"))!.PrimaryPath);
            Assert.Equal("entity/equipment/happy_ghast_body/" + color + "_harness",
                EquipmentTextureCatalog.Resolve(EquipmentAssetKind.HappyGhastBody, Item(color + "_harness"))!.PrimaryPath);
        }
    }

    [Fact]
    public void NautilusArmorAndWolfArmorResolve()
    {
        Assert.Equal("entity/equipment/nautilus_body/iron",
            EquipmentTextureCatalog.Resolve(EquipmentAssetKind.NautilusBody, Item("iron_nautilus_armor"))!.PrimaryPath);
        Assert.Equal("entity/equipment/nautilus_body/copper",
            EquipmentTextureCatalog.Resolve(EquipmentAssetKind.NautilusBody, Item("copper_nautilus_armor"))!.PrimaryPath);

        var wolf = EquipmentTextureCatalog.Resolve(EquipmentAssetKind.WolfBody, Item("wolf_armor"));
        Assert.Equal("entity/equipment/wolf_body/armadillo_scute", wolf!.PrimaryPath);
        Assert.Equal("entity/equipment/wolf_body/armadillo_scute_overlay", wolf.OverlayPath);
    }

    [Fact]
    public void UnsupportedItemsReturnNull()
    {
        Assert.Null(EquipmentTextureCatalog.Resolve(EquipmentAssetKind.HorseBody, Item("diamond")));
        Assert.Null(EquipmentTextureCatalog.Resolve(EquipmentAssetKind.LlamaBody, Item("white_wool")));
        Assert.Null(EquipmentTextureCatalog.Resolve(EquipmentAssetKind.WolfBody, Item("iron_horse_armor")));
    }
}
