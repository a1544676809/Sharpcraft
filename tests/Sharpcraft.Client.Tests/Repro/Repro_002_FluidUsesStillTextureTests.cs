// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using MinecraftCSharp = Sharpcraft;
using Sharpcraft.Client.Assets;
using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Imaging;
using Sharpcraft.Common.Registries;
using Xunit;

namespace Sharpcraft.Client.Tests.Repro;

/// <summary>
/// Repro-002：流体（水、岩浆）没有 blockstates 面变量，模型只声明 <c>particle</c>，于是模型链解不出面纹理，
/// 真实服务器画面里整片水面渲染成紫黑格子。修复是在名字约定之前增加 <c>&lt;名字&gt;_still</c> 回退。
/// 关联：docs/modules/render.md §流体；`BlockSpriteMap`。
/// </summary>
public sealed class Repro_002_FluidUsesStillTextureTests
{
    [Theory]
    [InlineData("minecraft:water")]
    [InlineData("minecraft:lava")]
    public void FluidBlocksResolveToTheirStillTexture(string blockId)
    {
        var still = new Identifier("minecraft", "block/" + blockId["minecraft:".Length..] + "_still");
        var atlas = new SpriteAtlasBuilderShim().Build(still);
        var layout = new BlockStateLayout([(new Identifier("minecraft", blockId["minecraft:".Length..]), 1)]);

        var map = new BlockSpriteMap(atlas, layout);
        var sprite = map.ForState(0);

        Assert.NotEqual(atlas.Sprites[SpriteAtlas.MissingSprite], sprite);
        Assert.Equal(atlas.Sprites[still], sprite);
    }

    /// <summary>Builds a two-sprite atlas: the fluid still texture and the missing sprite.</summary>
    private sealed class SpriteAtlasBuilderShim
    {
        public SpriteAtlas Build(Identifier still)
        {
            return SpriteAtlasBuilder.Stitch(
            [
                (still, Solid(16, 16, 40, 90, 200), 1),
                (SpriteAtlas.MissingSprite, Solid(16, 16, 255, 0, 255), 1)
            ]);
        }

        private static RgbaImage Solid(int width, int height, byte r, byte g, byte b)
        {
            var pixels = new byte[width * height * RgbaImage.BytesPerPixel];
            for (var i = 0; i < pixels.Length; i += RgbaImage.BytesPerPixel)
            {
                pixels[i] = r;
                pixels[i + 1] = g;
                pixels[i + 2] = b;
                pixels[i + 3] = 255;
            }

            return new RgbaImage(width, height, pixels);
        }
    }
}
