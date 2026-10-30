// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Ui;
using Sharpcraft.Common.Imaging;
using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Client.Tests.Ui;

/// <summary>
/// The caret and selection a text field draws (docs/modules/ui.md 5.2). The two caret shapes are not cosmetic: inside
/// the text it is a bar, and at the end it is the font's underscore, so the end of the line reads as a place to type.
/// </summary>
public sealed class TextEditRendererTests
{
    [Theory]
    [InlineData(0, true)]
    [InlineData(299, true)]
    [InlineData(300, false)]
    [InlineData(599, false)]
    [InlineData(600, true)]
    public void TheCaretBlinksEveryThreeHundredMilliseconds(long elapsed, bool visible) =>
        Assert.Equal(visible, TextEditRenderer.IsCaretVisible(elapsed));

    [Fact]
    public void ACaretInsideTheTextIsABarAndAtTheEndItIsAnUnderscore()
    {
        var model = new TextEditModel();
        model.Insert("abc".AsSpan());

        // At the end.
        Assert.False(TextEditRenderer.UsesInsertCaret(model));

        model.MoveCaretTo(1);
        Assert.True(TextEditRenderer.UsesInsertCaret(model));

        model.End();
        Assert.False(TextEditRenderer.UsesInsertCaret(model));
    }

    [Fact]
    public void AFullFieldKeepsTheBarEvenAtTheEnd()
    {
        // There is nowhere to type, and the reference keeps the bar to say so.
        var model = new TextEditModel(maxLength: 2);
        model.Insert("ab".AsSpan());

        Assert.True(TextEditRenderer.UsesInsertCaret(model));
    }

    [Fact]
    public void TheCaretSitsAfterTheCharactersBeforeIt()
    {
        var font = Monospace();
        var model = new TextEditModel();
        model.Insert("abc".AsSpan());

        Assert.Equal(0f, TextEditRenderer.CaretOffset(font, model.Value, 0), 3);
        Assert.Equal(6f, TextEditRenderer.CaretOffset(font, model.Value, 1), 3);
        Assert.Equal(18f, TextEditRenderer.CaretOffset(font, model.Value, 3), 3);
    }

    [Fact]
    public void AClickPlacesTheCaretOnTheNearestBoundary()
    {
        var font = Monospace();
        const string value = "abc";

        // Each glyph is six wide, so the boundary between the first and second sits at six.
        Assert.Equal(0, TextEditRenderer.PositionAtOffset(font, value, 1f));
        Assert.Equal(1, TextEditRenderer.PositionAtOffset(font, value, 4f));
        Assert.Equal(1, TextEditRenderer.PositionAtOffset(font, value, 6f));
        Assert.Equal(2, TextEditRenderer.PositionAtOffset(font, value, 10f));
        Assert.Equal(3, TextEditRenderer.PositionAtOffset(font, value, 400f));
    }

    [Fact]
    public void ASelectionIsDrawnBehindTheText()
    {
        var model = new TextEditModel();
        model.Insert("abcd".AsSpan());
        model.Home();
        model.MoveCaret(2, extendSelection: true);

        var geometry = new UiGeometry();
        TextEditRenderer.Draw(geometry, Monospace(), model, x: 0f, y: 0f, focused: false, elapsedMs: 0);

        // A highlight quad plus the glyphs for four characters.
        Assert.Equal(5, geometry.QuadCount);
    }

    [Fact]
    public void AnUnfocusedFieldDrawsNoCaret()
    {
        var model = new TextEditModel();
        model.Insert("a".AsSpan());

        var geometry = new UiGeometry();
        TextEditRenderer.Draw(geometry, Monospace(), model, x: 0f, y: 0f, focused: false, elapsedMs: 0);

        // One glyph, no caret.
        Assert.Equal(1, geometry.QuadCount);
    }

    /// <summary>A sheet whose glyphs advance six pixels.</summary>
    private static BitmapFont Monospace()
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
