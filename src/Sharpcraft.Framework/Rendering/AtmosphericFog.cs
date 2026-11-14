// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Framework.Rendering;

/// <summary>
/// The atmospheric fog shell: the fade that exists in every weather, and that rain pulls inward.
/// </summary>
/// <remarks>
/// One place computes it, so the window and the offscreen path cannot disagree. The reference derives the pull
/// from rain smoothed over time and scaled by the sky light at the camera and by whether the biome gets
/// precipitation; the two scalings are not modelled yet and are recorded in known-gaps.
/// </remarks>
public static class AtmosphericFog
{
    /// <summary>Where the shell begins in clear weather.</summary>
    public const float ClearStart = 0f;

    /// <summary>Where the shell reaches full strength in clear weather.</summary>
    public const float ClearEnd = 1024f;

    /// <summary>How far full rain pulls the start of the shell inward.</summary>
    public const float RainStartPull = 160f;

    /// <summary>How far full rain pulls the end of the shell inward.</summary>
    public const float RainEndPull = 256f;

    /// <summary>The end never collapses below this, however hard it rains.</summary>
    public const float MinimumRainEnd = 96f;

    /// <summary>Computes the shell for a rain level.</summary>
    /// <param name="rainLevel">Rain from nothing to full.</param>
    /// <returns>Where the shell starts and where it reaches full strength.</returns>
    public static (float Start, float End) Range(float rainLevel)
    {
        var rain = Math.Clamp(rainLevel, 0f, 1f);
        var start = ClearStart - (RainStartPull * rain);
        var end = ClearEnd - (RainEndPull * rain);
        return (start, MathF.Max(MinimumRainEnd, end));
    }
}
