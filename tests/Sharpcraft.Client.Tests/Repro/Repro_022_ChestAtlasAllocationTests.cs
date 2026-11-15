// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;

namespace Sharpcraft.Client.Tests.Repro;

/// <summary>
/// Issue 22 (B2): the chest atlas path lookup runs once per chest per frame, so it has to return a
/// precomputed string instead of building a new one every call.
/// </summary>
public sealed class Repro_022_ChestAtlasAllocationTests
{
    [Theory]
    [InlineData(ChestAtlas.Normal, 0, "entity/chest/normal")]
    [InlineData(ChestAtlas.Normal, 1, "entity/chest/normal_left")]
    [InlineData(ChestAtlas.Normal, 2, "entity/chest/normal_right")]
    [InlineData(ChestAtlas.Trapped, 1, "entity/chest/trapped_left")]
    [InlineData(ChestAtlas.Ender, 0, "entity/chest/ender")]
    [InlineData(ChestAtlas.Ender, 1, "entity/chest/ender")]
    [InlineData(ChestAtlas.Ender, 2, "entity/chest/ender")]
    [InlineData(ChestAtlas.Copper, 2, "entity/chest/copper_right")]
    [InlineData(ChestAtlas.CopperExposed, 1, "entity/chest/copper_exposed_left")]
    [InlineData(ChestAtlas.CopperWeathered, 0, "entity/chest/copper_weathered")]
    [InlineData(ChestAtlas.CopperOxidized, 2, "entity/chest/copper_oxidized_right")]
    public void EveryMaterialAndHalfResolvesToTheExpectedPath(byte material, byte type, string expected)
    {
        Assert.Equal(expected, ChestAtlas.TexturePathFor(material, type));
    }

    [Fact]
    public void TheLookupDoesNotAllocate()
    {
        // Warm up the static table and the JIT before measuring.
        for (var i = 0; i < 256; i++)
        {
            _ = ChestAtlas.TexturePathFor((byte)(i % 7), (byte)(i % 3));
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 10_000; i++)
        {
            _ = ChestAtlas.TexturePathFor((byte)(i % 7), (byte)(i % 3));
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
    }
}
