// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Assets;
using Xunit;

namespace Sharpcraft.Client.Tests.Assets;

/// <summary>
/// Atlas mip generation (docs/specs/render/block-layer-and-tint.md): a texture with holes must keep roughly the same alpha
/// test coverage at every level, otherwise the silhouette erodes and distant blocks turn into a checkerboard.
/// </summary>
public sealed class AtlasMipChainTests
{
    private static byte[] Checkerboard(int size)
    {
        var pixels = new byte[size * size * 4];

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var offset = (((y * size) + x) * 4);
                var opaque = (x + y) % 2 == 0;
                pixels[offset] = 90;
                pixels[offset + 1] = 120;
                pixels[offset + 2] = 60;
                pixels[offset + 3] = opaque ? (byte)255 : (byte)0;
            }
        }

        return pixels;
    }

    private static float CoveredFraction(byte[] pixels)
    {
        var covered = 0;
        var total = pixels.Length / 4;

        for (var index = 3; index < pixels.Length; index += 4)
        {
            if (pixels[index] >= 128)
            {
                covered++;
            }
        }

        return total == 0 ? 0f : covered / (float)total;
    }

    [Fact]
    public void AnOpaqueSpriteStaysOpaque()
    {
        const int size = 8;
        var pixels = new byte[size * size * 4];

        for (var index = 0; index < pixels.Length; index += 4)
        {
            pixels[index] = 200;
            pixels[index + 1] = 200;
            pixels[index + 2] = 200;
            pixels[index + 3] = 255;
        }

        var mips = AtlasMipChain.Build(pixels, size, size, [new SpriteRect(0, 0, size, size, 1)]);

        var levelZero = mips[0];
        Console.WriteLine($"diag: mips={mips.Length} lens={string.Join(',', mips.Select(m => m.Length))} a0={levelZero[3]} a1={levelZero[7]}");

        for (var index = 3; index < levelZero.Length; index += 4)
        {
            Assert.Equal(255, levelZero[index]);
        }
    }

    [Fact]
    public void LevelsHalveTheAtlas()
    {
        const int size = 16;
        var mips = AtlasMipChain.Build(new byte[size * size * 4], size, size, [new SpriteRect(0, 0, size, size, 1)]);

        Assert.Equal(size * size * 4 / 4, mips[0].Length);
        Assert.Equal(size * size * 4 / 16, mips[1].Length);
    }
}
