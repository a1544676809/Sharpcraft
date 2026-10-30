// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Text;
using Sharpcraft.Common.Network;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Ui;

/// <summary>
/// The chat log as it is drawn on screen (docs/modules/ui.md 8.4). Every number here is the reference's own.
/// </summary>
/// <remarks>
/// A line stays fully visible for most of its life and then fades over the last second, which is what makes old chat
/// unobtrusive without a timer per line. Lines keep their styled runs: flattening them to one colour would lose the
/// difference between a team name, an ordinary message and an error.
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

    /// <summary>
    /// Distance from the screen edge to the left edge of the chat background.
    /// </summary>
    /// <remarks>
    /// Measured from the reference rather than derived: its chat box starts two interface pixels in from the edge,
    /// while the text inside starts four. Drawing the box at the edge instead made the whole column sit two pixels
    /// to the left of where it belongs.
    /// </remarks>
    public const float LeftInset = 2f;

    /// <summary>Width of the tag indicator bar drawn at the left of a tagged line.</summary>
    public const float TagBarWidth = 2f;

    /// <summary>Colour of the indicator for a server or client system message.</summary>
    public const uint SystemTagColour = 0xFFD0D0D0u;

    /// <summary>Colour of the indicator for a chat error.</summary>
    public const uint ErrorTagColour = 0xFFFF5555u;

    /// <summary>Colour of the indicator for a message the client altered.</summary>
    public const uint ModifiedTagColour = 0xFF606060u;

    /// <summary>Background wash behind each line.</summary>
    public const float BackgroundOpacity = 0.5f;

    /// <summary>Text opacity at the default setting.</summary>
    public const float TextOpacity = 1f;

    private readonly List<Line> _lines = [];
    private readonly ScrollState _scroll = new();

    /// <summary>
    /// Marks a line the way the reference does, with a bar at its left.
    /// </summary>
    /// <remarks>
    /// The reference attaches a tag to lines that did not come from a player: system messages carry a pale bar, chat
    /// errors a red one, and messages the client altered a dark one. Player messages carry no tag and no bar.
    /// </remarks>
    public enum MessageTag
    {
        /// <summary>No bar, which is what a player message has.</summary>
        None,

        /// <summary>System message, drawn with a pale bar.</summary>
        System,

        /// <summary>Chat error, drawn with a red bar.</summary>
        Error,

        /// <summary>Message the client altered, drawn with a dark bar.</summary>
        Modified,

        /// <summary>
        /// Player message that arrived without a signature, which is what a server that does not sign chat produces.
        /// The reference marks these as not secure and gives them the same pale bar as a system line.
        /// </summary>
        NotSecure
    }

    /// <summary>Lines held, oldest first.</summary>
    public int Count => _lines.Count;

    /// <summary>Lines that fit on one page.</summary>
    public static int LinesPerPage => VisibleHeight / EntryHeight;

    /// <summary>Adds a plain line, which is drawn in white.</summary>
    /// <param name="text">Line text.</param>
    /// <param name="tick">Current tick, used for the fade.</param>
    public void Add(string text, long tick, MessageTag tag = MessageTag.None) =>
        Add([new TextRun(text, ComponentRuns.DefaultColour, Bold: false)], tick, tag);

    /// <summary>Adds a line made of styled runs.</summary>
    /// <param name="runs">Runs in order.</param>
    /// <param name="tick">Current tick, used for the fade.</param>
    public void Add(
        ReadOnlySpan<TextRun> runs,
        long tick,
        MessageTag tag = MessageTag.None,
        ChatClick?[]? clicks = null,
        TextRun[]?[]? hovers = null)
    {
        if (runs.Length == 0)
        {
            return;
        }

        var builder = new StringBuilder();
        var empty = true;

        for (var index = 0; index < runs.Length; index++)
        {
            builder.Append(runs[index].Text);
            empty &= string.IsNullOrWhiteSpace(runs[index].Text);
        }

        if (empty)
        {
            return;
        }

        _lines.Add(new Line(runs.ToArray(), tick, builder.ToString(), tag, clicks, hovers));

        if (_lines.Count > MaxHistory)
        {
            _lines.RemoveAt(0);
        }
    }

    /// <summary>Colour of a tag's indicator bar, or zero when the line has none.</summary>
    private static uint ColourOf(MessageTag tag) =>
        tag switch
        {
            MessageTag.System => SystemTagColour,
            MessageTag.Error => ErrorTagColour,
            MessageTag.Modified => ModifiedTagColour,
            MessageTag.NotSecure => SystemTagColour,
            _ => 0u
        };

    /// <summary>Indicator tag of one entry, which is what its bar's tooltip describes.</summary>
    /// <param name="index">Entry index, oldest first.</param>
    public MessageTag TagFor(int index) =>
        index >= 0 && index < _lines.Count ? _lines[index].Tag : MessageTag.None;

    /// <summary>Runs of one entry, for measuring where a pointer lands.</summary>
    /// <param name="index">Entry index, oldest first.</param>
    public TextRun[] RunsAt(int index) =>
        index >= 0 && index < _lines.Count ? _lines[index].Runs : [];

    /// <summary>Click attached to a run, or null when that run is not clickable.</summary>
    /// <param name="index">Entry index, oldest first.</param>
    /// <param name="run">Run index within the entry.</param>
    /// <remarks>
    /// The interaction arrays are parallel to the runs and may be shorter or absent, so both bounds are checked here
    /// rather than at every call site.
    /// </remarks>
    public ChatClick? ClickFor(int index, int run)
    {
        if (index < 0 || index >= _lines.Count || run < 0)
        {
            return null;
        }

        var clicks = _lines[index].Clicks;

        return clicks is not null && run < clicks.Length ? clicks[run] : null;
    }

    /// <summary>Hover text attached to a run, or null when it has none.</summary>
    /// <param name="index">Entry index, oldest first.</param>
    /// <param name="run">Run index within the entry.</param>
    public TextRun[]? HoverFor(int index, int run)
    {
        if (index < 0 || index >= _lines.Count || run < 0)
        {
            return null;
        }

        var hovers = _lines[index].Hovers;

        return hovers is not null && run < hovers.Length ? hovers[run] : null;
    }

    /// <summary>Scroll position, in whole entries from the newest.</summary>
    public ScrollState Scroll => _scroll;

    /// <summary>Scrolls by whole entries, clamped the way the reference clamps it.</summary>
    /// <param name="delta">Entries to move; positive goes towards older messages.</param>
    public void ScrollBy(int delta) => _scroll.ScrollBy(delta, _lines.Count, LinesPerPage);

    /// <summary>Returns to the newest entry, which is what closing the prompt does.</summary>
    public void ResetScroll() => _scroll.Reset();

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

        for (var index = _lines.Count - 1 - _scroll.Offset; index >= 0 && row < page; index--)
        {
            var line = _lines[index];
            // The reference keeps every line at full opacity while the list is scrolled, so reading back does
            // not fade under the reader.
            var alpha = fullyVisible || _scroll.IsScrolled ? 1f : AlphaFor(nowTick - line.AddedTick);

            if (alpha <= 0.00001f)
            {
                continue;
            }

            // A long entry wraps into several rows, and they read downwards: the first fragment belongs on the
            // highest row of the group, the last on the lowest. Each fragment starts at the same indent, so the pen
            // must not carry over from the previous one.
            var wrapped = new Wrapped(line.Text, font, ColumnWidth);
            var fragmentCount = 0;

            foreach (var _ in wrapped)
            {
                fragmentCount++;
            }

            var fragment = 0;

            foreach (var piece in wrapped)
            {
                var pieceRow = row + (fragmentCount - 1 - fragment);
                fragment++;

                if (pieceRow >= page)
                {
                    continue;
                }

                var entryBottom = bottom - (pieceRow * EntryHeight);
                var entryTop = entryBottom - EntryHeight;

                geometry.AddRectangle(
                    LeftInset,
                    entryTop,
                    ColumnWidth + 8f,
                    EntryHeight,
                    Pack(0x00000000u, alpha * BackgroundOpacity));

                var tagColour = ColourOf(line.Tag);

                if (tagColour != 0u)
                {
                    // The reference puts the bar entirely outside the box, its right edge against the box's left edge:
                    // measured on its own framebuffer, the bar covers canvas 0..2 while the box starts at 2.
                    geometry.AddRectangle(
                        LeftInset - TagBarWidth,
                        entryTop,
                        TagBarWidth,
                        EntryHeight,
                        Pack((tagColour), alpha * TextOpacity));
                }

                DrawPiece(geometry, font, line, piece.Start, piece.Length, MessageIndent, entryBottom - 8f, alpha);
            }

            row += fragmentCount;
        }

        return row;
    }

    private static void DrawPiece(
        UiGeometry geometry,
        BitmapFont font,
        Line line,
        int start,
        int length,
        float x,
        float y,
        float alpha)
    {
        var end = start + length;
        var penX = x;
        var cursor = start;
        var runIndex = 0;
        var runStart = 0;

        while (cursor < end && runIndex < line.Runs.Length)
        {
            // Skip runs that end before the piece begins.
            while (runIndex < line.Runs.Length && runStart + line.Runs[runIndex].Text.Length <= cursor)
            {
                runStart += line.Runs[runIndex].Text.Length;
                runIndex++;
            }

            if (runIndex >= line.Runs.Length)
            {
                break;
            }

            var run = line.Runs[runIndex];
            var runEnd = runStart + run.Text.Length;
            var sliceEnd = Math.Min(end, runEnd);
            var sliceLength = sliceEnd - cursor;

            if (sliceLength <= 0)
            {
                cursor = sliceEnd;
                continue;
            }

            var slice = run.Text.AsSpan(cursor - runStart, sliceLength);
            var colour = Pack((run.Colour), alpha * TextOpacity);
            var startX = penX;
            UiScreenLayout.AddText(geometry, font, slice, startX, y, 1f, colour);

            if (run.Bold)
            {
                // Bold is the same glyphs repeated one pixel to the right, which is how the reference fakes it.
                UiScreenLayout.AddText(geometry, font, slice, startX + 1f, y, 1f, colour);
            }

            penX += font.MeasureWidth(slice);
            cursor = sliceEnd;
        }
    }

    private static uint Pack(uint colour, float alpha)
    {
        var alphaByte = (uint)Math.Clamp((int)(alpha * 255f), 0, 255);
        return (colour & 0x00FFFFFFu) | (alphaByte << 24);
    }

    private readonly record struct Line(
        TextRun[] Runs,
        long AddedTick,
        string Text,
        MessageTag Tag,
        ChatClick?[]? Clicks = null,
        TextRun[]?[]? Hovers = null);

    /// <summary>Greedy word wrapping over a line, reporting where each piece starts, allocating nothing.</summary>
    private readonly ref struct Wrapped(string text, BitmapFont font, float width)
    {
        /// <summary>Enumerates the wrapped pieces.</summary>
        public Enumerator GetEnumerator() => new(text.AsSpan(), font, width);
    }

    /// <summary>Iterates wrapped pieces.</summary>
    private ref struct Enumerator(ReadOnlySpan<char> text, BitmapFont font, float width)
    {
        private readonly int _total = text.Length;
        private ReadOnlySpan<char> _rest = text;

        /// <summary>The current piece: where it starts in the line and how long it is.</summary>
        public (int Start, int Length) Current { get; private set; }

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

            Current = (_total - _rest.Length, length);
            _rest = _rest[length..];
            return true;
        }
    }
}
