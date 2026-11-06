// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Framework.Rendering;

/// <summary>
/// Converts window depth back to a distance along the view axis, in blocks.
/// </summary>
/// <remarks>
/// The engine uses reversed-Z (near maps to depth 1, far to depth 0), so window depth is distributed by inverse distance
/// with the far field compressed near zero: on the recorded scene, terrain tens of blocks away sat at a window depth of
/// only about 0.0014. Comparing occlusion in that domain is meaningless, so anything that reasons about "how far is this"
/// converts first.
/// </remarks>
public static class DepthLinearisation
{
    /// <summary>View-space distance for a window depth value.</summary>
    /// <param name="depth">Window depth, zero to one (1 is the near plane, 0 the far plane).</param>
    /// <param name="near">Near plane.</param>
    /// <param name="far">Far plane.</param>
    public static float ToDistance(float depth, float near, float far)
    {
        if (depth >= 1f)
        {
            return near;
        }

        if (depth <= 0f)
        {
            return far;
        }

        // Invert depth = near * (far - d) / (d * (far - near)) for d.
        return near * far / ((depth * (far - near)) + near);
    }

    /// <summary>Window depth for a view-space distance, the inverse of <see cref="ToDistance"/>.</summary>
    /// <param name="distance">View-space distance.</param>
    /// <param name="near">Near plane.</param>
    /// <param name="far">Far plane.</param>
    public static float ToDepth(float distance, float near, float far) =>
        distance <= 0f ? 0f : near * (far - distance) / (distance * (far - near));

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
