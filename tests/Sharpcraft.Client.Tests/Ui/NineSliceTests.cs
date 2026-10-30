// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Ui;
using Sharpcraft.Common.Network;
using Xunit;

namespace Sharpcraft.Client.Tests.Ui;

/// <summary>
/// The nine slice the tooltip frame needs (docs/analysis/gui/tooltip-background.md). Corners must keep their pixels:
/// that is what gives the frame its shape, and a stretched whole sprite cannot.
/// </summary>
public sealed class NineSliceTests
{
    [Fact]
    public void TheMetaBlockIsRead()
    {
        const string json = """
        { "gui": { "scaling": { "type": "nine_slice", "width": 100, "height": 100, "border": 10, "stretch_inner": true } } }
        """;

        var scaling = GuiSpriteScaling.TryParse(json);

        Assert.NotNull(scaling);
        Assert.Equal("nine_slice", scaling!.Type);
        Assert.Equal(10, scaling.Border);
        Assert.True(scaling.StretchInner);
    }

    [Fact]
    public void ASpriteWithoutScalingYieldsNothing()
    {
        Assert.Null(GuiSpriteScaling.TryParse("""{ "animation": { } }"""));
        Assert.Null(GuiSpriteScaling.TryParse(""));
    }

    [Fact]
    public void NineCellsAreProducedAndTheCornersKeepTheirSize()
    {
        var cells = NineSlice.Cells(0f, 0f, 100f, 100f, 100f, 100f, border: 10);

        Assert.Equal(9, cells.Count);

        // Corner cells are ten by ten at the corners, with the corner of the sprite behind them.
        Assert.Contains(cells, c => c is { X: 0f, Y: 0f, Width: 10f, Height: 10f, U0: 0f, V0: 0f, U1: 10f, V1: 10f });
        Assert.Contains(cells, c => c is { X: 90f, Y: 90f, Width: 10f, Height: 10f, U0: 90f, V0: 90f, U1: 100f, V1: 100f });

        // The middle stretches both ways, so its destination is larger than its source.
        var middle = cells[4];
        Assert.Equal(80f, middle.Width);
        Assert.Equal(80f, middle.Height);
        Assert.Equal(10f, middle.U0);
        Assert.Equal(90f, middle.U1);
    }

    [Fact]
    public void ATinyDestinationShrinksTheCornersInsteadOfGoingNegative()
    {
        var cells = NineSlice.Cells(0f, 0f, 12f, 12f, 100f, 100f, border: 10);

        Assert.All(cells, cell => Assert.True(cell.Width > 0f && cell.Height > 0f));
        Assert.All(cells, cell => Assert.True(cell.X >= 0f && cell.Y >= 0f));
        Assert.All(cells, cell => Assert.True(cell.X + cell.Width <= 12.001f));
    }

    [Fact]
    public void AZeroSizedDestinationDrawsNothing()
    {
        Assert.Empty(NineSlice.Cells(0f, 0f, 0f, 10f, 100f, 100f, 10));
    }

    [Fact]
    public void ATooltipWithoutSpritesFallsBackToARectangle()
    {
        // No pack means no sprites; the tip still has to be readable, so the rectangle path stays.
        var geometry = new Sharpcraft.Framework.Rendering.UiGeometry();
        Tooltip.Draw(geometry, Font(), [new TextRun("tip", 0xFFFFFFFFu, false)], 40f, 40f, 480f, 270f);

        Assert.True(geometry.QuadCount > 0);
        Assert.All(geometry.Ranges.ToArray(), range =>
            Assert.NotEqual(Sharpcraft.Framework.Rendering.UiMaterial.Sprite, range.Material));
    }

    [Fact]
    public void ATooltipWithSpritesUsesTheSpriteMaterialAndTheReferenceGeometry()
    {
        var background = new Sharpcraft.Framework.Rendering.TextureData(100, 100, Background());
        var frame = new Sharpcraft.Framework.Rendering.TextureData(100, 100, Background());
        var sprites = new GuiSpriteAtlas(background, frame, 10);

        var geometry = new Sharpcraft.Framework.Rendering.UiGeometry();
        Tooltip.Draw(geometry, Font(), [new TextRun("tip", 0xFFFFFFFFu, false)], 100f, 200f, 480f, 270f, sprites);

        var ranges = geometry.Ranges.ToArray();
        Assert.Contains(ranges, range => range.Material == Sharpcraft.Framework.Rendering.UiMaterial.Sprite);

        // Both sprites are drawn, so the ranges carry the two indices.
        var indices = ranges
            .Where(range => range.Material == Sharpcraft.Framework.Rendering.UiMaterial.Sprite)
            .Select(range => range.SpriteIndex)
            .Distinct()
            .OrderBy(index => index)
            .ToArray();

        Assert.Equal([GuiSpriteAtlas.BackgroundIndex, GuiSpriteAtlas.FrameIndex], indices);
    }

    [Fact]
    public void TheSpriteBoxReachesTwelvePixelsBeyondTheText()
    {
        // Nine slice margin 9 plus the three pixel padding: the text area grows by twelve on every side.
        var background = new Sharpcraft.Framework.Rendering.TextureData(100, 100, Background());
        var frame = new Sharpcraft.Framework.Rendering.TextureData(100, 100, Background());
        var sprites = new GuiSpriteAtlas(background, frame, 10);

        var geometry = new Sharpcraft.Framework.Rendering.UiGeometry();
        Tooltip.Draw(geometry, Font(), [new TextRun("tip", 0xFFFFFFFFu, false)], 100f, 200f, 480f, 270f, sprites);

        // Measure the quads themselves: a range's bounds come from the scissor, which a bare test never sets.
        var left = float.MaxValue;
        var right = float.MinValue;
        var top = float.MaxValue;
        var bottom = float.MinValue;

        for (var index = 0; index + 5 < geometry.Vertices.Length; index += 20)
        {
            left = MathF.Min(left, geometry.Vertices[index]);
            right = MathF.Max(right, geometry.Vertices[index + 10]);
            top = MathF.Min(top, geometry.Vertices[index + 1]);
            bottom = MathF.Max(bottom, geometry.Vertices[index + 16]);
        }

        var textWidth = Font().MeasureWidth("tip");
        Assert.Equal(textWidth + 24f, right - left, 1f);
        Assert.Equal(Tooltip.LineHeight + 24f, bottom - top, 1f);
    }

    private static byte[] Background()
    {
        var pixels = new byte[100 * 100 * 4];

        for (var index = 0; index < pixels.Length; index += 4)
        {
            pixels[index] = 0x80;
            pixels[index + 3] = 0xFF;
        }

        return pixels;
    }

    private static Sharpcraft.Client.Ui.BitmapFont Font() => ChatHitTestFonts.Create();
}
