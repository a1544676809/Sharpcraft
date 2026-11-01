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
}
