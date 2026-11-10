// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Assets;
using Xunit;

namespace Sharpcraft.Client.Tests.Assets;

/// <summary>
/// The atlas mip level and padding derivation, checked against the cases the reference algorithm produces.
/// </summary>
public sealed class AtlasMipPolicyTests
{
    [Fact]
    public void FullSizeSpritesKeepTheRequestedLevel()
    {
        // Every sprite is 16x16 and the option asks for four levels: the lowest set bit of 16 is 16, which is exactly
        // 1 << 4, so nothing forces a reduction.
        (int, int)[] sizes = [(16, 16), (16, 16)];

        Assert.Equal(4, AtlasMipPolicy.ResolveLevels(sizes, AtlasMipPolicy.DefaultMaxLevels));
    }

    [Fact]
    public void AWebSizedSpriteLowersTheLevel()
    {
        // The reference warns and drops the level when a sprite cannot survive the halving; a 4x4 sprite caps the atlas
        // at level two.
        (int, int)[] sizes = [(16, 16), (4, 4)];

        Assert.Equal(2, AtlasMipPolicy.ResolveLevels(sizes, AtlasMipPolicy.DefaultMaxLevels));
    }

    [Fact]
    public void LevelZeroIsAlwaysAvailable()
    {
        Assert.Equal(0, AtlasMipPolicy.ResolveLevels([(16, 16)], maxLevels: 0));

        // An empty atlas still has to produce a usable answer rather than an exception.
        Assert.Equal(0, AtlasMipPolicy.ResolveLevels([], AtlasMipPolicy.DefaultMaxLevels));
    }

    [Fact]
    public void PaddingFollowsTheLevelAndAnisotropy()
    {
        // Level zero keeps the bilinear floor.
        Assert.Equal(2, AtlasMipPolicy.ResolvePadding(mipLevels: 0, AtlasMipPolicy.NoAnisotropy));

        // Level one doubles it, level four is sixteen.
        Assert.Equal(2, AtlasMipPolicy.ResolvePadding(mipLevels: 1, AtlasMipPolicy.NoAnisotropy));
        Assert.Equal(16, AtlasMipPolicy.ResolvePadding(mipLevels: 4, AtlasMipPolicy.NoAnisotropy));

        // Anisotropy shifts further, and the reference caps that shift at four bits.
        Assert.Equal(4, AtlasMipPolicy.ResolvePadding(mipLevels: 1, anisotropyBit: 2));
        Assert.Equal(32, AtlasMipPolicy.ResolvePadding(mipLevels: 4, anisotropyBit: 2));
        Assert.Equal(256, AtlasMipPolicy.ResolvePadding(mipLevels: 4, anisotropyBit: 5));
    }

    [Fact]
    public void AnisotropyOffIgnoresItsExponent()
    {
        // The reference passes zero rather than one when filtering is not anisotropic, and the formula then shifts by
        // nothing.
        Assert.Equal(
            AtlasMipPolicy.ResolvePadding(mipLevels: 3, AtlasMipPolicy.NoAnisotropy),
            AtlasMipPolicy.ResolvePadding(mipLevels: 3, anisotropyBit: 1));
    }
}
