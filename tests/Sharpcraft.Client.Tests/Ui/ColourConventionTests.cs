// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Ui;
using Sharpcraft.Common.Imaging;
using Sharpcraft.Common.Network;
using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Client.Tests.Ui;

/// <summary>
/// Every colour the interface passes down is authored as ARGB and has to reach the vertex buffer as RGBA bytes
/// (docs/decisions/0013-gamma-space-ui-blending.md). The conversion belongs in one place; doing it at some call sites
/// and not others turned the tooltip's blue-violet frame into a red one.
/// </summary>
public sealed class ColourConventionTests
{
    [Fact]
    public void TheTooltipFrameKeepsItsBlueVioletColour()
    {
        var geometry = new UiGeometry();
        Tooltip.Draw(geometry, Font(), [new TextRun("tip", 0xFFFFFFFFu, false)], 40f, 40f, 480f, 270f);

        var colours = Colours(geometry);

        // Authored ARGB 0x505000FF is a blue-violet; as RGBA bytes that is 0x50,0x00,0xFF with alpha 0x50.
        var expected = UiGeometry.FromArgb(Tooltip.FrameStartColour);
        Assert.Contains(expected, colours);
        Assert.DoesNotContain(UiGeometry.FromArgb(0xFFCC3333u), colours);
    }

    [Fact]
    public void AnArgbColourReachesTheVertexAsRgba()
    {
        var geometry = new UiGeometry();
        geometry.AddRectangle(0f, 0f, 1f, 1f, 0xFF123456u);

        var packed = Colours(geometry)[0];

        Assert.Equal(0x12u, packed & 0xFF);          // red in the low byte
        Assert.Equal(0x34u, (packed >> 8) & 0xFF);   // green next
        Assert.Equal(0x56u, (packed >> 16) & 0xFF);  // blue next
        Assert.Equal(0xFFu, packed >> 24);
    }

    private static List<uint> Colours(UiGeometry geometry)
    {
        var colours = new List<uint>();

        for (var index = 0; index + 4 < geometry.Vertices.Length; index += 20)
        {
            colours.Add((uint)BitConverter.SingleToUInt32Bits(geometry.Vertices[index + 4]));
        }

        return colours;
    }

    private static BitmapFont Font()
    {
        const int size = 128;
        const int cell = 8;
        var sheet = new RgbaImage(size, size, new byte[size * size * RgbaImage.BytesPerPixel]);

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                if (x % cell < 6)
                {
                    sheet.Pixels[(((y * size) + x) * RgbaImage.BytesPerPixel) + 3] = 255;
                }
            }
        }

        return BitmapFont.FromSheet(sheet);
    }
}
