// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Reprojection between the current and the previous frame, the input every temporal method needs.
/// </summary>
/// <remarks>
/// Motion is expressed in pixels and points from where the surface is now to where it was last frame, which is the sign
/// upscalers and temporal antialiasing expect. Only the two view-projection matrices are involved, so this half can be
/// proven without a GPU; the per-pixel pass that consumes it comes later.
/// </remarks>
public static class Reprojection
{
    /// <summary>
    /// Screen-space displacement of a world point, in pixels.
    /// </summary>
    /// <param name="world">World position of the surface.</param>
    /// <param name="previousViewProjection">Last frame's combined matrix.</param>
    /// <param name="currentViewProjection">This frame's combined matrix.</param>
    /// <param name="width">Viewport width in pixels.</param>
    /// <param name="height">Viewport height in pixels.</param>
    /// <returns>Displacement in pixels, or <see cref="Vector2.NaN"/> when the point is not in front of the camera.</returns>
    public static Vector2 Of(
        Vector3 world,
        Matrix4x4 previousViewProjection,
        Matrix4x4 currentViewProjection,
        int width,
        int height)
    {
        if (!TryToScreen(world, currentViewProjection, out var current) ||
            !TryToScreen(world, previousViewProjection, out var previous))
        {
            return Vector2.NaN;
        }

        return new Vector2((previous.X - current.X) * width, (previous.Y - current.Y) * height);
    }

    /// <summary>Projects a world point to zero-to-one screen coordinates.</summary>
    /// <param name="world">World position.</param>
    /// <param name="viewProjection">Combined matrix.</param>
    /// <param name="screen">Screen coordinates in zero-to-one.</param>
    /// <returns>False when the point is behind the camera.</returns>
    public static bool TryToScreen(Vector3 world, Matrix4x4 viewProjection, out Vector2 screen)
    {
        var clip = Vector4.Transform(new Vector4(world, 1f), viewProjection);

        if (clip.W <= 1e-6f)
        {
            screen = Vector2.NaN;
            return false;
        }

        screen = new Vector2(
            ((clip.X / clip.W) * 0.5f) + 0.5f,
            // Screen y grows downward while clip y grows upward, hence the flip.
            (0.5f - ((clip.Y / clip.W) * 0.5f)));

        return true;
    }
}

/// <summary>
/// Keeps the previous frame's view-projection so motion can be measured against it.
/// </summary>
public struct FrameHistory
{
    private Matrix4x4 _previous;
    private bool _hasPrevious;

    /// <summary>The previous frame's combined matrix, or the identity before the first frame.</summary>
    public readonly Matrix4x4 PreviousViewProjection => _previous;

    /// <summary>True once at least one frame has been recorded.</summary>
    public readonly bool HasPrevious => _hasPrevious;

    /// <summary>Records this frame's matrix, returning the one it replaced.</summary>
    /// <param name="currentViewProjection">This frame's combined matrix.</param>
    /// <returns>The previous matrix, which callers use to compute motion for this frame.</returns>
    public Matrix4x4 Advance(Matrix4x4 currentViewProjection)
    {
        var previous = _previous;
        _previous = currentViewProjection;
        _hasPrevious = true;
        return previous;
    }
}
