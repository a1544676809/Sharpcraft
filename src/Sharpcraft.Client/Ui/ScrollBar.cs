// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Ui;

/// <summary>
/// Draws the thin scroll bar a scrolled list shows on its right (docs/modules/ui.md 5.3).
/// </summary>
/// <remarks>
/// The reference draws it only while the list holds more items than fit, two pixels wide, with the thumb sized by the
/// visible fraction and moved by how far the list has been scrolled. A list that is scrolled far enough for the thumb
/// to leave the top edge is drawn slightly stronger, which is how it shows there is more above.
/// </remarks>
public static class ScrollBar
{
    /// <summary>Width of the bar.</summary>
    public const float Width = 2f;

    /// <summary>Colour of the bar's edge, the reference's own value.</summary>
    public const uint EdgeColour = 0xFFCCCCCCu;

    /// <summary>Colour while new items are waiting below.</summary>
    public const uint PendingColour = 0xFFCC3333u;

    /// <summary>Ordinary colour.</summary>
    // The reference's own value for the bar: 3355562 = 0x3333AA.
    public const uint NormalColour = 0xFF3333AAu;

    /// <summary>Draws the bar when the list overflows.</summary>
    /// <param name="geometry">Receives the quads.</param>
    /// <param name="left">Left edge, which the caller places just right of the list.</param>
    /// <param name="areaTop">Top of the visible area.</param>
    /// <param name="areaBottom">Bottom of the visible area.</param>
    /// <param name="itemCount">Items in the list.</param>
    /// <param name="pageSize">Items that fit.</param>
    /// <param name="offset">Items scrolled away from the newest.</param>
    /// <param name="pending">True when items arrived since the list was last scrolled.</param>
    public static void Draw(
        UiGeometry geometry,
        float left,
        float areaTop,
        float areaBottom,
        int itemCount,
        int pageSize,
        int offset,
        bool pending)
    {
        ArgumentNullException.ThrowIfNull(geometry);

        if (itemCount <= pageSize || pageSize <= 0)
        {
            return;
        }

        var visibleHeight = areaBottom - areaTop;
        var virtualHeight = (float)itemCount / pageSize * visibleHeight;
        var thumbHeight = Math.Max(4f, visibleHeight * visibleHeight / virtualHeight);
        var thumbBottom = areaBottom - (offset * visibleHeight / itemCount);
        var thumbTop = thumbBottom - thumbHeight;

        byte alpha = offset > 0 ? (byte)170 : (byte)96;
        var colour = pending ? PendingColour : NormalColour;

        geometry.AddRectangle(left, thumbTop, Width, thumbHeight, WithAlpha(colour, alpha));
        geometry.AddRectangle(left + Width, thumbTop, 1f, thumbHeight, WithAlpha(EdgeColour, alpha));
    }

    private static uint WithAlpha(uint argb, byte alpha) => (argb & 0x00FFFFFFu) | ((uint)alpha << 24);
}
