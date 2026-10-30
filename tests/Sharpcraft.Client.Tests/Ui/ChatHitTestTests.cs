// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Ui;
using Xunit;

namespace Sharpcraft.Client.Tests.Ui;

/// <summary>
/// Turning a pointer into a message and a run (docs/modules/ui.md 8.6). The arithmetic has to respect that rows fill
/// upward and that the scroll offset shifts which entry a row holds.
/// </summary>
public sealed class ChatHitTestTests
{
    // Column at x 2..322, history ending at y 200, eleven rows of 9, twenty entries, scrolled by three.
    private static int EntryAt(float x, float y) =>
        ChatHitTest.EntryAt(x, y, left: 2f, columnWidth: 320f, bottom: 200f, entryHeight: 9f,
            visibleRows: 11, entryCount: 20, scrollOffset: 3);

    [Fact]
    public void TheBottomRowIsTheOldestVisibleEntry()
    {
        // One pixel above the bottom is the newest visible row, which is three entries back from the newest.
        Assert.Equal(3, EntryAt(10f, 199f));
    }

    [Fact]
    public void TheTopRowIsAVisibleEntryNotTheNewest()
    {
        // The top row is ten rows up from the bottom, so index three plus ten.
        Assert.Equal(13, EntryAt(10f, 200f - (10 * 9f) - 1f));
    }

    [Fact]
    public void APointOutsideTheColumnIsNotAnEntry()
    {
        Assert.Equal(-1, EntryAt(1f, 100f));
        Assert.Equal(-1, EntryAt(400f, 100f));
        Assert.Equal(-1, EntryAt(10f, 199f + 5f));
        Assert.Equal(-1, EntryAt(10f, 100f - 1f));
    }

    [Fact]
    public void TheRunBoundaryBelongsToTheEarlierRun()
    {
        float[] widths = [10f, 20f];

        Assert.Equal(0, ChatHitTest.RunAt(2f, 2f, widths));
        Assert.Equal(0, ChatHitTest.RunAt(12f, 2f, widths));
        Assert.Equal(1, ChatHitTest.RunAt(13f, 2f, widths));
        Assert.Equal(1, ChatHitTest.RunAt(32f, 2f, widths));
        Assert.Equal(-1, ChatHitTest.RunAt(33f, 2f, widths));
    }
}

/// <summary>
/// The tip that appears beside a hovered run (docs/modules/ui.md 8.6). It wraps at the reference's width instead of
/// running off screen, and it keeps itself inside the canvas.
/// </summary>
public sealed class TooltipTests
{
    [Fact]
    public void AShortTipStaysOnOneLine()
    {
        var lines = Tooltip.Wrap([new Sharpcraft.Common.Network.TextRun("hi", 0xFFFFFFFFu, false)], Font(), Tooltip.MaxWidth);

        Assert.Single(lines);
        Assert.Equal("hi", string.Concat(lines[0].Select(run => run.Text)));
    }

    [Fact]
    public void ALongTipWrapsInsteadOfRunningOff()
    {
        var text = new string('a', 400);
        var lines = Tooltip.Wrap([new Sharpcraft.Common.Network.TextRun(text, 0xFFFFFFFFu, false)], Font(), Tooltip.MaxWidth);

        Assert.True(lines.Count > 1, "the tip has to break");

        foreach (var line in lines)
        {
            var width = line.Sum(run => Font().MeasureWidth(run.Text));
            Assert.True(width <= Tooltip.MaxWidth, $"line wider than the limit: {width}");
        }

        // Nothing is lost when the tip is broken.
        Assert.Equal(text.Length, lines.Sum(line => line.Sum(run => run.Text.Length)));
    }

    [Fact]
    public void TheBoxIsKeptInsideTheCanvas()
    {
        var geometry = new Sharpcraft.Framework.Rendering.UiGeometry();
        Tooltip.Draw(geometry, Font(), [new Sharpcraft.Common.Network.TextRun("tip", 0xFFFFFFFFu, false)],
            pointerX: 470f, pointerY: 4f, canvasWidth: 480f, canvasHeight: 270f);

        Assert.True(geometry.QuadCount > 0);
        Assert.True(geometry.Vertices[0] >= 0f);
        Assert.True(geometry.Vertices[1] >= 0f);
    }

    private static Sharpcraft.Client.Ui.BitmapFont Font() => ChatHitTestFonts.Create();
}
