// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Ui;

/// <summary>
/// The chat log as it is drawn on screen (docs/modules/ui.md 8.4). Every number here is the reference's own.
/// </summary>
/// <remarks>
/// A line stays fully visible for most of its life and then fades over the last second, which is what makes old chat
/// unobtrusive without a timer per line: the alpha is computed from the line's age with the same curve the
/// reference uses. Wrapping happens at draw time against the real glyph metrics, so a line that does not fit breaks
/// where the font says it breaks rather than at an assumed character count.
/// </remarks>
public sealed class ChatLog
{
    /// <summary>Lines kept for scrollback.</summary>
    public const int MaxHistory = 100;

    /// <summary>Vertical pitch of a line.</summary>
    public const int EntryHeight = 9;

    /// <summary>Width of the text column.</summary>
    public const float ColumnWidth = 320f;

    /// <summary>Height of the visible area, which yields the lines per page.</summary>
    public const int VisibleHeight = 100;

    /// <summary>Left indent of the text.</summary>
    public const float MessageIndent = 4f;

    /// <summary>Distance from the bottom of the screen.</summary>
    public const float BottomMargin = 40f;

    /// <summary>Ticks after which a line has faded completely.</summary>
    public const long FadeTicks = 200;

    /// <summary>Background wash behind each line.</summary>
    public const float BackgroundOpacity = 0.5f;

    /// <summary>Text opacity at the default setting.</summary>
    public const float TextOpacity = 1f;

    private readonly List<(string Text, long AddedTick)> _lines = [];

    /// <summary>Lines held, oldest first.</summary>
    public int Count => _lines.Count;

    /// <summary>Lines that fit on one page.</summary>
    public static int LinesPerPage => VisibleHeight / EntryHeight;

    /// <summary>Adds a line, dropping the oldest once the history is full.</summary>
    /// <param name="text">Line text.</param>
    /// <param name="tick">Current tick, used for the fade.</param>
    public void Add(string text, long tick)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        _lines.Add((text, tick));

        if (_lines.Count > MaxHistory)
        {
            _lines.RemoveAt(0);
        }
    }

    /// <summary>Forgets every line, used when leaving a world.</summary>
    public void Clear() => _lines.Clear();

    /// <summary>
    /// How opaque a line is, given its age in ticks.
    /// </summary>
    /// <param name="ageTicks">Ticks since the line arrived.</param>
    /// <remarks>
    /// The curve is the reference's: the line is untouched until the last tenth of its life, then falls away
    /// squared. Reproducing the shape matters because it is what the fade looks like.
    /// </remarks>
    public static float AlphaFor(long ageTicks)
    {
        var remaining = 1d - ((double)ageTicks / FadeTicks);
        remaining *= 10d;

        if (remaining <= 0d)
        {
            return 0f;
        }

        if (remaining >= 1d)
        {
            return 1f;
        }

        return (float)(remaining * remaining);
    }

    /// <summary>Adds the visible chat to the interface geometry.</summary>
    /// <param name="geometry">Receives the quads.</param>
    /// <param name="font">Glyph metrics.</param>
    /// <param name="screenHeight">Virtual height, used to sit above the hotbar.</param>
    /// <param name="nowTick">Current tick.</param>
    /// <param name="fullyVisible">True while the prompt is open, which pins every line at full opacity.</param>
    /// <returns>Rows drawn.</returns>
    public int Draw(UiGeometry geometry, BitmapFont font, float screenHeight, long nowTick, bool fullyVisible)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(font);

        var bottom = (float)Math.Floor(screenHeight - BottomMargin);
        var row = 0;
        var page = LinesPerPage;

        for (var index = _lines.Count - 1; index >= 0 && row < page; index--)
        {
            var (text, addedTick) = _lines[index];
            var alpha = fullyVisible ? 1f : AlphaFor(nowTick - addedTick);

            if (alpha <= 0.00001f)
            {
                continue;
            }

            foreach (var span in new Wrapped(text, font, ColumnWidth))
            {
                if (row >= page)
                {
                    break;
                }

                var entryBottom = bottom - (row * EntryHeight);
                var entryTop = entryBottom - EntryHeight;

                geometry.AddRectangle(
                    MessageIndent - 4f,
                    entryTop,
                    ColumnWidth + 8f,
                    EntryHeight,
                    PackBlack(alpha * BackgroundOpacity));

                UiScreenLayout.AddText(
                    geometry,
                    font,
                    span,
                    MessageIndent,
                    entryBottom - 8f,
                    1f,
                    PackWhite(alpha * TextOpacity));

                row++;
            }
        }

        return row;
    }

    private static uint PackBlack(float alpha) =>
        (uint)Math.Clamp((int)(alpha * 255f), 0, 255) << 24;

    private static uint PackWhite(float alpha) =>
        0x00FFFFFFu | ((uint)Math.Clamp((int)(alpha * 255f), 0, 255) << 24);

    /// <summary>Greedy word wrapping over a line, allocating nothing.</summary>
    private readonly ref struct Wrapped(string text, BitmapFont font, float width)
    {
        /// <summary>Enumerates the wrapped pieces, breaking at spaces and, when a word cannot fit, mid word.</summary>
        public Enumerator GetEnumerator() => new(text.AsSpan(), font, width);
    }

    /// <summary>Iterates wrapped pieces.</summary>
    private ref struct Enumerator(ReadOnlySpan<char> text, BitmapFont font, float width)
    {
        private ReadOnlySpan<char> _rest = text;

        /// <summary>The current piece.</summary>
        public ReadOnlySpan<char> Current { get; private set; }

        /// <summary>Advances to the next piece.</summary>
        public bool MoveNext()
        {
            if (_rest.IsEmpty)
            {
                return false;
            }

            var pen = 0f;
            var lastSpace = -1;
            var index = 0;

            for (; index < _rest.Length; index++)
            {
                var advance = font.AdvanceOf(_rest[index]);

                if (pen + advance > width && index > 0)
                {
                    break;
                }

                if (_rest[index] == ' ')
                {
                    lastSpace = index;
                }

                pen += advance;
            }

            var length = index == _rest.Length
                ? index
                : lastSpace > 0
                    ? lastSpace + 1
                    : index;

            Current = _rest[..length];
            _rest = _rest[length..];
            return true;
        }
    }
}
