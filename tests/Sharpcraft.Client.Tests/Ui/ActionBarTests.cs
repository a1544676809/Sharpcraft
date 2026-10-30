// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Ui;
using Sharpcraft.Common.Imaging;
using Sharpcraft.Common.Network;
using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Client.Tests.Ui;

/// <summary>
/// The line a server shows above the hotbar (docs/modules/ui.md 8.4). It is not a chat line: it is centred, has no
/// history and expires on its own, which is what the overlay flag selects.
/// </summary>
public sealed class ActionBarTests
{
    [Theory]
    [InlineData(60, 1f)]
    [InlineData(40, 1f)]
    [InlineData(20, 1f)]
    [InlineData(10, 0.5f)]
    [InlineData(5, 0.25f)]
    [InlineData(0, 0f)]
    public void TheLineIsOpaqueUntilItsLastSecond(int remaining, float expected) =>
        Assert.Equal(expected, ActionBar.AlphaFor(remaining), 3);

    [Fact]
    public void AMessageExpiresOnItsOwn()
    {
        var bar = new ActionBar();
        bar.Set([new TextRun("hello", 0xFFFFFFFFu, false)]);

        Assert.True(bar.IsVisible);

        for (var tick = 0; tick < ActionBar.LifetimeTicks; tick++)
        {
            bar.Tick();
        }

        Assert.False(bar.IsVisible);
        Assert.Equal(0, bar.RemainingTicks);
    }

    [Fact]
    public void SettingAgainRestartsTheLifetime()
    {
        var bar = new ActionBar();
        bar.Set([new TextRun("first", 0xFFFFFFFFu, false)]);
        bar.Tick();

        bar.Set([new TextRun("second", 0xFFFFFFFFu, false)]);

        Assert.Equal(ActionBar.LifetimeTicks, bar.RemainingTicks);
    }

    [Fact]
    public void TheLineIsCentredAboveTheHotbar()
    {
        var bar = new ActionBar();
        bar.Set([new TextRun("centre", 0xFFFFFFFFu, false)]);

        var geometry = new UiGeometry();
        bar.Draw(geometry, Font(), canvasWidth: 480f, canvasHeight: 270f);

        // There is no backdrop, so the extent comes from the glyph quads themselves.
        var left = float.MaxValue;
        var right = float.MinValue;
        var top = float.MaxValue;

        for (var index = 0; index + 5 < geometry.Vertices.Length; index += 20)
        {
            left = MathF.Min(left, geometry.Vertices[index]);
            right = MathF.Max(right, geometry.Vertices[index + 5]);
            top = MathF.Min(top, geometry.Vertices[index + 1]);
        }

        Assert.Equal(240f, (left + right) / 2f, 1f);
        Assert.True(top < 270f - ActionBar.BottomInset, "the line sits above the hotbar");
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
