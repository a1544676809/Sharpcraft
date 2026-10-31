// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Parameters of the vanilla lightmap for one dimension/time. Defaults describe the overworld at day, the only case the
/// first pass models (docs/specs/render/lighting.md 6). Night, weather and other dimensions are future work; the shape is
/// here so they slot in without touching callers.
/// </summary>
public readonly record struct LightmapParameters(
    float SkyFactor,
    float BlockFactor,
    Vector3 SkyLightColor,
    Vector3 BlockLightTint,
    Vector3 AmbientColor,
    float BrightnessFactor)
{
    /// <summary>Overworld daytime defaults (docs/specs/render/lighting.md 3).</summary>
    public static LightmapParameters DayOverworld { get; } = new(
        SkyFactor: 1.0f,
        BlockFactor: 1.4f,
        SkyLightColor: Vector3.One,
        BlockLightTint: Srgb24ToLinear(-10100),
        AmbientColor: Vector3.Zero,
        BrightnessFactor: 0.5f);

    /// <summary>Decodes a packed sRGB 24-bit colour into linear floats, as the reference does for environment colours.</summary>
    public static Vector3 Srgb24ToLinear(int rgb24)
    {
        var red = (rgb24 >> 16) & 0xFF;
        var green = (rgb24 >> 8) & 0xFF;
        var blue = rgb24 & 0xFF;
        return new Vector3(SrgbToLinear(red / 255f), SrgbToLinear(green / 255f), SrgbToLinear(blue / 255f));
    }

    private static float SrgbToLinear(float value) =>
        value >= 0.04045f ? MathF.Pow((value + 0.055f) / 1.055f, 2.4f) : value / 12.92f;
}

/// <summary>
/// The vanilla lightmap multiplier for a (sky, block) light pair (docs/specs/render/lighting.md 3). Returns a linear RGB
/// value that the terrain shader multiplies with the decoded texel colour; the gamma encode happens in the shader.
/// </summary>
public static class VanillaLightmap
{
    /// <summary>Lightmap colour with the overworld daytime parameters.</summary>
    public static Vector3 Colour(int sky, int block) => Colour(sky, block, LightmapParameters.DayOverworld);

    /// <summary>Lightmap colour for explicit parameters.</summary>
    public static Vector3 Colour(int sky, int block, in LightmapParameters parameters)
    {
        var blockLevel = Math.Clamp(block, 0, 15) / 15f;
        var skyLevel = Math.Clamp(sky, 0, 15) / 15f;

        var blockBrightness = BrightnessCurve(blockLevel) * parameters.BlockFactor;
        var skyBrightness = BrightnessCurve(skyLevel) * parameters.SkyFactor;

        var colour = parameters.AmbientColor + (parameters.SkyLightColor * skyBrightness);

        var parabolic = (2f * blockLevel) - 1f;
        parabolic *= parabolic;
        var blockLightColour = Vector3.Lerp(parameters.BlockLightTint, Vector3.One, 0.9f * parabolic);
        colour += blockLightColour * blockBrightness;

        colour = Vector3.Clamp(colour, Vector3.Zero, Vector3.One);

        var notGamma = NotGamma(colour);
        colour = Vector3.Lerp(colour, notGamma, Math.Clamp(parameters.BrightnessFactor, 0f, 1f));

        return Vector3.Clamp(colour, Vector3.Zero, Vector3.One);
    }

    /// <summary>The vanilla brightness curve <c>x / (4 - 3x)</c> over 0..1.</summary>
    public static float BrightnessCurve(float level) => level / (4f - (3f * level));

    /// <summary>The reference inverse-gamma boost applied before the brightness mix.</summary>
    public static Vector3 NotGamma(Vector3 colour)
    {
        var maximum = MathF.Max(colour.X, MathF.Max(colour.Y, colour.Z));

        if (maximum <= 0f)
        {
            return colour;
        }

        var inverted = 1f - maximum;
        var scaled = 1f - (inverted * inverted * inverted * inverted);
        return colour * (scaled / maximum);
    }
}
