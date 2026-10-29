// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Framework.Platform;

/// <summary>
/// The interface scale, and the virtual canvas it produces (docs/modules/ui.md 9.1).
/// </summary>
/// <remarks>
/// The reference does not draw its interface at native resolution. It picks the largest whole number of device
/// pixels per interface pixel that still leaves at least 320 by 240 interface pixels on screen, then draws
/// everything in that smaller coordinate system. Whole numbers only, because a fractional scale makes text blurry.
/// Falling to reproduce this makes every number in the interface wrong: the chat column is 320 interface pixels
/// wide in both implementations, but it covers twice as much of a window drawn at half the intended scale.
/// </remarks>
/// <summary>The scale and the virtual canvas it produces.</summary>
/// <param name="Scale">Device pixels per interface pixel.</param>
/// <param name="Width">Virtual width in interface pixels.</param>
/// <param name="Height">Virtual height in interface pixels.</param>
public readonly record struct GuiCanvas(int Scale, int Width, int Height);

/// <summary>Chooses the interface scale and the canvas it produces.</summary>
public static class GuiScale
{
    /// <summary>Narrowest interface canvas the reference allows at any scale.</summary>
    public const int MinWidth = 320;

    /// <summary>Shortest interface canvas the reference allows at any scale.</summary>
    public const int MinHeight = 240;

    /// <summary>Scale value that means "as large as fits".</summary>
    public const int Auto = 0;

    /// <summary>
    /// Picks the scale for a framebuffer.
    /// </summary>
    /// <param name="framebufferWidth">Device pixels across.</param>
    /// <param name="framebufferHeight">Device pixels down.</param>
    /// <param name="maxScale">Requested maximum, or <see cref="Auto"/> for as large as fits.</param>
    /// <param name="enforceUnicode">True when the font needs an even scale, as the reference does for its unicode
    /// font.</param>
    public static int Calculate(
        int framebufferWidth,
        int framebufferHeight,
        int maxScale = Auto,
        bool enforceUnicode = false)
    {
        var limit = maxScale == Auto ? int.MaxValue : Math.Max(1, maxScale);
        var scale = 1;

        while (scale != limit
            && scale < framebufferWidth
            && scale < framebufferHeight
            && (framebufferWidth / (scale + 1)) >= MinWidth
            && (framebufferHeight / (scale + 1)) >= MinHeight)
        {
            scale++;
        }

        if (enforceUnicode && (scale % 2) != 0)
        {
            scale++;
        }

        return scale;
    }

    /// <summary>The virtual canvas produced by a scale.</summary>
    /// <param name="framebufferWidth">Device pixels across.</param>
    /// <param name="framebufferHeight">Device pixels down.</param>
    /// <param name="scale">Scale from <see cref="Calculate"/>.</param>
    public static (int Width, int Height) ScaledSize(int framebufferWidth, int framebufferHeight, int scale)
    {
        var divisor = Math.Max(1, scale);
        return (
            (int)Math.Ceiling(framebufferWidth / (double)divisor),
            (int)Math.Ceiling(framebufferHeight / (double)divisor));
    }
}
