// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Assets;

/// <summary>
/// Alpha content of a sprite inside a stitched atlas.
/// </summary>
/// <remarks>
/// Split out from <see cref="SpriteAtlas"/> so the rule can be tested without building an atlas, and because it is a pure
/// function of the pixels. The rule matches the reference: a zero alpha pixel cuts the texture out, a partially
/// transparent one means it has to be blended
/// (ref://mc-26.3-analysis/src/com/mojang/blaze3d/platform/NativeImage.java computeTransparency).
/// </remarks>
public static class SpriteAlpha
{
    /// <summary>
    /// Computes the alpha content of one sprite.
    /// </summary>
    /// <param name="rgba">Atlas pixels, four bytes per texel.</param>
    /// <param name="atlasWidth">Atlas width in pixels.</param>
    /// <param name="sprite">Sprite rectangle in atlas pixels.</param>
    public static Transparency Compute(ReadOnlySpan<byte> rgba, int atlasWidth, SpriteRect sprite)
    {
        var hasTransparent = false;
        var hasTranslucent = false;

        for (var y = sprite.Y; y < sprite.Y + sprite.Height; y++)
        {
            for (var x = sprite.X; x < sprite.X + sprite.Width; x++)
            {
                var alpha = rgba[(((y * atlasWidth) + x) * 4) + 3];

                if (alpha == 0)
                {
                    hasTransparent = true;
                }
                else if (alpha != 255)
                {
                    hasTranslucent = true;
                }
            }
        }

        return Transparency.Of(hasTransparent, hasTranslucent);
    }
}
