// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Common.World;

/// <summary>
/// The overworld sky colour from biome temperature, matching the reference
/// <c>OverworldBiomes.calculateSkyColor</c> (docs/modules/weather-and-sky-color.md 6.2).
/// </summary>
public static class SkyColour
{
    /// <summary>
    /// Returns the opaque ARGB sky colour for a biome temperature: <c>t = clamp(temperature/3, -1, 1)</c> then
    /// HSV <c>(0.62222224 - 0.05t, 0.5 + 0.1t, 1.0)</c>.
    /// </summary>
    public static int FromTemperature(float temperature)
    {
        var t = Math.Clamp(temperature / 3f, -1f, 1f);
        var hue = 0.62222224f - (t * 0.05f);
        var saturation = 0.5f + (t * 0.1f);
        return Opaque(HsvToArgb(hue, saturation, 1f, 0));
    }

    /// <summary>Reference <c>Mth.hsvToArgb</c>: packs HSV (hue wrap by whole turns) into ARGB with the given alpha.</summary>
    internal static int HsvToArgb(float hue, float saturation, float value, int alpha)
    {
        var h = (int)(hue * 6f) % 6;
        var f = (hue * 6f) - h;
        var p = value * (1f - saturation);
        var q = value * (1f - (f * saturation));
        var t = value * (1f - ((1f - f) * saturation));

        float red;
        float green;
        float blue;
        switch (h)
        {
            case 0: red = value; green = t; blue = p; break;
            case 1: red = q; green = value; blue = p; break;
            case 2: red = p; green = value; blue = t; break;
            case 3: red = p; green = q; blue = value; break;
            case 4: red = t; green = p; blue = value; break;
            case 5: red = value; green = p; blue = q; break;
            default: throw new InvalidOperationException($"invalid hue {hue}");
        }

        return Color(alpha, Channel(red), Channel(green), Channel(blue));
    }

    private static int Opaque(int color) => color | unchecked((int)0xFF000000);

    private static int Channel(float value) => Math.Clamp((int)(value * 255f), 0, 255);

    private static int Color(int alpha, int red, int green, int blue) =>
        ((alpha & 0xFF) << 24) | ((red & 0xFF) << 16) | ((green & 0xFF) << 8) | (blue & 0xFF);
}
