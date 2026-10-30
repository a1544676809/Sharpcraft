// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Ui;

/// <summary>
/// Scroll position of a list, in whole items, with the reference's clamping rules (docs/modules/ui.md 5.3).
/// </summary>
/// <remarks>
/// Kept apart from any particular list so the chat, a server list and a book page can share it. The position counts
/// items scrolled away from the newest, which is the same direction the reference uses: zero means the newest item is
/// visible and the list is not scrolled.
/// </remarks>
public sealed class ScrollState
{
    /// <summary>Items scrolled away from the newest.</summary>
    public int Offset { get; private set; }

    /// <summary>True when the list is scrolled at all.</summary>
    public bool IsScrolled => Offset > 0;

    /// <summary>Moves the position by whole items and clamps it to the list.</summary>
    /// <param name="delta">Items to move; positive scrolls towards older items.</param>
    /// <param name="itemCount">Total items in the list.</param>
    /// <param name="pageSize">Items that fit on screen.</param>
    public void ScrollBy(int delta, int itemCount, int pageSize)
    {
        var limit = itemCount - Math.Max(1, pageSize);
        Offset = Math.Clamp(Offset + delta, 0, Math.Max(0, limit));
    }

    /// <summary>Returns to the newest item.</summary>
    public void Reset() => Offset = 0;
}
