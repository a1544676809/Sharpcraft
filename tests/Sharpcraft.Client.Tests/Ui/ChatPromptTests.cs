// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Ui;
using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Client.Tests.Ui;

/// <summary>
/// Where the chat prompt sits (docs/modules/ui.md 8.4). It is drawn against the canvas it is given, so the only way
/// it can float above the bottom is if the canvas is stale; these cases pin the arithmetic for several heights.
/// </summary>
public sealed class ChatPromptTests
{
    [Theory]
    [InlineData(270f)]
    [InlineData(540f)]
    [InlineData(1080f)]
    [InlineData(135f)]
    public void TheBoxSitsAgainstTheBottomOfTheCanvas(float canvasHeight)
    {
        var geometry = new UiGeometry();
        ChatPrompt.Draw(geometry, font: null, 480f, canvasHeight, new TextEditModel(), 0);

        var (top, bottom) = VerticalExtent(geometry);

        Assert.Equal(canvasHeight - ChatPrompt.BottomInset, bottom, 3);
        Assert.Equal(canvasHeight - ChatPrompt.BottomInset - ChatPrompt.Height, top, 3);
    }

    [Fact]
    public void TheBoxSpansTheCanvasWithTheReferenceInsets()
    {
        var geometry = new UiGeometry();
        ChatPrompt.Draw(geometry, font: null, 480f, 270f, new TextEditModel(), 0);

        var (left, right) = HorizontalExtent(geometry);

        Assert.Equal(ChatPrompt.InsetX, left, 3);
        Assert.Equal(480f - ChatPrompt.InsetX, right, 3);
    }

    [Fact]
    public void TypedTextAddsGlyphsAndTheCaret()
    {
        var model = new TextEditModel();
        model.Insert("hi".AsSpan());

        var geometry = new UiGeometry();
        ChatPrompt.Draw(geometry, Font(), 480f, 270f, model, elapsedMs: 0);

        // The box; each visible character twice, once for its shadow and once for the glyph; and the caret at the
        // end, which is the underscore glyph and also carries a shadow.
        Assert.Equal(7, geometry.QuadCount);
    }

    private static (float Top, float Bottom) VerticalExtent(UiGeometry geometry)
    {
        var vertices = geometry.Vertices;
        var top = float.MaxValue;
        var bottom = float.MinValue;

        for (var index = 0; index < 4 * 5; index += 5)
        {
            var y = vertices[index + 1];
            top = Math.Min(top, y);
            bottom = Math.Max(bottom, y);
        }

        return (top, bottom);
    }

    private static (float Left, float Right) HorizontalExtent(UiGeometry geometry)
    {
        var vertices = geometry.Vertices;
        var left = float.MaxValue;
        var right = float.MinValue;

        for (var index = 0; index < 4 * 5; index += 5)
        {
            var x = vertices[index];
            left = Math.Min(left, x);
            right = Math.Max(right, x);
        }

        return (left, right);
    }

    /// <summary>A sheet whose glyphs advance six pixels.</summary>
    private static BitmapFont Font()
    {
        const int size = 128;
        const int cell = 8;
        var sheet = new Sharpcraft.Common.Imaging.RgbaImage(
            size,
            size,
            new byte[size * size * Sharpcraft.Common.Imaging.RgbaImage.BytesPerPixel]);

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                if (x % cell >= 5)
                {
                    continue;
                }

                sheet.Pixels[(((y * size) + x) * Sharpcraft.Common.Imaging.RgbaImage.BytesPerPixel) + 3] = 255;
            }
        }

        return BitmapFont.FromSheet(sheet);
    }
}
