// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Imaging;

namespace Sharpcraft.Client.Assets;

/// <summary>
/// The 256 by 256 colour map the reference uses for grass and foliage tints.
/// </summary>
/// <remarks>
/// Indexed exactly as the reference does: rainfall is multiplied by temperature, then
/// <c>x = (1 - temperature) * 255</c>, <c>y = (1 - rainfall) * 255</c> and the colour is <c>pixels[(y &lt;&lt; 8) | x]</c>
/// (ref://mc-26.3-analysis/src/net/minecraft/world/level/ColorMapColorUtil.java). The image itself comes from the resource
/// pack, never from this repository.
/// </remarks>
public sealed class Colormap
{
    private readonly int[] _pixels;

    private Colormap(int[] pixels)
    {
        _pixels = pixels;
    }

    /// <summary>Number of pixels a colour map carries.</summary>
    public const int PixelCount = 65536;

    /// <summary>
    /// Loads a colour map from the resource stack.
    /// </summary>
    /// <param name="resources">Session resource manager.</param>
    /// <param name="path">Path below <c>textures/colormap/</c>, such as <c>grass</c>.</param>
    /// <returns>The map, or null when the pack does not carry it.</returns>
    public static Colormap? Load(ResourceManager resources, string path)
    {
        ArgumentNullException.ThrowIfNull(resources);

        var data = resources.Read(ResourceKind.Texture, new Identifier("minecraft", "colormap/" + path));

        if (data is null)
        {
            return null;
        }

        var image = PngDecoder.Decode(data);

        if (image.Width * image.Height != PixelCount)
        {
            return null;
        }

        var pixels = new int[PixelCount];

        for (var index = 0; index < PixelCount; index++)
        {
            var offset = index * 4;
            pixels[index] = (image.Pixels[offset] << 16) | (image.Pixels[offset + 1] << 8) | image.Pixels[offset + 2];
        }

        return new Colormap(pixels);
    }

    /// <summary>
    /// Samples the map for a temperature and rainfall.
    /// </summary>
    /// <param name="temperature">Biome temperature.</param>
    /// <param name="rainfall">Biome rainfall, already clamped like the reference.</param>
    /// <param name="fallback">Colour used when the computed index falls outside the map.</param>
    public int Sample(double temperature, double rainfall, int fallback)
    {
        var rain = rainfall * temperature;
        var x = (int)((1.0 - temperature) * 255.0);
        var y = (int)((1.0 - rain) * 255.0);
        var index = (y << 8) | x;

        return index < 0 || index >= _pixels.Length ? fallback : _pixels[index];
    }
}
