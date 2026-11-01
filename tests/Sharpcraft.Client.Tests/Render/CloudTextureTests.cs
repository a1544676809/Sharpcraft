// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Assets;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>Cloud texture packing: alpha threshold and neighbour flags (docs/modules/clouds.md 2).</summary>
public sealed class CloudTextureTests
{
    [Fact]
    public void CellsBelowTheAlphaThresholdAreEmpty()
    {
        // 1x1 all transparent -> empty; alpha 9 is empty, alpha 10 is a cloud.
        Assert.Equal(0, CloudTexture.FromPixels(1, 1, [0x00000000]).Cells[0]);
        Assert.Equal(0, CloudTexture.FromPixels(1, 1, [0x09000000]).Cells[0]);
        Assert.NotEqual(0, CloudTexture.FromPixels(1, 1, [0x0A000000]).Cells[0]);
    }

    [Fact]
    public void AnIsolatedCellHasAllNeighbourFlagsSet()
    {
        // 3x3 with only the centre opaque: its four neighbours wrap to transparent.
        var pixels = new int[9];
        pixels[4] = unchecked((int)0xFFFFFFFF);
        var texture = CloudTexture.FromPixels(3, 3, pixels);

        var center = texture.Cells[4];
        Assert.NotEqual(0, center);
        // Bits 0..3 are west/east/south/north-empty; an isolated cell has all four set.
        Assert.Equal(0b1111, (int)(center & 0b1111));
        Assert.Equal(0, texture.Cells[0]);
    }
}
