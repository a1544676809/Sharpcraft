// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Framework.Rendering;

/// <summary>
/// The transform the two dimensional layer draws with (docs/modules/ui.md 9).
/// </summary>
/// <remarks>
/// A plain orthographic projection from top left origin pixels to clip space, so a widget can be laid out in
/// readable coordinates and still land on the pixel grid. The matrix is column major to match what the shader
/// expects, and is written into caller owned storage so no allocation happens per frame.
/// </remarks>
public static class UiProjection
{
    /// <summary>Number of floats in a four by four matrix.</summary>
    public const int FloatsPerMatrix = 16;

    /// <summary>Writes the projection for a virtual resolution of the given size.</summary>
    /// <param name="width">Virtual width in pixels; must be positive.</param>
    /// <param name="height">Virtual height in pixels; must be positive.</param>
    /// <param name="destination">Receives sixteen floats, column major.</param>
    public static void WriteOrthographic(float width, float height, Span<float> destination)
    {
        if (destination.Length < FloatsPerMatrix)
        {
            throw new ArgumentException("destination is too small for a matrix", nameof(destination));
        }

        if (width <= 0f || height <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "virtual resolution must be positive");
        }

        destination[..FloatsPerMatrix].Clear();

        // x in [0, width] maps to [-1, 1]; y in [0, height] maps to [1, -1] because the origin is the top left.
        destination[0] = 2f / width;
        destination[5] = -2f / height;
        destination[10] = 1f;
        destination[12] = -1f;
        destination[13] = 1f;
        destination[15] = 1f;
    }

    /// <summary>Maps a virtual pixel to clip space, for tests and hit testing.</summary>
    /// <param name="x">Pixel x.</param>
    /// <param name="y">Pixel y.</param>
    /// <param name="width">Virtual width.</param>
    /// <param name="height">Virtual height.</param>
    public static (float X, float Y) ToClip(float x, float y, float width, float height) =>
        ((2f * x / width) - 1f, 1f - (2f * y / height));
}
