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
}
