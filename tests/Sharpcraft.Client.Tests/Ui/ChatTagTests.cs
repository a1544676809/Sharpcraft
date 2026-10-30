// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Ui;
using Sharpcraft.Common.Imaging;
using Sharpcraft.Common.Network;
using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Client.Tests.Ui;

/// <summary>
/// The indicator bar the reference draws at the left of a tagged chat line (docs/modules/ui.md 8.4). A system line
/// carries a pale bar, a chat error a red one, and a player message none at all, which is why only some lines show it.
/// </summary>
public sealed class ChatTagTests
{
    [Fact]
    public void ASystemLineDrawsAPaleBarAtTheLeftOfTheRow()
    {
        var log = new ChatLog();
        log.Add("joined the game", tick: 0, ChatLog.MessageTag.System);

        var geometry = new UiGeometry();
        log.Draw(geometry, Font(), screenHeight: 200f, nowTick: 0, fullyVisible: true);

        // Background, then the bar; the glyphs follow.
        var bar = ColourAt(geometry, quad: 1);
        Assert.Equal(UiGeometry.ToLinear(UiGeometry.FromArgb(ChatLog.SystemTagColour)), bar & 0x00FFFFFFu | (bar & 0xFF000000u));
    }

    [Fact]
    public void APlayerLineDrawsNoBar()
    {
        var log = new ChatLog();
        log.Add([new TextRun("hi", 0xFFFFFFFFu, Bold: false)], tick: 0);

        var geometry = new UiGeometry();
        log.Draw(geometry, Font(), screenHeight: 200f, nowTick: 0, fullyVisible: true);

        // Background first, then the glyphs: no bar in between.
        var second = ColourAt(geometry, quad: 1);
        Assert.NotEqual(UiGeometry.ToLinear(UiGeometry.FromArgb(ChatLog.SystemTagColour)), second);
    }

    [Fact]
    public void AChatErrorBarIsRed()
    {
        var log = new ChatLog();
        log.Add("Unknown command", tick: 0, ChatLog.MessageTag.Error);

        var geometry = new UiGeometry();
        log.Draw(geometry, Font(), screenHeight: 200f, nowTick: 0, fullyVisible: true);

        var bar = ColourAt(geometry, quad: 1);
        var red = (byte)(bar & 0xFF);
        var blue = (byte)((bar >> 16) & 0xFF);

        Assert.True(red > blue, "an error bar has to read as red");
    }

    [Fact]
    public void TheBarIsTwoPixelsWideAndSitsOnTheBackgroundEdge()
    {
        var log = new ChatLog();
        log.Add("joined the game", tick: 0, ChatLog.MessageTag.System);

        var geometry = new UiGeometry();
        log.Draw(geometry, Font(), screenHeight: 200f, nowTick: 0, fullyVisible: true);

        // Quad zero is the background and quad one is the bar, whose left edge is the chat inset. A quad stores its
        // left and right edges one after the other, so the width is the second float of the same vertex.
        Assert.Equal(ChatLog.LeftInset, geometry.Vertices[20], 3);
        Assert.Equal(ChatLog.TagBarWidth, geometry.Vertices[30] - geometry.Vertices[20], 3);
    }

    private static uint ColourAt(UiGeometry geometry, int quad) =>
        (uint)BitConverter.SingleToUInt32Bits(geometry.Vertices[(quad * 20) + 4]);

    /// <summary>A sheet whose glyphs advance six pixels.</summary>
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
