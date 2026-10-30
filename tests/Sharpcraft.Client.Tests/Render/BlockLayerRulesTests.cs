// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Assets;
using Sharpcraft.Client.Render;
using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Registries;
using Xunit;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Render layers and biome tint groundwork (docs/specs/render/block-layer-and-tint.md): the layer comes from the
/// material's transparency - partially transparent means blended, binary holes mean cut out, opaque means solid.
/// </summary>
public sealed class BlockLayerRulesTests
{
    private static SpriteRect Sprite(int x, int y, int w, int h) => new(x, y, w, h, 1);

    [Fact]
    public void AnOpaqueTextureIsSolid()
    {
        var pixels = new byte[4 * 4 * 4];
        for (var index = 3; index < pixels.Length; index += 4)
        {
            pixels[index] = 255;
        }

        var transparency = SpriteAlpha.Compute(pixels, 4, Sprite(0, 0, 4, 4));

        Assert.True(transparency.IsOpaque);
    }

    [Fact]
    public void AHoleMakesItCutout()
    {
        var pixels = new byte[4 * 4 * 4];
        for (var index = 3; index < pixels.Length; index += 4)
        {
            pixels[index] = 255;
        }

        pixels[3] = 0;

        var transparency = SpriteAlpha.Compute(pixels, 4, Sprite(0, 0, 4, 4));

        Assert.True(transparency.HasTransparent);
        Assert.False(transparency.HasTranslucent);
    }

    [Fact]
    public void PartialAlphaMakesItTranslucent()
    {
        var pixels = new byte[4 * 4 * 4];
        for (var index = 3; index < pixels.Length; index += 4)
        {
            pixels[index] = 255;
        }

        pixels[3] = 128;

        var transparency = SpriteAlpha.Compute(pixels, 4, Sprite(0, 0, 4, 4));

        Assert.True(transparency.HasTranslucent);
        Assert.False(transparency.HasTransparent);
    }

    [Fact]
    public void TheScannedRectangleIsTheSpriteOnly()
    {
        // A hole outside the sprite must not leak into its transparency.
        var pixels = new byte[4 * 4 * 4];
        for (var index = 3; index < pixels.Length; index += 4)
        {
            pixels[index] = 255;
        }

        pixels[(((3 * 4) + 3) * 4) + 3] = 0;

        Assert.True(SpriteAlpha.Compute(pixels, 4, Sprite(0, 0, 3, 3)).IsOpaque);
    }

    [Fact]
    public void CombiningGrowsTheTransparency()
    {
        var left = Transparency.Of(hasTransparent: true, hasTranslucent: false);
        var right = Transparency.Of(hasTransparent: false, hasTranslucent: true);

        var combined = left.Or(right);

        Assert.True(combined.HasTransparent);
        Assert.True(combined.HasTranslucent);
        Assert.False(combined.IsOpaque);
    }

    [Fact]
    public void TheLayerFollowsTheNameTableForFluidsAndGlass()
    {
        var layout = new BlockStateLayout([(new Identifier("minecraft", "stone"), 1), (new Identifier("minecraft", "water"), 1)]);

        Assert.Equal(BlockLayer.Translucent, BlockLayerRules.Of(1, sprites: null, layout));
        Assert.Equal(BlockLayer.Solid, BlockLayerRules.Of(0, sprites: null, layout));
    }

    [Fact]
    public void UnknownStatesAreSolid()
    {
        Assert.Equal(BlockLayer.Solid, BlockLayerRules.Of(9999, sprites: null, layout: null));
    }
}
