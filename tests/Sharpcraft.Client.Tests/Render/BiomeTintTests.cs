// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Nbt;
using Sharpcraft.Common.Network;
using Sharpcraft.Common.World;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Biome tint groundwork (docs/specs/render/block-layer-and-tint.md, A3): the colour map is indexed exactly like the
/// reference, and the tint table follows the reference's block registrations.
/// </summary>
public sealed class BiomeTintTests
{
    [Fact]
    public void TheColourMapIsIndexedLikeTheReference()
    {
        // Mirror of ColorMapColorUtil.get: rain *= temp, x = (1 - temp) * 255, y = (1 - rain) * 255, index = (y << 8) | x.
        var temperature = 0.5;
        var rainfall = 1.0;
        var rain = rainfall * temperature;
        var x = (int)((1.0 - temperature) * 255.0);
        var y = (int)((1.0 - rain) * 255.0);

        Assert.Equal(127, x);
        Assert.Equal(127, y);
        Assert.Equal((127 << 8) | 127, 32639);
    }

    [Fact]
    public void GrassTintsTheTopFaceOnly()
    {
        Assert.True(BlockTintTable.TintsTopFaceOnly(BlockTintKind.Grass));
        Assert.False(BlockTintTable.TintsTopFaceOnly(BlockTintKind.Foliage));
    }

    [Theory]
    [InlineData("grass_block", BlockTintKind.Grass)]
    [InlineData("short_grass", BlockTintKind.Grass)]
    [InlineData("oak_leaves", BlockTintKind.Foliage)]
    [InlineData("spruce_leaves", BlockTintKind.FoliageEvergreen)]
    [InlineData("birch_leaves", BlockTintKind.FoliageBirch)]
    [InlineData("water", BlockTintKind.Water)]
    [InlineData("stone", BlockTintKind.None)]
    public void TheTintTableMatchesTheReferenceRegistrations(string path, BlockTintKind expected)
    {
        Assert.Equal(expected, BlockTintTable.KindOf(new Identifier("minecraft", path)));
    }

    [Fact]
    public void NoTintLeavesTheMultiplierAtOne()
    {
        var (red, green, blue) = BiomeTint.ToMultiplier(-1);

        Assert.Equal(1f, red);
        Assert.Equal(1f, green);
        Assert.Equal(1f, blue);
    }

    [Fact]
    public void NegativeArgbIsStillATint()
    {
        // The reference foliage default is 0xFF48B518, which is negative as a signed int; it must not read as "no tint".
        var (red, green, blue) = BiomeTint.ToMultiplier(BiomeTint.FoliageDefault);

        Assert.Equal(0x48 / 255f, red, 4);
        Assert.Equal(0xB5 / 255f, green, 4);
        Assert.Equal(0x18 / 255f, blue, 4);
    }

    [Fact]
    public void ATintBecomesAUnitMultiplier()
    {
        var (red, green, blue) = BiomeTint.ToMultiplier(0x80_40_20);

        Assert.Equal(128f / 255f, red, 4);
        Assert.Equal(64f / 255f, green, 4);
        Assert.Equal(32f / 255f, blue, 4);
    }

    [Fact]
    public void TheReferenceConstantsArePreserved()
    {
        Assert.Equal(-12012264, BiomeTint.FoliageDefault);
        Assert.Equal(-10380959, BiomeTint.FoliageEvergreen);
        Assert.Equal(-8345771, BiomeTint.FoliageBirch);
        Assert.Equal(4159204, BiomeTint.WaterDefault);
    }

    private static NbtCompound Biome(string grass, string water, string? modifier, float temperature)
    {
        var effects = new NbtCompound()
            .Set("water_color", new NbtString(water))
            .Set("grass_color", new NbtString(grass));
        if (modifier is not null)
        {
            effects.Set("grass_color_modifier", new NbtString(modifier));
        }

        return new NbtCompound()
            .Set("has_precipitation", new NbtByte((sbyte)1))
            .Set("temperature", new NbtFloat(temperature))
            .Set("downfall", new NbtFloat(1f))
            .Set("effects", effects);
    }

    private static BiomeRegistry Registry(
        string grass0, string water0, string? mod0, float temp0,
        string grass1, string water1, string? mod1, float temp1)
    {
        var registries = new DynamicRegistries();
        registries.Apply(BiomeRegistry.RegistryId,
        [
            new DynamicRegistries.Entry(new Identifier("minecraft", "a"), Biome(grass0, water0, mod0, temp0)),
            new DynamicRegistries.Entry(new Identifier("minecraft", "b"), Biome(grass1, water1, mod1, temp1))
        ]);
        return BiomeRegistry.From(registries)!;
    }

    [Fact]
    public void ResolvesGrassAndWaterPerBiome()
    {
        var registry = Registry("112233", "3F76E4", null, 0.5f, "445566", "ABCDEF", null, 0.5f);

        var biomeA = BiomeTint.FromColormaps(null, null).WithBiomes((x, y, z) => 0, registry, blendRadius: 0);
        var biomeB = BiomeTint.FromColormaps(null, null).WithBiomes((x, y, z) => 1, registry, blendRadius: 0);

        Assert.Equal(unchecked((int)0xFF112233), biomeA.ColourAt(BlockTintKind.Grass, 5, 64, 5));
        Assert.Equal(unchecked((int)0xFF445566), biomeB.ColourAt(BlockTintKind.Grass, 5, 64, 5));
        Assert.Equal(unchecked((int)0xFFABCDEF), biomeB.ColourAt(BlockTintKind.Water, 5, 64, 5));
    }

    [Fact]
    public void DarkForestModifierIsApplied()
    {
        var registry = Registry("808080", "3F76E4", null, 0.5f, "445566", "ABCDEF", "dark_forest", 0.5f);
        var tint = BiomeTint.FromColormaps(null, null).WithBiomes((x, y, z) => 1, registry, blendRadius: 0);

        var expected = unchecked((int)0xFF000000) | ((((0xFF445566 & 0x00FEFEFE) + 2634762) >> 1) & 0xFFFFFF);
        Assert.Equal(expected, tint.ColourAt(BlockTintKind.Grass, 5, 64, 5));
    }

    [Fact]
    public void BlendRadiusAveragesTheBiomeSquare()
    {
        // Biome 0 grass black (0), biome 1 grass grey 0x64 (100); id 0 only at x==0, else 1.
        var registry = Registry("000000", "3F76E4", null, 0.5f, "646464", "3F76E4", null, 0.5f);
        var tint = BiomeTint.FromColormaps(null, null).WithBiomes((x, y, z) => x == 0 ? 0 : 1, registry, blendRadius: 1);

        // 3x3 square around x=0: columns x=-1,0,1 -> 100,0,100 per z row, so R = 3*(100+0+100)/9 = 66.
        Assert.Equal(66, (tint.ColourAt(BlockTintKind.Grass, 0, 64, 0) >> 16) & 0xFF);
    }

    [Fact]
    public void TemperatureIsClampedBeforeColourMapLookup()
    {
        // Identity colour map: pixel[i] = i. A biome temperature of 2 clamps to 1 -> index 0 -> colour 0 (black),
        // whereas the unclamped index would be out of range and return the magenta fallback -65281.
        var pixels = new int[Colormap.PixelCount];
        for (var i = 0; i < pixels.Length; i++)
        {
            pixels[i] = i;
        }

        var biome = new NbtCompound()
            .Set("has_precipitation", new NbtByte((sbyte)1))
            .Set("temperature", new NbtFloat(2f))
            .Set("downfall", new NbtFloat(1f))
            .Set("effects", new NbtCompound().Set("water_color", new NbtString("3F76E4")));
        var registries = new DynamicRegistries();
        registries.Apply(BiomeRegistry.RegistryId, [new DynamicRegistries.Entry(new Identifier("minecraft", "hot"), biome)]);
        var registry = BiomeRegistry.From(registries)!;

        var tint = BiomeTint.FromColormaps(Colormap.FromPixels(pixels), null).WithBiomes((x, y, z) => 0, registry, blendRadius: 0);
        var colour = tint.ColourAt(BlockTintKind.Grass, 5, 64, 5);

        Assert.Equal(0, colour);            // index 0 in the identity map
        Assert.NotEqual(-65281, colour);    // not the magenta out-of-range fallback
    }
}
