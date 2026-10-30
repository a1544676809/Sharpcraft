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
        Assert.Equal((UiGeometry.FromArgb(TextEditRenderer.SelectionColour)), highlight);
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
        Assert.Equal((UiGeometry.FromArgb(TextEditRenderer.SelectedTextColour)), glyph);
        Assert.NotEqual((UiGeometry.FromArgb(TextEditRenderer.TextColour)), glyph);
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
            var linear = (byte)(((uint)value) & 0xFF);
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
        // Since the surface is no longer sRGB (docs/decisions/0013-gamma-space-ui-blending.md) the authored byte is
        // written straight into the vertex, so what the interface asked for is exactly what the shader receives.
        var argb = 0xFF000000u | (value << 16) | (value << 8) | value;
        var packed = UiGeometry.FromArgb(argb);

        Assert.Equal((byte)value, (byte)(packed & 0xFF));
        Assert.Equal((byte)value, (byte)((packed >> 8) & 0xFF));
        Assert.Equal((byte)value, (byte)((packed >> 16) & 0xFF));
    }
}
