// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Ui;
using Sharpcraft.Common.Imaging;
using Xunit;

namespace Sharpcraft.Client.Tests.Ui;

/// <summary>
/// Advances declared by the font definition (docs/modules/ui.md 5). A space has no ink, so its width cannot come from
/// the glyph sheet; without the declared value every sentence draws with its words run together.
/// </summary>
public sealed class FontDefinitionTests
{
    private const string Space = """
{
  "providers": [
    { "type": "space", "advances": { " ": 4, "\u200c": 0 } }
  ]
}
""";

    private const string Root = """
{
  "providers": [
    { "type": "reference", "id": "minecraft:include/space" },
    { "type": "bitmap", "file": "minecraft:font/ascii.png", "ascent": 7, "chars": [] }
  ]
}
""";

    [Fact]
    public void ASpaceTakesItsAdvanceFromTheDefinition()
    {
        var font = BlankFont();

        // From the sheet alone a space is one pixel wide, which is the bug this exists to prevent.
        Assert.Equal(1, font.AdvanceOf(' '));

        var applied = FontDefinition.Apply(Space, _ => null, font);

        Assert.Equal(2, applied);
        Assert.Equal(4, font.AdvanceOf(' '));
    }

    [Fact]
    public void ReferencesAreFollowedToTheProviderThatDeclaresTheAdvance()
    {
        var font = BlankFont();

        var applied = FontDefinition.Apply(
            Root,
            reference => reference == "minecraft:include/space" ? Space : null,
            font);

        Assert.Equal(2, applied);
        Assert.Equal(4, font.AdvanceOf(' '));
    }

    [Fact]
    public void AMissingReferenceIsSkippedRatherThanFailing()
    {
        var font = BlankFont();

        Assert.Equal(0, FontDefinition.Apply(Root, _ => null, font));
        Assert.Equal(1, font.AdvanceOf(' '));
    }

    [Fact]
    public void TheWidthOfALineCountsTheDeclaredSpaces()
    {
        var font = BlankFont();
        FontDefinition.Apply(Space, _ => null, font);

        // The declared advance is what a line width counts for the space, whatever the glyphs around it measure.
        Assert.Equal(4, font.AdvanceOf(' '));
        Assert.Equal(
            font.AdvanceOf('a') + font.AdvanceOf(' ') + font.AdvanceOf('b'),
            font.MeasureWidth("a b"));
    }

    /// <summary>
    /// A sheet whose glyphs carry five inked columns, so each advances six pixels, with the space cell left blank the
    /// way a real sheet leaves it: that is what makes a space fall back to a one pixel advance without a definition.
    /// </summary>
    private static BitmapFont BlankFont()
    {
        const int size = 128;
        const int cell = 8;
        const int columns = 16;
        const int spaceCodepoint = ' ';
        var sheet = new RgbaImage(size, size, new byte[size * size * RgbaImage.BytesPerPixel]);

        var spaceCellX = (spaceCodepoint % columns) * cell;
        var spaceCellY = (spaceCodepoint / columns) * cell;

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                if (x % cell >= 5)
                {
                    continue;
                }

                var inSpaceCell = x >= spaceCellX && x < spaceCellX + cell &&
                    y >= spaceCellY && y < spaceCellY + cell;

                if (inSpaceCell)
                {
                    continue;
                }

                sheet.Pixels[(((y * size) + x) * RgbaImage.BytesPerPixel) + 3] = 255;
            }
        }

        return BitmapFont.FromSheet(sheet);
    }
}
