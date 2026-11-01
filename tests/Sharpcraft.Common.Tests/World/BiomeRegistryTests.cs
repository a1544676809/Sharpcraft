// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Nbt;
using Sharpcraft.Common.Network;
using Sharpcraft.Common.World;
using Xunit;

namespace Sharpcraft.Common.Tests.World;

/// <summary>Decodes biome registry entries (docs/analysis/common/biome-data.md 2) from their NBT payload.</summary>
public sealed class BiomeRegistryTests
{
    private static NbtCompound SampleBiome() => new NbtCompound()
        .Set("has_precipitation", new NbtByte((sbyte)1))
        .Set("temperature", new NbtFloat(0.8f))
        .Set("downfall", new NbtFloat(0.4f))
        .Set("effects", new NbtCompound()
            .Set("water_color", new NbtString("3F76E4"))
            .Set("grass_color", new NbtString("79C05A"))
            .Set("grass_color_modifier", new NbtString("swamp")));

    [Fact]
    public void DecodesClimateAndColours()
    {
        var entry = BiomeRegistry.Parse(new Identifier("minecraft", "swamp"), SampleBiome())!;

        Assert.Equal(0.8f, entry.Climate.Temperature, 4);
        Assert.Equal(0.4f, entry.Climate.Downfall, 4);
        Assert.True(entry.Climate.HasPrecipitation);
        Assert.Equal(unchecked((int)0xFF3F76E4), entry.Effects.WaterColor);
        Assert.Equal(unchecked((int)0xFF79C05A), entry.Effects.GrassColor);
        Assert.Null(entry.Effects.FoliageColor);
        Assert.Equal(GrassColourModifier.Swamp, entry.Effects.GrassColorModifier);
    }

    [Fact]
    public void ParsesHexColours()
    {
        Assert.Equal(unchecked((int)0xFF3F76E4), BiomeRegistry.ParseColour("3F76E4"));
        Assert.Equal(unchecked((int)0xFF3F76E4), BiomeRegistry.ParseColour("#3F76E4"));
        Assert.Null(BiomeRegistry.ParseColourOrNull("zzz"));
        Assert.Equal(-1, BiomeRegistry.ParseColour(null, -1));
    }

    [Fact]
    public void MissingPayloadYieldsNull()
    {
        Assert.Null(BiomeRegistry.Parse(new Identifier("minecraft", "plains"), null));
    }

    [Fact]
    public void BuildsFromRegistriesAndResolvesById()
    {
        var registries = new DynamicRegistries();
        Assert.Null(BiomeRegistry.From(registries));

        registries.Apply(BiomeRegistry.RegistryId,
        [
            new DynamicRegistries.Entry(new Identifier("minecraft", "plains"), SampleBiome())
        ]);

        var registry = BiomeRegistry.From(registries)!;
        Assert.Single(registry.Entries);
        Assert.Equal("minecraft:plains", registry.ById(0)!.Id.ToString());
        Assert.Null(registry.ById(5));
    }
}
