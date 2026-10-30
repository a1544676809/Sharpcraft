// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Ui;
using Sharpcraft.Common.Imaging;
using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Client.Tests.Ui;

/// <summary>
/// How a long message is broken up and stacked (docs/modules/ui.md 8.4). Both halves matter and both were wrong once:
/// the fragments drifted sideways because the pen carried over, and they stacked upwards, which reads a message
/// backwards.
/// </summary>
public sealed class ChatWrapTests
{
    [Fact]
    public void EveryFragmentOfAWrappedEntryStartsAtTheSameIndent()
    {
        var log = new ChatLog();
        log.Add(new string('w', 120), tick: 0);

        var geometry = new UiGeometry();
        log.Draw(geometry, Font(), screenHeight: 200f, nowTick: 0, fullyVisible: true);

        var rows = Rows(geometry);

        Assert.True(rows.Count >= 3, $"expected at least three fragments, got {rows.Count}");

        foreach (var row in rows)
        {
            // The glyph sits at the indent and its shadow one pixel further right, so the leftmost is the indent.
            Assert.Equal(ChatLog.MessageIndent, row.Left, 3);
        }
    }

    [Fact]
    public void TheFragmentsRunFromTheTopOfTheGroupDownwards()
    {
        var log = new ChatLog();
        log.Add(new string('w', 120), tick: 0);

        var geometry = new UiGeometry();
        log.Draw(geometry, Font(), screenHeight: 200f, nowTick: 0, fullyVisible: true);

        var rows = Rows(geometry);
        var lowest = rows.MaxBy(r => r.Y);
        var highest = rows.MinBy(r => r.Y);

        // Yellow-less text of equal glyphs wraps into a long first fragment and a short last one; the short one has
        // to be the lowest on screen, otherwise the message reads upwards.
        Assert.True(lowest!.Ink < highest!.Ink, $"the last fragment should be the shortest, got {lowest.Ink} vs {highest.Ink}");
    }

    /// <summary>One drawn row: where it sits and how much ink it carries.</summary>
    private readonly record struct Row(float Y, float Left, float Ink);

    /// <summary>
    /// Collects one entry per drawn row. A vertex carries x, y, u, v and the colour, and a quad carries four of them,
    /// so the second vertex starts twenty floats later and its x is the quad's right edge.
    /// </summary>
    private static List<Row> Rows(UiGeometry geometry)
    {
        var backgrounds = new List<float>();
        var glyphs = new List<(float Y, float Left, float Right)>();

        for (var quad = 0; quad < geometry.QuadCount; quad++)
        {
            var offset = quad * 20;
            var left = geometry.Vertices[offset];
            var top = geometry.Vertices[offset + 1];
            var right = geometry.Vertices[offset + 5];
            var width = right - left;

            if (Math.Abs(width - (ChatLog.ColumnWidth + 8f)) < 0.01f)
            {
                backgrounds.Add(top);
            }
            else if (width > 0f && width < 9f)
            {
                glyphs.Add((top, left, right));
            }
        }

        var rows = new List<Row>();

        foreach (var backgroundTop in backgrounds)
        {
            var onRow = glyphs.Where(g => g.Y >= backgroundTop - 0.5f && g.Y <= backgroundTop + 3.5f).ToList();

            if (onRow.Count == 0)
            {
                continue;
            }

            var left = onRow.Min(g => g.Left);
            rows.Add(new Row(backgroundTop, left, onRow.Max(g => g.Right) - left));
        }

        return rows;
    }

    /// <summary>A sheet whose glyphs advance six pixels, so a 320 wide column fits about 53 characters.</summary>
    private static BitmapFont Font()
    {
        const int size = 128;
        const int cell = 8;
        var sheet = new RgbaImage(size, size, new byte[size * size * RgbaImage.BytesPerPixel]);

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                if (x % cell >= 5)
                {
                    continue;
                }

                sheet.Pixels[(((y * size) + x) * RgbaImage.BytesPerPixel) + 3] = 255;
            }
        }

        return BitmapFont.FromSheet(sheet);
    }
}
