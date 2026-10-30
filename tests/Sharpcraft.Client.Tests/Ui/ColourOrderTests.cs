// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Ui;
using Sharpcraft.Common.Imaging;
using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Client.Tests.Ui;

/// <summary>
/// The byte order colours travel in (docs/modules/ui.md 9). Interface colours are written red first because that is
/// readable, while the vertex packs one channel per byte in memory order; mixing the two turns a red highlight blue,
/// which is exactly what happened once and is easy to reintroduce.
/// </summary>
public sealed class ColourOrderTests
{
    [Theory]
    [InlineData(0xFFFF0000u, 0xFF0000FFu)]   // red, written AARRGGBB, becomes blue-first in memory
    [InlineData(0xFF0000FFu, 0xFFFF0000u)]   // blue, written AARRGGBB
    [InlineData(0xFFFFAA00u, 0xFF00AAFFu)]   // gold
    [InlineData(0xFFE0E0E0u, 0xFFE0E0E0u)]   // grey is symmetrical, which is why the text looked right
    public void ColoursAreConvertedFromWrittenOrderToVertexOrder(uint written, uint packed) =>
        Assert.Equal(packed, UiGeometry.FromArgb(written));

    [Fact]
    public void ASelectionIsDrawnBlue()
    {
        var model = new TextEditModel();
        model.Insert("ab".AsSpan());
        model.SelectAll();

        var geometry = new UiGeometry();
        TextEditRenderer.Draw(geometry, Monospace(), model, 0f, 0f, focused: false, elapsedMs: 0);

        // The first quad is the highlight, so its colour word is the one to check.
        var highlight = (uint)BitConverter.SingleToUInt32Bits(geometry.Vertices[4]);
        Assert.Equal(UiGeometry.ToLinear(UiGeometry.FromArgb(TextEditRenderer.SelectionColour)), highlight);
    }

    [Fact]
    public void SelectedTextIsDrawnInTheInvertedColour()
    {
        var model = new TextEditModel();
        model.Insert("ab".AsSpan());
        model.SelectAll();

        var geometry = new UiGeometry();
        TextEditRenderer.Draw(geometry, Monospace(), model, 0f, 0f, focused: false, elapsedMs: 0);

        // Highlight first, then for each character its shadow and then the glyph itself.
        var glyph = (uint)BitConverter.SingleToUInt32Bits(geometry.Vertices[(2 * 20) + 4]);
        Assert.Equal(UiGeometry.ToLinear(UiGeometry.FromArgb(TextEditRenderer.SelectedTextColour)), glyph);
        Assert.NotEqual(UiGeometry.ToLinear(UiGeometry.FromArgb(TextEditRenderer.TextColour)), glyph);
    }

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

    [Fact]
    public void TheSrgbConversionIsMonotonic()
    {
        // The swap chain encodes what is written to it, so decoding has to preserve order: a brighter authored
        // colour must never end up darker. This is the check that would have caught the washed out colours.
        var previous = -1;

        for (var value = 0; value < 256; value++)
        {
            var linear = (byte)(UiGeometry.ToLinear((uint)value) & 0xFF);
            Assert.True(linear >= previous, $"linear value fell at {value}");
            previous = linear;
        }
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(32u)]
    [InlineData(85u)]
    [InlineData(127u)]
    [InlineData(160u)]
    [InlineData(192u)]
    [InlineData(200u)]
    [InlineData(224u)]
    [InlineData(255u)]
    public void TheShadesTheInterfaceUsesComeBackUnchanged(uint value)
    {
        // Decoding then letting the hardware encode must return the authored byte, which is what makes a pale blue
        // selection arrive pale rather than nearly white.
        var linear = (byte)(UiGeometry.ToLinear(value) & 0xFF);
        var channel = linear / 255.0;
        var encoded = (byte)Math.Round(
            255.0 * (channel <= 0.0031308 ? channel * 12.92 : (1.055 * Math.Pow(channel, 1 / 2.4)) - 0.055));

        // Two steps of tolerance: the linear value is stored as a byte, and for dark shades that byte is coarse
        // enough that rounding it and encoding it back moves the result by two.
        Assert.InRange(encoded, value <= 2 ? 0 : (int)value - 2, (int)value + 2);
    }
}
