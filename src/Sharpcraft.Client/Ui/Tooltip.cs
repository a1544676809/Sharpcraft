// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Network;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Ui;

/// <summary>
/// Draws the box that appears beside the pointer when a chat run has hover text (docs/modules/ui.md 8.6).
/// </summary>
/// <remarks>
/// The colours are the reference's: a near black background with a purple frame. The box is kept inside the canvas so a
/// run near an edge does not put its tip off screen.
/// </remarks>
public static class Tooltip
{
    /// <summary>Background of the box.</summary>
    public const uint BackgroundColour = 0xF0100010u;

    /// <summary>Top and left edge of the frame.</summary>
    public const uint FrameStartColour = 0x505000FFu;

    /// <summary>Bottom and right edge of the frame.</summary>
    public const uint FrameEndColour = 0x5028007Fu;

    /// <summary>Padding between the frame and the text.</summary>
    public const float Padding = 3f;

    /// <summary>Height of one line of tip text.</summary>
    public const float LineHeight = 9f;

    /// <summary>
    /// Adds the box to the interface geometry.
    /// </summary>
    /// <param name="geometry">Receives the quads.</param>
    /// <param name="font">Glyph metrics.</param>
    /// <param name="runs">Tip text, one line per run set element.</param>
    /// <param name="pointerX">Pointer x in canvas pixels.</param>
    /// <param name="pointerY">Pointer y in canvas pixels.</param>
    /// <param name="canvasWidth">Canvas width, used to keep the box on screen.</param>
    /// <param name="canvasHeight">Canvas height, used to keep the box on screen.</param>
    public static void Draw(
        UiGeometry geometry,
        BitmapFont font,
        TextRun[] runs,
        float pointerX,
        float pointerY,
        float canvasWidth,
        float canvasHeight)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(font);

        if (runs.Length == 0)
        {
            return;
        }

        var width = 0f;

        foreach (var run in runs)
        {
            width += font.MeasureWidth(run.Text);
        }

        var boxWidth = width + (Padding * 2f) + 2f;
        var boxHeight = LineHeight + (Padding * 2f) + 2f;

        // Prefer up and to the right of the pointer, then pull back inside the canvas.
        var left = MathF.Min(pointerX + 6f, canvasWidth - boxWidth - 2f);
        var top = MathF.Min(pointerY - boxHeight - 6f, canvasHeight - boxHeight - 2f);
        left = MathF.Max(2f, left);
        top = MathF.Max(2f, top);

        geometry.AddRectangle(left, top, boxWidth, boxHeight, BackgroundColour);
        geometry.AddRectangle(left, top, boxWidth, 1f, FrameStartColour);
        geometry.AddRectangle(left, top + boxHeight - 1f, boxWidth, 1f, FrameEndColour);
        geometry.AddRectangle(left, top, 1f, boxHeight, FrameStartColour);
        geometry.AddRectangle(left + boxWidth - 1f, top, 1f, boxHeight, FrameEndColour);

        var pen = left + Padding + 1f;
        var textTop = top + Padding + 1f;

        foreach (var run in runs)
        {
            UiScreenLayout.AddText(geometry, font, run.Text, pen, textTop, 1f, run.Colour, shadow: false);
            pen += font.MeasureWidth(run.Text);
        }
    }
}
