// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Client.World;
using Sharpcraft.Common.Imaging;
using Sharpcraft.Common.Registries;
using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>Trim registry order, palette mapping and the humanoid trim render layer.</summary>
public sealed class TrimTextureTests
{
    [Fact]
    public void BootstrapRegistriesExposeTheReferenceOrder()
    {
        Assert.Equal(11, TrimMaterialRegistry.Entries.Count);
        Assert.Equal("minecraft:quartz", TrimMaterialRegistry.Entries[0].Id);
        Assert.Equal("minecraft:resin", TrimMaterialRegistry.Entries[^1].Id);
        Assert.Equal("minecraft:trim/iron", TrimMaterialRegistry.PaletteIdFor("minecraft:iron"));

        Assert.Equal(18, TrimPatternRegistry.Entries.Count);
        Assert.Equal("minecraft:sentry", TrimPatternRegistry.Entries[0].Id);
        Assert.Equal("minecraft:bolt", TrimPatternRegistry.Entries[^1].Id);
        Assert.Equal("minecraft:coast", TrimPatternRegistry.ForId("minecraft:coast")?.AssetId);
    }

    [Fact]
    public void PaletteMappingReplacesOpaqueColorsAndKeepsAlpha()
    {
        // Base palette values and a matching target palette, with a transparent trim texel.
        var basePalette = Texture(8, 1, [
            224, 224, 224, 255, 192, 192, 192, 255, 160, 160, 160, 255, 128, 128, 128, 255,
            96, 96, 96, 255, 64, 64, 64, 255, 32, 32, 32, 255, 0, 0, 0, 255
        ]);
        var targetPalette = Texture(8, 1, [
            197, 210, 212, 255, 191, 201, 200, 255, 157, 170, 170, 255, 123, 137, 137, 255,
            113, 125, 125, 255, 101, 112, 112, 255, 87, 99, 99, 255, 70, 81, 81, 255
        ]);
        var trim = Texture(2, 1, [224, 224, 224, 255, 0, 0, 0, 0]);

        var mapped = TrimTextureCatalog.MapPalette(basePalette, targetPalette, trim);

        Assert.NotNull(mapped);
        Assert.Equal((197, 210, 212, 255), Pixel(mapped!, 0));
        Assert.Equal((0, 0, 0, 0), Pixel(mapped!, 1));
    }

    [Fact]
    public void CatalogLoadsBothHumanoidLayersFromResources()
    {
        var root = Path.Combine(Path.GetTempPath(), "sharpcraft-trim-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var basePalette = Texture(8, 1, [
                224, 224, 224, 255, 192, 192, 192, 255, 160, 160, 160, 255, 128, 128, 128, 255,
                96, 96, 96, 255, 64, 64, 64, 255, 32, 32, 32, 255, 0, 0, 0, 255
            ]);
            var ironPalette = Texture(8, 1, [
                197, 210, 212, 255, 191, 201, 200, 255, 157, 170, 170, 255, 123, 137, 137, 255,
                113, 125, 125, 255, 101, 112, 112, 255, 87, 99, 99, 255, 70, 81, 81, 255
            ]);
            var pattern = Texture(2, 1, [224, 224, 224, 255, 0, 0, 0, 0]);
            Write(root, "assets/minecraft/textures/palettes/trim_base.png", basePalette);
            Write(root, "assets/minecraft/textures/palettes/trim/iron.png", ironPalette);
            Write(root, "assets/minecraft/textures/trims/entity/humanoid/coast.png", pattern);
            Write(root, "assets/minecraft/textures/trims/entity/humanoid_leggings/coast.png", pattern);

            using var stack = new AssetStack { Policy = MissingResourcePolicy.Warn, Warn = _ => { } }
                .Add(new DirectoryAssetSource(root, "trim-test"));
            using var resources = new ResourceManager(stack, 1);
            Assert.NotNull(resources.Read(ResourceKind.Texture, new Sharpcraft.Common.Identifiers.Identifier("minecraft", "palettes/trim_base")));
            Assert.NotNull(resources.Read(ResourceKind.Texture, new Sharpcraft.Common.Identifiers.Identifier("minecraft", "palettes/trim/iron")));
            Assert.NotNull(resources.Read(ResourceKind.Texture, new Sharpcraft.Common.Identifiers.Identifier("minecraft", "trims/entity/humanoid/coast")));
            var baseDecoded = PngDecoder.Decode(resources.Read(ResourceKind.Texture, new Sharpcraft.Common.Identifiers.Identifier("minecraft", "palettes/trim_base"))!);
            var ironDecoded = PngDecoder.Decode(resources.Read(ResourceKind.Texture, new Sharpcraft.Common.Identifiers.Identifier("minecraft", "palettes/trim/iron"))!);
            var coastDecoded = PngDecoder.Decode(resources.Read(ResourceKind.Texture, new Sharpcraft.Common.Identifiers.Identifier("minecraft", "trims/entity/humanoid/coast"))!);
            Assert.NotNull(TrimTextureCatalog.MapPalette(
                new TextureData(baseDecoded.Width, baseDecoded.Height, baseDecoded.Pixels),
                new TextureData(ironDecoded.Width, ironDecoded.Height, ironDecoded.Pixels),
                new TextureData(coastDecoded.Width, coastDecoded.Height, coastDecoded.Pixels)));
            var textures = TrimTextureCatalog.Load(resources);

            Assert.Contains("trims/entity/humanoid/coast/iron", textures.Keys);
            Assert.Contains("trims/entity/humanoid_leggings/coast/iron", textures.Keys);
            Assert.Equal((197, 210, 212, 255), Pixel(textures["trims/entity/humanoid/coast/iron"], 0));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void TrimLayerUsesThePaletteMappedTextureAndIsHiddenWithoutTrim()
    {
        var trimmed = new EntityRenderState(
            1,
            HumanoidMobModelDefinition.ZombieEntityTypeId,
            0f, 0f, 0f, 0f, 0f,
            Equipment: new EntityEquipment(-1, -1, -1, -1, 9999, -1, -1, -1),
            EquipmentTrimMaterials: [null, null, null, null, "minecraft:iron", null, null, null],
            EquipmentTrimPatterns: [null, null, null, null, "minecraft:coast", null, null, null]);

        Assert.Equal("trims/entity/humanoid/coast/iron",
            EntityModelResolver.ResolveTrimTexture(trimmed, EquipmentLayerType.Humanoid, 4));
        var layers = new EntityModelMesher().BuildLayers(
            [trimmed], new Sharpcraft.Client.Assets.SpriteRect(0, 0, 64, 64, 1), 64, 64,
            variants: EntityVariantTable.Builtin);
        Assert.Contains(layers, layer => layer.Name == "trim_HumanoidArmor_4");

        var plain = trimmed with
        {
            EquipmentTrimMaterials = null,
            EquipmentTrimPatterns = null
        };
        Assert.Null(EntityModelResolver.ResolveTrimTexture(plain, EquipmentLayerType.Humanoid, 4));
        Assert.DoesNotContain(
            new EntityModelMesher().BuildLayers([plain], new Sharpcraft.Client.Assets.SpriteRect(0, 0, 64, 64, 1), 64, 64,
                variants: EntityVariantTable.Builtin),
            layer => layer.Name == "trim_HumanoidArmor_4");
    }

    private static void Write(string root, string relativePath, TextureData texture)
    {
        var path = Path.Combine(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, PngEncoder.EncodeRgba(texture.Width, texture.Height, texture.Rgba));
    }

    private static TextureData Texture(int width, int height, int[] rgba)
    {
        var bytes = new byte[rgba.Length];
        for (var i = 0; i < rgba.Length; i++)
        {
            bytes[i] = (byte)rgba[i];
        }

        return new TextureData(width, height, bytes);
    }

    private static (byte R, byte G, byte B, byte A) Pixel(TextureData texture, int x)
    {
        var offset = x * 4;
        return (texture.Rgba[offset], texture.Rgba[offset + 1], texture.Rgba[offset + 2], texture.Rgba[offset + 3]);
    }
}
