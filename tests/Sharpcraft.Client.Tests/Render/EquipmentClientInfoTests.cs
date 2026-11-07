// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Text;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Registries;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

public sealed class EquipmentClientInfoTests
{
    [Fact]
    public void ParsesLayersAndTrimOverride()
    {
        const string json = """
            {
              "layers": {
                "horse_body": [{ "texture": "minecraft:iron" }],
                "humanoid": [{ "texture": "minecraft:iron" }]
              },
              "trim_overrides": [{
                "palette": "minecraft:trim/iron_darker",
                "when": { "material": "minecraft:iron" }
              }]
            }
            """;

        var info = EquipmentClientInfo.Parse(Encoding.UTF8.GetBytes(json));

        Assert.Equal(2, info.Layers.Count);
        Assert.Equal(new Identifier("minecraft", "iron"), Assert.Single(info.GetLayers(EquipmentLayerType.HorseBody)).Texture);
        Assert.Single(info.TrimOverrides);
        Assert.Equal(new Identifier("minecraft", "trim/iron_darker"), info.TrimOverrides[0].Palette);
        Assert.Equal(new Identifier("minecraft", "iron"), info.TrimOverrides[0].When.Material);
    }

    [Fact]
    public void ResolvesTrimOverrideByMaterialAndPattern()
    {
        const string json = """
            {
              "layers": { "humanoid": [{ "texture": "minecraft:iron" }] },
              "trim_overrides": [
                { "palette": "minecraft:trim/iron_darker", "when": { "material": "minecraft:iron" } },
                { "palette": "minecraft:trim/gold_darker", "when": { "pattern": "minecraft:spire" } }
              ]
            }
            """;
        var info = EquipmentClientInfo.Parse(Encoding.UTF8.GetBytes(json));
        var iron = new Identifier("minecraft", "iron");

        Assert.Equal(new Identifier("minecraft", "trim/iron_darker"), info.ResolveTrim(iron, null)?.Palette);
        Assert.Null(info.ResolveTrim(new Identifier("minecraft", "gold"), null));
        Assert.Equal(
            new Identifier("minecraft", "trim/gold_darker"),
            info.ResolveTrim(null, new Identifier("minecraft", "spire"))?.Palette);
    }

    [Fact]
    public void ParsesDyeableAndPlayerTextureFlags()
    {
        const string json = """
            {
              "layers": {
                "humanoid": [
                  { "texture": "minecraft:leather", "dyeable": { "color_when_undyed": -6265536 } },
                  { "texture": "minecraft:leather_overlay" }
                ],
                "wings": [{ "texture": "minecraft:elytra", "use_player_texture": true }]
              }
            }
            """;

        var info = EquipmentClientInfo.Parse(Encoding.UTF8.GetBytes(json));
        var humanoid = info.GetLayers(EquipmentLayerType.Humanoid);

        Assert.Equal(2, humanoid.Count);
        Assert.Equal(-6265536, humanoid[0].Dyeable?.ColourWhenUndyed);
        Assert.Null(humanoid[1].Dyeable);
        Assert.True(Assert.Single(info.GetLayers(EquipmentLayerType.Wings)).UsePlayerTexture);
    }

    [Fact]
    public void ParsesEverySaddleLayerFamily()
    {
        const string json = """
            {
              "layers": {
                "camel_saddle": [{ "texture": "minecraft:saddle" }],
                "camel_husk_saddle": [{ "texture": "minecraft:saddle" }],
                "donkey_saddle": [{ "texture": "minecraft:saddle" }],
                "horse_saddle": [{ "texture": "minecraft:saddle" }],
                "mule_saddle": [{ "texture": "minecraft:saddle" }],
                "nautilus_saddle": [{ "texture": "minecraft:saddle" }],
                "pig_saddle": [{ "texture": "minecraft:saddle" }],
                "skeleton_horse_saddle": [{ "texture": "minecraft:saddle" }],
                "strider_saddle": [{ "texture": "minecraft:saddle" }],
                "zombie_horse_saddle": [{ "texture": "minecraft:saddle" }]
              }
            }
            """;

        var info = EquipmentClientInfo.Parse(Encoding.UTF8.GetBytes(json));

        Assert.Equal(10, info.Layers.Count);
        Assert.Equal(19, Enum.GetValues<EquipmentLayerType>().Length);
        Assert.Equal("minecraft:saddle", Assert.Single(info.GetLayers(EquipmentLayerType.CamelSaddle)).Texture.ToString());
    }

    [Fact]
    public void EquipmentResourcesUseTheEquipmentDirectory()
    {
        Assert.Equal(
            "assets/minecraft/equipment/iron.json",
            ResourcePath.FilePath(ResourceKind.Equipment, new Identifier("minecraft", "iron")));
    }

    [Fact]
    public void ResolvesTexturesFromParsedAssetsBeforeStaticFallback()
    {
        const string json = """
            {
              "layers": {
                "humanoid": [{ "texture": "minecraft:iron" }],
                "horse_saddle": [{ "texture": "minecraft:saddle" }]
              }
            }
            """;
        var info = EquipmentClientInfo.Parse(Encoding.UTF8.GetBytes(json));
        var previous = EquipmentTextureCatalog.Assets;
        EquipmentTextureCatalog.Assets = EquipmentAssetRegistry.From(
            new Dictionary<Identifier, EquipmentClientInfo>
            {
                [new Identifier("minecraft", "iron")] = info,
                [new Identifier("minecraft", "saddle")] = info
            });
        try
        {
            var armor = EquipmentTextureCatalog.Resolve(
                EquipmentAssetKind.HumanoidArmor,
                new ItemInfo(new Identifier("minecraft", "iron_helmet")));
            Assert.Equal("entity/equipment/humanoid/iron", armor?.PrimaryPath);

            var saddle = EquipmentTextureCatalog.Resolve(
                EquipmentAssetKind.HorseSaddle,
                new ItemInfo(new Identifier("minecraft", "saddle")));
            Assert.Equal("entity/equipment/horse_saddle/saddle", saddle?.PrimaryPath);

            var custom = EquipmentClientInfo.Parse(Encoding.UTF8.GetBytes(
                """{"layers":{"humanoid":[{"texture":"minecraft:emerald"}]}}"""));
            EquipmentTextureCatalog.Assets = EquipmentAssetRegistry.From(
                new Dictionary<Identifier, EquipmentClientInfo>
                {
                    [new Identifier("minecraft", "custom_armor")] = custom
                });
            var overridden = EquipmentTextureCatalog.Resolve(
                EquipmentAssetKind.HumanoidArmor,
                new ItemInfo(new Identifier("minecraft", "iron_helmet")),
                equippableAssetId: "minecraft:custom_armor");
            Assert.Equal("entity/equipment/humanoid/emerald", overridden?.PrimaryPath);
        }
        finally
        {
            EquipmentTextureCatalog.Assets = previous;
        }
    }

    [Fact]
    public void AssetRegistryExposesEquipmentLayerTexturePaths()
    {
        const string json = """
            {
              "layers": {
                "humanoid": [{ "texture": "minecraft:custom_tex" }]
              }
            }
            """;
        var info = EquipmentClientInfo.Parse(Encoding.UTF8.GetBytes(json));
        var registry = EquipmentAssetRegistry.From(
            new Dictionary<Identifier, EquipmentClientInfo>
            {
                [new Identifier("minecraft", "custom_armor")] = info
            });

        Assert.Contains("entity/equipment/humanoid/custom_tex", registry.TexturePaths);
    }

    [Fact]
    public void ParsedDyeableAndPlayerTextureMetadataReachResolvedTexture()
    {
        const string json = """
            {
              "layers": {
                "humanoid": [{ "texture": "minecraft:leather", "dyeable": { "color_when_undyed": -6265536 } }],
                "wings": [{ "texture": "minecraft:elytra", "use_player_texture": true }]
              }
            }
            """;
        var info = EquipmentClientInfo.Parse(Encoding.UTF8.GetBytes(json));
        var previous = EquipmentTextureCatalog.Assets;
        EquipmentTextureCatalog.Assets = EquipmentAssetRegistry.From(
            new Dictionary<Identifier, EquipmentClientInfo>
            {
                [new Identifier("minecraft", "leather")] = info,
                [new Identifier("minecraft", "elytra")] = info
            });
        try
        {
            var leather = EquipmentTextureCatalog.Resolve(
                EquipmentAssetKind.HumanoidArmor,
                new ItemInfo(new Identifier("minecraft", "leather_helmet")));
            Assert.Equal(-6265536, leather?.ColourWhenUndyed);

            var elytra = EquipmentTextureCatalog.Resolve(
                EquipmentAssetKind.Elytra,
                new ItemInfo(new Identifier("minecraft", "elytra")),
                glintOverride: true);
            Assert.True(elytra?.UsePlayerTexture);
            Assert.True(elytra?.Glint);
        }
        finally
        {
            EquipmentTextureCatalog.Assets = previous;
        }
    }

    [Fact]
    public void ResolvedEquipmentTextureInfoCarriesDyeMetadata()
    {
        const string json = """
            {
              "layers": {
                "humanoid": [
                  { "texture": "minecraft:leather", "dyeable": { "color_when_undyed": -6265536 } },
                  { "texture": "minecraft:leather_overlay" }
                ]
              }
            }
            """;
        var info = EquipmentClientInfo.Parse(Encoding.UTF8.GetBytes(json));
        var previous = EquipmentTextureCatalog.Assets;
        EquipmentTextureCatalog.Assets = EquipmentAssetRegistry.From(
            new Dictionary<Identifier, EquipmentClientInfo>
            {
                [new Identifier("minecraft", "leather")] = info
            });
        try
        {
            var helmet = ItemRegistry.LoadEmbedded().GetId(Identifier.Parse("minecraft:leather_helmet"));
            Assert.True(helmet >= 0, "leather helmet item id missing from embedded registry");

            var state = new EntityRenderState(1, 154, 0f, 0f, 0f, 0f, 0f,
                Equipment: new EntityEquipment(-1, -1, -1, -1, -1, helmet, -1, -1),
                EquipmentDyeColors: new int?[] { null, null, null, null, null, 0x3F2A1B, null, null });

            var texture = EntityModelResolver.ResolveEquipmentTextureInfo(
                state, EquipmentAssetKind.HumanoidArmor, 5);
            Assert.NotNull(texture);
            Assert.Equal("entity/equipment/humanoid/leather", texture!.PrimaryPath);
            Assert.Equal("entity/equipment/humanoid/leather_overlay", texture.OverlayPath);
            Assert.Equal(-6265536, texture.ColourWhenUndyed);

            // ResolveEquipmentTexture stays a thin selector over the same resolved info (behaviour unchanged).
            Assert.Equal(texture.PrimaryPath,
                EntityModelResolver.ResolveEquipmentTexture(state, EquipmentAssetKind.HumanoidArmor, 5));
            Assert.Equal(texture.OverlayPath,
                EntityModelResolver.ResolveEquipmentTexture(state, EquipmentAssetKind.HumanoidArmor, 5, overlay: true));
        }
        finally
        {
            EquipmentTextureCatalog.Assets = previous;
        }
    }

    [Fact]
    public void RejectsUnknownLayerAndMissingTexture()
    {
        Assert.Throws<InvalidDataException>(() =>
            EquipmentClientInfo.Parse(Encoding.UTF8.GetBytes("""{"layers":{"not_a_layer":[{"texture":"minecraft:iron"}]}}""")));
        Assert.Throws<InvalidDataException>(() =>
            EquipmentClientInfo.Parse(Encoding.UTF8.GetBytes("""{"layers":{"humanoid":[{}]}}""")));
    }
}
