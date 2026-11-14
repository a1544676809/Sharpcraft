// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;

namespace Sharpcraft.Client.Tests;

/// <summary>
/// Pins the chest atlas contents to the reference's <c>textures/entity/chest</c> directory, which
/// holds one standing variant per material plus left and right halves for the doubles.
/// </summary>
public sealed class ChestAtlasTests
{
    /// <summary>The reference directory has twenty two textures, ender chests having no halves.</summary>
    [Fact]
    public void TheChestSheetHoldsEveryReferenceTexture()
    {
        Assert.Equal(22, ChestAtlas.Textures.Length);
    }

    /// <summary>Each entry is a chest texture and none repeats.</summary>
    [Fact]
    public void EveryEntryIsADistinctChestTexture()
    {
        Assert.Equal(ChestAtlas.Textures.Length, ChestAtlas.Textures.Distinct(StringComparer.Ordinal).Count());
        Assert.All(ChestAtlas.Textures, path => Assert.StartsWith("entity/chest/", path, StringComparison.Ordinal));
    }

    /// <summary>Only the double chest variants carry a half suffix.</summary>
    [Fact]
    public void HalvesComeInPairs()
    {
        var halves = ChestAtlas.Textures.Where(path => path.EndsWith("_left", StringComparison.Ordinal)).Select(path => path[..^5]).ToArray();
        var rights = ChestAtlas.Textures.Where(path => path.EndsWith("_right", StringComparison.Ordinal)).Select(path => path[..^6]).ToArray();

        Assert.Equal(7, halves.Length);
        Assert.Equal(halves.OrderBy(path => path, StringComparer.Ordinal), rights.OrderBy(path => path, StringComparer.Ordinal));
    }

    /// <summary>The plain wood texture is part of the sheet.</summary>
    [Fact]
    public void TheNormalTextureIsInTheSheet()
    {
        Assert.Contains(ChestAtlas.NormalTexture, ChestAtlas.Textures);
    }

    /// <summary>Each material and half resolves to the texture the reference names it by.</summary>
    [Theory]
    [InlineData((byte)0, (byte)0, "entity/chest/normal")]
    [InlineData((byte)1, (byte)1, "entity/chest/trapped_left")]
    [InlineData((byte)1, (byte)2, "entity/chest/trapped_right")]
    [InlineData((byte)2, (byte)0, "entity/chest/ender")]
    [InlineData((byte)2, (byte)2, "entity/chest/ender")]
    [InlineData((byte)3, (byte)0, "entity/chest/copper")]
    [InlineData((byte)4, (byte)1, "entity/chest/copper_exposed_left")]
    [InlineData((byte)5, (byte)2, "entity/chest/copper_weathered_right")]
    [InlineData((byte)6, (byte)0, "entity/chest/copper_oxidized")]
    public void TexturePathsMatchTheReferenceNames(byte material, byte type, string expected)
    {
        Assert.Equal(expected, ChestAtlas.TexturePathFor(material, type));
    }

    /// <summary>Every resolved path is one the atlas actually holds.</summary>
    [Fact]
    public void EveryMaterialResolvesToAPackedTexture()
    {
        for (byte material = 0; material <= 6; material++)
        {
            for (byte type = 0; type <= 2; type++)
            {
                var path = ChestAtlas.TexturePathFor(material, type);
                Assert.Contains(path, ChestAtlas.Textures);
            }
        }
    }
}
