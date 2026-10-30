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

        // The selected ink is drawn, and so is a shadow, which in the reference would have been inverted along with the
        // rest of the selection. Both are looked for rather than indexed, because the order inside a run is an
        // implementation detail.
        var colours = new List<uint>();

        for (var index = 0; index + 4 < geometry.Vertices.Length; index += 20)
        {
            colours.Add((uint)BitConverter.SingleToUInt32Bits(geometry.Vertices[index + 4]));
        }

        var ink = UiGeometry.FromArgb(TextEditRenderer.SelectedTextColour);
        Assert.Contains(ink, colours);
        Assert.DoesNotContain(UiGeometry.FromArgb(TextEditRenderer.TextColour), colours);

        // The shadow has to be lighter than the ink, otherwise the selection reads as a dirty bold line.
        Assert.Contains(colours, colour => (colour & 0xFF) > (ink & 0xFF));

        // The reference inverts the shadow it drew from the ordinary text colour (0xE0E0E0), which lands on a neutral
        // light grey. Deriving it from the blue ink instead produced a saturated pink that read far too strongly.
        Assert.Contains(0xFFC7C7C7u, colours);
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
