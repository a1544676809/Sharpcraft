// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;

namespace Sharpcraft.Common.World;

/// <summary>
/// Weather (rain/thunder) blending of environment attributes, matching the reference WeatherAttributes layer application
/// (docs/modules/weather-and-sky-color.md 6.3): rain weight = rainLevel − thunderLevel, thunder weight = thunderLevel,
/// each applied by <c>stateChangeLerp(weight, result, entry.applyModifier(result))</c> in that order.
/// </summary>
public static class WeatherBlend
{
    // SKY_LIGHT_FACTOR ALPHA_BLEND arguments: FloatWithAlpha(0.24, alpha).
    private const float RainLightFactorValue = 0.24f;
    private const float RainLightFactorAlpha = 0.3125f;
    private const float ThunderLightFactorValue = 0.24f;
    private const float ThunderLightFactorAlpha = 0.52734375f;

    // SKY_COLOR BLEND_TO_GRAY arguments.
    private const float RainGrayFactor = 0.6f;
    private const float RainGrayBrightness = 0.75f;
    private const float ThunderGrayFactor = 0.24f;
    private const float ThunderGrayBrightness = 0.94f;

    // FOG_COLOR MULTIPLY_RGB arguments (ARGB.colorFromFloat(1, r,g,b) -> rgb only).
    private static readonly Vector3 RainFog = new(0.5f, 0.5f, 0.6f);
    private static readonly Vector3 ThunderFog = new(0.25f, 0.25f, 0.3f);

    // SUNRISE_SUNSET_COLOR MULTIPLY_ARGB arguments (alpha comes from colorFromFloat(alpha=1) so it is 1).
    private static readonly Vector4 RainSunrise = new(0.5f, 0.5f, 0.6f, 1f);
    private static readonly Vector4 ThunderSunrise = new(0.25f, 0.25f, 0.3f, 1f);

    /// <summary>Reference <c>RAIN_BRIGHTNESS = 1 − getRainLevel(partialTicks)</c>, the sun/moon/stars alpha.</summary>
    public static float RainBrightness(float rainLevel) => 1f - rainLevel;

    /// <summary>
    /// Weather-applied <c>SKY_LIGHT_FACTOR</c>: forest base factor blended toward the RAIN then THUNDER overrides
    /// (<c>FloatWithAlpha(0.24,alpha)</c> → <c>lerp(alpha, subject, 0.24)</c>) by their weights.
    /// </summary>
    public static float SkyLightFactor(float baseFactor, float rainLevel, float thunderLevel)
    {
        var result = baseFactor;
        var rainWeight = rainLevel - thunderLevel;
        if (rainWeight > 0f)
        {
            var modified = ColourBlend.Lerp(RainLightFactorAlpha, result, RainLightFactorValue);
            result = ColourBlend.Lerp(rainWeight, result, modified);
        }

        if (thunderLevel > 0f)
        {
            var modified = ColourBlend.Lerp(ThunderLightFactorAlpha, result, ThunderLightFactorValue);
            result = ColourBlend.Lerp(thunderLevel, result, modified);
        }

        return result;
    }

    /// <summary>Weather-applied <c>SKY_COLOR</c>: base colour blended toward RAIN then THUNDER greyscale by their weights.</summary>
    public static Vector3 ApplySkyColour(Vector3 baseColour, float rainLevel, float thunderLevel)
    {
        var result = baseColour;
        var rainWeight = rainLevel - thunderLevel;
        if (rainWeight > 0f)
        {
            var modified = ColourBlend.BlendToGray(result, RainGrayFactor, RainGrayBrightness);
            result = ColourBlend.Lerp(rainWeight, result, modified);
        }

        if (thunderLevel > 0f)
        {
            var modified = ColourBlend.BlendToGray(result, ThunderGrayFactor, ThunderGrayBrightness);
            result = ColourBlend.Lerp(thunderLevel, result, modified);
        }

        return result;
    }

    /// <summary>Weather-applied <c>FOG_COLOR</c>: base RGB multiplied by the RAIN/THUNDER factor, blended by their weights.</summary>
    public static Vector3 ApplyFog(Vector3 baseColour, float rainLevel, float thunderLevel)
    {
        var result = baseColour;
        var rainWeight = rainLevel - thunderLevel;
        if (rainWeight > 0f)
        {
            result = Vector3.Lerp(result, Multiply(result, RainFog), rainWeight);
        }

        if (thunderLevel > 0f)
        {
            result = Vector3.Lerp(result, Multiply(result, ThunderFog), thunderLevel);
        }

        return result;
    }

    /// <summary>Weather-applied <c>SUNRISE_SUNSET_COLOR</c>: base RGBA multiplied by the RAIN/THUNDER factor, blended by their weights.</summary>
    public static Vector4 ApplySunrise(Vector4 baseColour, float rainLevel, float thunderLevel)
    {
        var result = baseColour;
        var rainWeight = rainLevel - thunderLevel;
        if (rainWeight > 0f)
        {
            result = Vector4.Lerp(result, Multiply(result, RainSunrise), rainWeight);
        }

        if (thunderLevel > 0f)
        {
            result = Vector4.Lerp(result, Multiply(result, ThunderSunrise), thunderLevel);
        }

        return result;
    }

    // SKY_LIGHT_COLOR ALPHA_BLEND_RGB: source = night sky-light colour (0.48,0.48,1.0 sRGB -> 122,122,255) with alpha
    // as8BitChannel(0.3125/0.52734375) = 79/134.
    private static readonly Vector3 NightSkyLightRgb = new(122f / 255f, 122f / 255f, 1f);
    private const float NightSkyLightRainAlpha = 79f / 255f;
    private const float NightSkyLightThunderAlpha = 134f / 255f;

    /// <summary>Weather-applied <c>SKY_LIGHT_COLOR</c>: base RGB alpha-blended toward the night sky-light colour, weighted.</summary>
    public static Vector3 ApplySkyLightColor(Vector3 baseColour, float rainLevel, float thunderLevel)
    {
        var result = new Vector4(baseColour, 1f);
        var rainWeight = rainLevel - thunderLevel;
        if (rainWeight > 0f)
        {
            var source = new Vector4(NightSkyLightRgb, NightSkyLightRainAlpha);
            result = Vector4.Lerp(result, ColourBlend.AlphaBlend(result, source), rainWeight);
        }

        if (thunderLevel > 0f)
        {
            var source = new Vector4(NightSkyLightRgb, NightSkyLightThunderAlpha);
            result = Vector4.Lerp(result, ColourBlend.AlphaBlend(result, source), thunderLevel);
        }

        return new Vector3(result.X, result.Y, result.Z);
    }

    private static Vector3 Multiply(Vector3 a, Vector3 b) => new(a.X * b.X, a.Y * b.Y, a.Z * b.Z);

    private static Vector4 Multiply(Vector4 a, Vector4 b) => new(a.X * b.X, a.Y * b.Y, a.Z * b.Z, a.W * b.W);
}
