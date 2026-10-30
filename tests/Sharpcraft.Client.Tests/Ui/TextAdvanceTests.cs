// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Ui;
using Sharpcraft.Common.Imaging;
using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Client.Tests.Ui;

/// <summary>
/// Where the drawing path puts each glyph (docs/modules/ui.md 5.1). Measuring and drawing have to agree: if drawing
/// advances by the glyph's ink width, a space collapses to a single pixel and every sentence loses its word breaks,
/// even though the measured width says otherwise.
/// </summary>
public sealed class TextAdvanceTests
{
    [Fact]
    public void ASpaceAdvancesByItsDeclaredWidthWhenDrawn()
    {
        var font = SpaceFont();
        font.SetAdvance(' ', 4);
        var geometry = new UiGeometry();

        UiScreenLayout.AddText(geometry, font, "a a", 0f, 0f, 1f, 0xFFFFFFFFu);

        // Two glyphs are drawn (the space is blank), and the second one starts at glyph advance plus space advance.
        Assert.Equal(2, geometry.QuadCount);

        var firstX = geometry.Vertices[0];
        var secondX = geometry.Vertices[20];

        Assert.Equal(0f, firstX, 3);
        Assert.Equal(font.AdvanceOf('a') + 4f, secondX, 3);
    }

    [Fact]
    public void DrawnWidthMatchesMeasuredWidth()
    {
        var font = SpaceFont();
        font.SetAdvance(' ', 4);
        var geometry = new UiGeometry();

        UiScreenLayout.AddText(geometry, font, "ab cd", 0f, 0f, 1f, 0xFFFFFFFFu);

        // The last drawn glyph ends where the measured width says the text ends.
        var lastGlyphX = geometry.Vertices[3 * 20];
        var lastGlyphWidth = font.CellWidth;

        Assert.Equal(font.MeasureWidth("ab c") + lastGlyphWidth, lastGlyphX + lastGlyphWidth, 3);
    }

    /// <summary>A sheet whose glyphs carry five inked columns and whose space cell is blank.</summary>
    private static BitmapFont SpaceFont()
    {
        const int size = 128;
        const int cell = 8;
        const int columns = 16;
        var sheet = new RgbaImage(size, size, new byte[size * size * RgbaImage.BytesPerPixel]);

        var spaceX = (' ' % columns) * cell;
        var spaceY = (' ' / columns) * cell;

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                if (x % cell >= 5)
                {
                    continue;
                }

                if (x >= spaceX && x < spaceX + cell && y >= spaceY && y < spaceY + cell)
                {
                    continue;
                }

                sheet.Pixels[(((y * size) + x) * RgbaImage.BytesPerPixel) + 3] = 255;
            }
        }

        return BitmapFont.FromSheet(sheet);
    }
}
