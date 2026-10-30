// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Sub-pixel camera offsets for temporal antialiasing and temporal upscalers.
/// </summary>
/// <remarks>
/// A temporal method needs the projection to wander inside one pixel, otherwise the samples it accumulates are identical
/// and the extra frames buy nothing. The offsets are in pixels and stay well inside one pixel; the sequence repeats over a
/// small fixed period so motion vectors and history reuse stay coherent.
/// </remarks>
public static class CameraJitter
{
    /// <summary>Frames after which the sequence repeats.</summary>
    public const int Period = 8;

    private static readonly Vector2[] Offsets =
    [
        new(-0.125f, -0.375f),
        new(0.375f, -0.125f),
        new(0.125f, 0.375f),
        new(-0.375f, 0.125f),
        new(-0.375f, -0.125f),
        new(0.125f, -0.375f),
        new(0.375f, 0.125f),
        new(-0.125f, 0.375f)
    ];

    /// <summary>Offset in pixels for a frame index; negative indices are accepted.</summary>
    /// <param name="frameIndex">Monotonic frame counter.</param>
    public static Vector2 For(int frameIndex)
    {
        var index = frameIndex % Period;

        if (index < 0)
        {
            index += Period;
        }

        return Offsets[index];
    }

    /// <summary>Applies an offset to a projection matrix as a clip-space translation, where one unit is one pixel.</summary>
    /// <param name="projection">Projection matrix.</param>
    /// <param name="jitterPixels">Offset in pixels.</param>
    /// <param name="width">Viewport width in pixels.</param>
    /// <param name="height">Viewport height in pixels.</param>
    public static Matrix4x4 Apply(Matrix4x4 projection, Vector2 jitterPixels, int width, int height)
    {
        // Clip space runs -1..1 across the viewport, so one pixel is two units divided by the viewport size.
        var offsetX = width > 0 ? jitterPixels.X * 2f / width : 0f;
        var offsetY = height > 0 ? jitterPixels.Y * 2f / height : 0f;

        projection.M31 += offsetX;
        projection.M32 += offsetY;

        return projection;
    }
}
