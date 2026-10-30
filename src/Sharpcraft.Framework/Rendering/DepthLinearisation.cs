// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Framework.Rendering;

/// <summary>
/// Converts window depth back to a distance along the view axis, in blocks.
/// </summary>
/// <remarks>
/// Window depth is distributed by inverse distance, so almost the whole far field compresses into the last few thousandths:
/// measured on the recorded scene it ran from 0.9986 to 1.0 for terrain tens of blocks away. Comparing occlusion in that
/// domain is meaningless, so anything that reasons about "how far is this" converts first.
/// </remarks>
public static class DepthLinearisation
{
    /// <summary>View-space distance for a window depth value.</summary>
    /// <param name="depth">Window depth, zero to one.</param>
    /// <param name="near">Near plane.</param>
    /// <param name="far">Far plane.</param>
    public static float ToDistance(float depth, float near, float far)
    {
        if (depth >= 1f)
        {
            return far;
        }

        if (depth <= 0f)
        {
            return near;
        }

        return near * far / (far - (depth * (far - near)));
    }

    /// <summary>Window depth for a view-space distance, the inverse of <see cref="ToDistance"/>.</summary>
    /// <param name="distance">View-space distance.</param>
    /// <param name="near">Near plane.</param>
    /// <param name="far">Far plane.</param>
    public static float ToDepth(float distance, float near, float far) =>
        distance <= 0f ? 0f : far / (far - near) * (1f - (near / distance));

    /// <summary>Converts a whole buffer in place.</summary>
    /// <param name="depth">Window depths, converted to distances on return.</param>
    /// <param name="near">Near plane.</param>
    /// <param name="far">Far plane.</param>
    public static void InPlace(Span<float> depth, float near, float far)
    {
        for (var index = 0; index < depth.Length; index++)
        {
            depth[index] = ToDistance(depth[index], near, far);
        }
    }
}
