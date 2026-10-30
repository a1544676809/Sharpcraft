// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Ui;

/// <summary>One cell of a nine slice: where it lands and which part of the sprite it shows.</summary>
/// <param name="X">Destination left.</param>
/// <param name="Y">Destination top.</param>
/// <param name="Width">Destination width.</param>
/// <param name="Height">Destination height.</param>
/// <param name="U0">Sprite left, in sprite pixels.</param>
/// <param name="V0">Sprite top, in sprite pixels.</param>
/// <param name="U1">Sprite right, in sprite pixels.</param>
/// <param name="V1">Sprite bottom, in sprite pixels.</param>
public readonly record struct NineSliceCell(
    float X, float Y, float Width, float Height, float U0, float V0, float U1, float V1);

/// <summary>
/// Splits a sprite into cells so its corners keep their pixels while its edges and middle stretch
/// (docs/analysis/gui/tooltip-background.md).
/// </summary>
/// <remarks>
/// Pure geometry, kept away from the renderer so the stretching can be checked without a GPU. The sizes are in
/// <em>sprite</em> pixels, which a resource pack may declare independently of the image, so the caller passes the
/// nominal size rather than the file's dimensions. Corners shrink when the destination cannot hold them, otherwise the
/// centre would take a negative size.
/// </remarks>
public static class NineSlice
{
    /// <summary>Computes the nine cells for per-side borders and a nominal source size.</summary>
    /// <param name="x">Destination left.</param>
    /// <param name="y">Destination top.</param>
    /// <param name="width">Destination width.</param>
    /// <param name="height">Destination height.</param>
    /// <param name="sourceWidth">Nominal sprite width.</param>
    /// <param name="sourceHeight">Nominal sprite height.</param>
    /// <param name="border">Corner sizes per side.</param>
    public static List<NineSliceCell> Cells(
        float x,
        float y,
        float width,
        float height,
        float sourceWidth,
        float sourceHeight,
        GuiSpriteBorder border)
    {
        var cells = new List<NineSliceCell>(9);

        if (width <= 0f || height <= 0f || sourceWidth <= 0f || sourceHeight <= 0f)
        {
            return cells;
        }

        var left = MathF.Min(border.Left, MathF.Min(sourceWidth / 2f, width / 2f));
        var right = MathF.Min(border.Right, MathF.Min(sourceWidth / 2f, width / 2f));
        var top = MathF.Min(border.Top, MathF.Min(sourceHeight / 2f, height / 2f));
        var bottom = MathF.Min(border.Bottom, MathF.Min(sourceHeight / 2f, height / 2f));

        if (left <= 0f && right <= 0f && top <= 0f && bottom <= 0f)
        {
            cells.Add(new NineSliceCell(x, y, width, height, 0f, 0f, sourceWidth, sourceHeight));
            return cells;
        }

        var middleWidth = MathF.Max(0f, width - left - right);
        var middleHeight = MathF.Max(0f, height - top - bottom);
        var sourceMiddleWidth = MathF.Max(0f, sourceWidth - left - right);
        var sourceMiddleHeight = MathF.Max(0f, sourceHeight - top - bottom);

        var columns = new (float X, float Width, float U0, float U1)[]
        {
            (x, left, 0f, left),
            (x + left, middleWidth, left, left + sourceMiddleWidth),
            (x + left + middleWidth, right, sourceWidth - right, sourceWidth)
        };

        var rows = new (float Y, float Height, float V0, float V1)[]
        {
            (y, top, 0f, top),
            (y + top, middleHeight, top, top + sourceMiddleHeight),
            (y + top + middleHeight, bottom, sourceHeight - bottom, sourceHeight)
        };

        foreach (var row in rows)
        {
            if (row.Height <= 0f)
            {
                continue;
            }

            foreach (var column in columns)
            {
                if (column.Width <= 0f)
                {
                    continue;
                }

                cells.Add(new NineSliceCell(
                    column.X, row.Y, column.Width, row.Height,
                    column.U0, row.V0, column.U1, row.V1));
            }
        }

        return cells;
    }

    /// <summary>Computes the nine cells for a uniform border.</summary>
    /// <param name="x">Destination left.</param>
    /// <param name="y">Destination top.</param>
    /// <param name="width">Destination width.</param>
    /// <param name="height">Destination height.</param>
    /// <param name="spriteWidth">Nominal sprite width.</param>
    /// <param name="spriteHeight">Nominal sprite height.</param>
    /// <param name="border">Corner size, the same on every side.</param>
    public static List<NineSliceCell> Cells(
        float x,
        float y,
        float width,
        float height,
        float spriteWidth,
        float spriteHeight,
        int border) =>
        Cells(x, y, width, height, spriteWidth, spriteHeight, GuiSpriteBorder.Uniform(border));
}
