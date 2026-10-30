// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// Draw order for translucent chunk geometry: farthest first.
/// </summary>
/// <remarks>
/// Blending is not commutative, so translucent surfaces must be drawn from the back forward; the reference sorts translucent
/// sections by distance to the camera and accepts the remaining error inside a section (docs/specs/render/pipeline.md 7.1).
/// Sorting is stable so two runs of the same scene produce the same order, which keeps the pixel comparison meaningful.
/// </remarks>
public static class TranslucentOrder
{
    /// <summary>
    /// Orders items by descending squared distance, keeping the incoming order for equal distances.
    /// </summary>
    /// <param name="items">Pairs of key and squared distance to the camera.</param>
    /// <returns>Keys ordered from farthest to nearest.</returns>
    public static long[] ByDistanceDescending(ReadOnlySpan<(long Key, float DistanceSquared)> items)
    {
        var ordered = items.ToArray();

        // Insertion sort: n is the number of sections in view, small, and stability is worth more than asymptotics here.
        for (var index = 1; index < ordered.Length; index++)
        {
            var current = ordered[index];
            var scan = index - 1;

            while (scan >= 0 && ordered[scan].DistanceSquared < current.DistanceSquared)
            {
                ordered[scan + 1] = ordered[scan];
                scan--;
            }

            ordered[scan + 1] = current;
        }

        var keys = new long[ordered.Length];

        for (var index = 0; index < ordered.Length; index++)
        {
            keys[index] = ordered[index].Key;
        }

        return keys;
    }
}
