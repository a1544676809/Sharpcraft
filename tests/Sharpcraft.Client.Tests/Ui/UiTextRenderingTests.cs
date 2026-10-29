// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Ui;
using Sharpcraft.Common.Imaging;
using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Client.Tests.Ui;

/// <summary>
/// Text drawn through the interface layer (docs/modules/ui.md 9). Two things matter: panels and text must not
/// collapse into one range, because they need different pipelines, and glyphs must land on their advances so a
/// line of text is not a smear.
/// </summary>
public sealed class UiTextRenderingTests
{
    [Fact]
    public void ColourThenTextProducesTwoRangesWithDifferentMaterials()
    {
        var geometry = new UiGeometry();
        geometry.SetScissor(0, 0, 100, 100);

        geometry.AddRectangle(0, 0, 50, 10, 0);
        geometry.AddTexturedRectangle(0, 10, 8, 8, 0f, 0f, 0.1f, 0.1f, 0);

        Assert.Equal(2, geometry.Ranges.Length);
        Assert.Equal(UiMaterial.Colour, geometry.Ranges[0].Material);
        Assert.Equal(UiMaterial.Text, geometry.Ranges[1].Material);
        Assert.Equal(1, geometry.Ranges[0].IndexCount / 6);
        Assert.Equal(6, geometry.Ranges[1].FirstIndex);
    }

    [Fact]
    public void AlternatingBackAndForthKeepsOnlyTheRangesThatDraw()
    {
        var geometry = new UiGeometry();
        geometry.SetScissor(0, 0, 100, 100);

        // Two colours in a row stay in one range; text then colour opens and closes.
        geometry.AddRectangle(0, 0, 10, 10, 0);
        geometry.AddRectangle(10, 0, 10, 10, 0);
        geometry.AddTexturedRectangle(0, 10, 8, 8, 0, 0, 1, 1, 0);
        geometry.AddRectangle(0, 20, 10, 10, 0);

        Assert.Equal(3, geometry.Ranges.Length);
        Assert.Equal(2, geometry.Ranges[0].IndexCount / 6);
        Assert.Equal(UiMaterial.Text, geometry.Ranges[1].Material);
        Assert.Equal(UiMaterial.Colour, geometry.Ranges[2].Material);
    }

    [Fact]
    public void ATextBlockBecomesOneQuadPerVisibleCharacter()
    {
        var sheet = SheetWithEveryGlyph(width: 2);
        var font = BitmapFont.FromSheet(sheet);
        const string document = """
<Screen Padding="0">
  <TextBlock Text="AB C" Colour="#FFFFFFFF" />
</Screen>
""";

        var root = UiMarkup.Parse(document);
        var styles = UiStyles.Compile(root);
        var geometry = new UiGeometry();

        UiScreenLayout.Build(root, styles, 200f, 100f, geometry, font, textScale: 2f);

        // Three visible characters, the space advances without drawing, and they share one range because they
        // have the same material and scissor.
        Assert.Equal(3, geometry.QuadCount);
        Assert.Equal(1, geometry.Ranges.Length);
        Assert.Equal(UiMaterial.Text, geometry.Ranges[0].Material);
    }

    [Fact]
    public void GlyphsAdvanceByTheMetricsNotTheCellWidth()
    {
        // Every cell carries ink in its first column only. The sheet is 128 pixels wide with sixteen cells, so a
        // cell is eight pixels and the scale to the eight pixel line is one: the advance is round(1 * 1) + 1 = 2
        // font pixels, times a text scale of two, so the second glyph starts at four.
        var font = BitmapFont.FromSheet(SheetWithEveryGlyph(width: 1));
        const string document = """
<Screen Padding="0">
  <TextBlock Text="AA" Colour="#FFFFFFFF" />
</Screen>
""";

        var root = UiMarkup.Parse(document);
        var styles = UiStyles.Compile(root);
        var geometry = new UiGeometry();

        UiScreenLayout.Build(root, styles, 200f, 100f, geometry, font, textScale: 2f);

        var vertices = geometry.Vertices;

        // First glyph at x = 0; second begins at one advance of ten virtual pixels.
        Assert.Equal(0f, vertices[0], 3);
        Assert.Equal(4f, vertices[20], 3);
    }

    [Fact]
    public void WithoutAFontThePlaceholderBarsAreStillUsed()
    {
        const string document = """
<Screen Padding="0">
  <TextBlock Text="AB" Colour="#FFFFFFFF" />
</Screen>
""";

        var root = UiMarkup.Parse(document);
        var styles = UiStyles.Compile(root);
        var geometry = new UiGeometry();

        UiScreenLayout.Build(root, styles, 200f, 100f, geometry, font: null);

        Assert.Equal(2, geometry.QuadCount);
        Assert.Equal(UiMaterial.Colour, geometry.Ranges[0].Material);
    }

    /// <summary>A synthetic sheet whose four columns of cells each carry a one pixel wide mark.</summary>
    private static RgbaImage SheetWithEveryGlyph(int width)
    {
        const int size = 128;
        const int cell = 8;
        var sheet = new RgbaImage(size, size, new byte[size * size * RgbaImage.BytesPerPixel]);

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                if (x % cell >= width)
                {
                    continue;
                }

                var offset = (((y * size) + x) * RgbaImage.BytesPerPixel) + 3;
                sheet.Pixels[offset] = 255;
            }
        }

        return sheet;
    }
}
