// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Ui;

/// <summary>
/// Answers which chat entry and which run of it a point is over (docs/modules/ui.md 8.6).
/// </summary>
/// <remarks>
/// Kept free of drawing and of the message model so the arithmetic can be tested on its own: a pointer is translated
/// into a row and then into a run, and both steps have to account for the scroll offset and for rows being filled
/// from the bottom up.
/// </remarks>
public static class ChatHitTest
{
    /// <summary>
    /// Finds the entry under a point, counting from the newest entry as the bottom row.
    /// </summary>
    /// <param name="x">Pointer x in canvas pixels.</param>
    /// <param name="y">Pointer y in canvas pixels.</param>
    /// <param name="left">Left edge of the message column.</param>
    /// <param name="columnWidth">Width of the message column.</param>
    /// <param name="bottom">Bottom edge of the history, which is where the newest entry ends.</param>
    /// <param name="entryHeight">Height of one entry.</param>
    /// <param name="visibleRows">Entries that fit on screen.</param>
    /// <param name="entryCount">Entries in the history.</param>
    /// <param name="scrollOffset">Entries scrolled away from the newest.</param>
    /// <returns>Entry position from the newest, or -1 when the point is outside.</returns>
    public static int EntryAt(
        float x,
        float y,
        float left,
        float columnWidth,
        float bottom,
        float entryHeight,
        int visibleRows,
        int entryCount,
        int scrollOffset)
    {
        if (entryHeight <= 0 || visibleRows <= 0 || entryCount <= 0)
        {
            return -1;
        }

        var top = bottom - (visibleRows * entryHeight);

        if (x < left || x > left + columnWidth || y < top || y >= bottom)
        {
            return -1;
        }

        var rowFromBottom = (int)((bottom - y) / entryHeight);
        var index = scrollOffset + rowFromBottom;

        return index < entryCount ? index : -1;
    }

    /// <summary>
    /// Finds the run under a horizontal position within one entry.
    /// </summary>
    /// <param name="x">Pointer x in canvas pixels.</param>
    /// <param name="left">Left edge of the text.</param>
    /// <param name="widths">Width of each run, left to right.</param>
    /// <returns>Run index, or -1 when the point is past the text.</returns>
    /// <remarks>
    /// The right edge of a run counts as being over it, because the reference lets a click land on the boundary rather
    /// than requiring the pointer strictly inside the glyphs.
    /// </remarks>
    public static int RunAt(float x, float left, ReadOnlySpan<float> widths)
    {
        var pen = left;

        for (var index = 0; index < widths.Length; index++)
        {
            var next = pen + widths[index];

            if (x >= pen && x <= next)
            {
                return index;
            }

            pen = next;
        }

        return -1;
    }
}
