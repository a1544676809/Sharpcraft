// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// Adaptive cloud draw range: reduces the cloudRange when frame time is high and raises it when there is headroom.
/// Explicitly opt-in (MC_CLOUD_ADAPTIVE); the default remains the reference 128 chunks.
/// </summary>
public static class CloudRangePolicy
{
    /// <summary>Minimum and maximum cloud range in chunks (reference default is 128).</summary>
    public const int Minimum = 32;

    /// <summary>Reference/default cloud range in chunks.</summary>
    public const int Maximum = 128;

    /// <summary>Frame-time threshold above which the range is reduced.</summary>
    public const double ReduceAboveMs = 40.0;

    /// <summary>Frame-time threshold below which the range can grow.</summary>
    public const double GrowBelowMs = 18.0;

    /// <summary>Next range for the given measured average frame time.</summary>
    public static int Next(int current, double averageFrameMilliseconds)
    {
        if (averageFrameMilliseconds > ReduceAboveMs && current > Minimum)
        {
            return Math.Max(Minimum, current - 8);
        }

        if (averageFrameMilliseconds < GrowBelowMs && current < Maximum)
        {
            return Math.Min(Maximum, current + 4);
        }

        return current;
    }
}
