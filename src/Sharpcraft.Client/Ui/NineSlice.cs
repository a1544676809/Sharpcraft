// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Ui;

/// <summary>One cell of a nine slice: where it lands and which part of the sprite it shows.</summary>
/// <param name="X">Destination left.</param>
/// <param name="Y">Destination top.</param>
/// <param name="Width">Destination width.</param>
/// <param name="Height">Destination height.</param>
/// <param name="U0">Sprite left, in texels.</param>
/// <param name="V0">Sprite top, in texels.</param>
/// <param name="U1">Sprite right, in texels.</param>
/// <param name="V1">Sprite bottom, in texels.</param>
public readonly record struct NineSliceCell(
    float X, float Y, float Width, float Height, float U0, float V0, float U1, float V1);

/// <summary>
/// Splits a sprite into nine cells so its corners keep their pixels while its edges and middle stretch
/// (docs/analysis/gui/tooltip-background.md).
/// </summary>
/// <remarks>
/// This is pure geometry, kept away from the renderer so the stretching can be checked without a GPU. When the
/// destination is smaller than two corners the corner size is reduced, otherwise the edges would get a negative size.
/// </remarks>
public static class NineSlice
{
    /// <summary>
    /// Computes the nine cells, or four when the destination is too small for the middle band.
    /// </summary>
    /// <param name="x">Destination left.</param>
    /// <param name="y">Destination top.</param>
    /// <param name="width">Destination width.</param>
    /// <param name="height">Destination height.</param>
    /// <param name="spriteWidth">Sprite width in pixels.</param>
    /// <param name="spriteHeight">Sprite height in pixels.</param>
    /// <param name="border">Corner size in sprite pixels.</param>
    public static List<NineSliceCell> Cells(
        float x,
        float y,
        float width,
        float height,
        float spriteWidth,
        float spriteHeight,
        int border)
    {
        var cells = new List<NineSliceCell>(9);

        if (width <= 0f || height <= 0f || spriteWidth <= 0f || spriteHeight <= 0f)
        {
            return cells;
        }

        // A corner can never take more than half of either side, so the edges keep a non-negative size.
        var bx = Math.Min(border, MathF.Min(spriteWidth / 2f, width / 2f));
        var by = Math.Min(border, MathF.Min(spriteHeight / 2f, height / 2f));

        if (bx <= 0f || by <= 0f)
        {
            cells.Add(new NineSliceCell(x, y, width, height, 0f, 0f, spriteWidth, spriteHeight));
            return cells;
        }

        var middleWidth = width - (bx * 2f);
        var middleHeight = height - (by * 2f);
        var spriteMiddleWidth = spriteWidth - (bx * 2f);
        var spriteMiddleHeight = spriteHeight - (by * 2f);

        var columns = new (float X, float Width, float U0, float U1)[]
        {
            (x, bx, 0f, bx),
            (x + bx, middleWidth, bx, bx + spriteMiddleWidth),
            (x + bx + middleWidth, bx, spriteWidth - bx, spriteWidth)
        };

        var rows = new (float Y, float Height, float V0, float V1)[]
        {
            (y, by, 0f, by),
            (y + by, middleHeight, by, by + spriteMiddleHeight),
            (y + by + middleHeight, by, spriteHeight - by, spriteHeight)
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
}
