// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Text;
using Sharpcraft.Client.Ui;
using Sharpcraft.Common.Imaging;
using Xunit;

namespace Sharpcraft.Client.Tests.Ui;

/// <summary>
/// Glyph metrics from a font sheet (docs/modules/ui.md 5). The sheet here is synthetic: the real one is a game
/// asset and never enters the repository, but the rule for deriving a width from ink is what matters.
/// </summary>
public sealed class BitmapFontTests
{
    private const int SheetSize = 32;

    [Fact]
    public void AnInkedCellAdvancesByItsInkedWidthPlusSpacing()
    {
        // Two pixel cells on a thirty two pixel sheet: the scale from sheet pixels to font pixels is four.
        var sheet = BlankSheet();
        Ink(sheet, x: 2, y: 8); // slot (1,4) is 'A', left column only.

        var font = BitmapFont.FromSheet(sheet);

        Assert.True(font.TryGet('A', out var glyph));
        Assert.Equal(1, glyph.Width);
        Assert.Equal(5, glyph.Advance); // round(1 * 4) + 1
    }

    [Fact]
    public void AnEmptyCellStillAdvances()
    {
        var font = BitmapFont.FromSheet(BlankSheet());

        Assert.True(font.TryGet(' ', out var glyph));
        Assert.Equal(0, glyph.Width);
        Assert.Equal(1, glyph.Advance);
    }

    [Fact]
    public void TheRightmostInkedColumnDefinesTheWidth()
    {
        var sheet = BlankSheet();
        Ink(sheet, x: 2, y: 8);  // left column of 'A'
        Ink(sheet, x: 3, y: 8);  // right column too

        var font = BitmapFont.FromSheet(sheet);

        Assert.True(font.TryGet('A', out var glyph));
        Assert.Equal(2, glyph.Width);
        Assert.Equal(9, glyph.Advance); // round(2 * 4) + 1
    }

    [Fact]
    public void AMeasuredLineIsTheSumOfAdvances()
    {
        var sheet = BlankSheet();
        Ink(sheet, x: 2, y: 8);

        var font = BitmapFont.FromSheet(sheet);

        // 'A' advances five, the space one, the trailing 'A' five again.
        Assert.Equal(11, font.MeasureWidth("A A"));
        Assert.Equal(0, font.MeasureWidth(string.Empty));
    }

    [Fact]
    public void TextureCoordinatesCoverExactlyOneCell()
    {
        var font = BitmapFont.FromSheet(BlankSheet());

        Assert.True(font.TryGet('A', out var glyph));
        var (u0, v0, u1, v1) = font.Uv(glyph);

        Assert.Equal(2f / SheetSize, u0, 4);
        Assert.Equal(8f / SheetSize, v0, 4);
        Assert.Equal(4f / SheetSize, u1, 4);
        Assert.Equal(10f / SheetSize, v1, 4);
    }

    [Fact]
    public void ACharacterOutsideTheSheetIsNotInvented()
    {
        var font = BitmapFont.FromSheet(BlankSheet());

        Assert.False(font.TryGet('\u4e2d', out _));
        Assert.Equal(0, font.MeasureWidth("\u4e2d"));
    }

    [Fact]
    public void LineHeightAndAscentComeFromTheProvider()
    {
        var font = BitmapFont.FromSheet(BlankSheet(), height: 9, ascent: 8);

        Assert.Equal(9, font.Height);
        Assert.Equal(8, font.Ascent);
    }

    private static RgbaImage BlankSheet()
    {
        var sheet = new RgbaImage(SheetSize, SheetSize, new byte[SheetSize * SheetSize * RgbaImage.BytesPerPixel]);
        return sheet;
    }

    private static void Ink(RgbaImage sheet, int x, int y)
    {
        var offset = ((y * sheet.Width) + x) * RgbaImage.BytesPerPixel;

        sheet.Pixels[offset] = 255;
        sheet.Pixels[offset + 1] = 255;
        sheet.Pixels[offset + 2] = 255;
        sheet.Pixels[offset + 3] = 255;
    }
}
