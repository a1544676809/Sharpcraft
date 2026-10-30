// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Ui;
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
}
