// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Ui;
using Sharpcraft.Common.Imaging;

namespace Sharpcraft.Client.Tests.Ui;

/// <summary>A font whose every glyph is a fixed-width block, so widths are predictable in tests.</summary>
internal static class ChatHitTestFonts
{
    public static BitmapFont Create()
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
