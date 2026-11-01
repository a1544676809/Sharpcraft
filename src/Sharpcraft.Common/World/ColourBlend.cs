// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;

namespace Sharpcraft.Common.World;

/// <summary>
/// Colour blend primitives used by the environment-attribute system (docs/modules/weather-and-sky-color.md 6.3):
/// reference <c>ARGB.greyscale/scaleRGB/srgbLerp</c> for Vector3fc, plus <c>Mth.lerp</c>.
/// </summary>
public static class ColourBlend
{
    /// <summary>Reference <c>Mth.lerp</c>: <c>a + alpha·(b − a)</c>.</summary>
    public static float Lerp(float alpha, float a, float b) => a + (alpha * (b - a));

    /// <summary>Channel-wise <c>a.lerp(b, alpha)</c> (the reference <c>ARGB.srgbLerp(Vector3fc)</c>).</summary>
    public static Vector3 Lerp(float alpha, Vector3 a, Vector3 b) => a + ((b - a) * alpha);

    /// <summary>Reference <c>ARGB.greyscale(Vector3fc)</c>: luminance <c>0.3r + 0.59g + 0.11b</c> on every channel.</summary>
    public static Vector3 Greyscale(Vector3 colour)
    {
        var grey = (colour.X * 0.3f) + (colour.Y * 0.59f) + (colour.Z * 0.11f);
        return new Vector3(grey, grey, grey);
    }

    /// <summary>Reference <c>ARGB.scaleRGB(Vector3fc, scale)</c>: multiply then clamp to 0..1.</summary>
    public static Vector3 ScaleRgb(Vector3 colour, float scale) => new(
        Math.Clamp(colour.X * scale, 0f, 1f),
        Math.Clamp(colour.Y * scale, 0f, 1f),
        Math.Clamp(colour.Z * scale, 0f, 1f));

    /// <summary>Reference <c>ColorModifier.BLEND_TO_GRAY_RGB</c>: <c>srgbLerp(factor, subject, greyscale(subject)·brightness)</c>.</summary>
    public static Vector3 BlendToGray(Vector3 subject, float factor, float brightness) =>
        Lerp(factor, subject, ScaleRgb(Greyscale(subject), brightness));

    /// <summary>Packs an opaque RGB24 colour (reference <c>ARGB.color(r,g,b)</c> = 0xFFRRGGBB).</summary>
    public static int Rgb(int red, int green, int blue) =>
        unchecked((int)0xFF000000) | ((red & 0xFF) << 16) | ((green & 0xFF) << 8) | (blue & 0xFF);

    /// <summary>Red channel of a packed ARGB colour.</summary>
    public static int Red(int color) => (color >> 16) & 0xFF;

    /// <summary>Green channel of a packed ARGB colour.</summary>
    public static int Green(int color) => (color >> 8) & 0xFF;

    /// <summary>Blue channel of a packed ARGB colour.</summary>
    public static int Blue(int color) => color & 0xFF;
}
