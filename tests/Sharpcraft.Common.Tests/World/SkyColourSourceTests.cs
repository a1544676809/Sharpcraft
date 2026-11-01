// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Linq;
using System.Numerics;
using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Nbt;
using Sharpcraft.Common.Network;
using Sharpcraft.Common.World;
using Xunit;

namespace Sharpcraft.Common.Tests.World;

/// <summary>SKY_COLOR at a position = calculateSkyColor(temperature) blended over nearby biomes (biome-data.md 4).</summary>
public sealed class SkyColourSourceTests
{
    private static NbtCompound Biome(float temperature) => new NbtCompound()
        .Set("has_precipitation", new NbtByte((sbyte)1))
        .Set("temperature", new NbtFloat(temperature))
        .Set("downfall", new NbtFloat(1f))
        .Set("effects", new NbtCompound().Set("water_color", new NbtString("3F76E4")));

    private static BiomeRegistry Registry(params (string Id, float Temperature)[] biomes)
    {
        var registries = new DynamicRegistries();
        registries.Apply(BiomeRegistry.RegistryId,
            biomes.Select(b => new DynamicRegistries.Entry(new Identifier("minecraft", b.Id), Biome(b.Temperature))).ToArray());
        return BiomeRegistry.From(registries)!;
    }

    [Fact]
    public void AUniformBiomeYieldsItsSkyColour()
    {
        var registry = Registry(("a", 0.5f));
        var colour = SkyColourSource.ColorAt((x, y, z) => 0, registry, 8, 64, 8);

        var packed = SkyColour.FromTemperature(0.5f);
        Assert.Equal(((packed >> 16) & 0xFF) / 255f, colour.X, 2);
        Assert.Equal(((packed >> 8) & 0xFF) / 255f, colour.Y, 2);
        Assert.Equal((packed & 0xFF) / 255f, colour.Z, 2);
    }

    [Fact]
    public void UnknownBiomesContributeNothing()
    {
        var registry = Registry(("a", 0.5f));
        Assert.Equal(Vector3.Zero, SkyColourSource.ColorAt((x, y, z) => -1, registry, 8, 64, 8));
    }

    [Fact]
    public void WarmAndFrozenBiomesDiffer()
    {
        var warm = SkyColourSource.ColorAt((x, y, z) => 0, Registry(("warm", 1.0f)), 8, 64, 8);
        var frozen = SkyColourSource.ColorAt((x, y, z) => 0, Registry(("frozen", -1.0f)), 8, 64, 8);

        Assert.NotEqual(warm, frozen);
    }
}
