// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Framework.Platform;
using Xunit;

namespace Sharpcraft.Framework.Tests;

/// <summary>
/// Pointer positions in interface pixels (docs/modules/ui.md 9.1). The layout is written in interface pixels, so a
/// click has to be reported in the same units; otherwise everything lands in the wrong place as soon as the scale
/// is not one.
/// </summary>
public sealed class CursorCanvasTests
{
    [Fact]
    public void ThePointerIsReportedInInterfacePixels()
    {
        var input = new WindowInputState();
        input.SetCanvas(new GuiCanvas(Scale: 2, Width: 480, Height: 270));

        input.MoveCursor(new Vector2(100f, 61f));

        // Two device pixels per interface pixel, rounded down so a position never leaves the canvas.
        Assert.Equal(new Vector2(50f, 30f), input.CursorInCanvas);
    }

    [Fact]
    public void AScaleOfOneLeavesThePositionAlone()
    {
        var input = new WindowInputState();
        input.SetCanvas(new GuiCanvas(Scale: 1, Width: 960, Height: 540));

        input.MoveCursor(new Vector2(37.5f, 12.25f));

        Assert.Equal(new Vector2(37f, 12f), input.CursorInCanvas);
    }

    [Fact]
    public void ChangingTheScaleRereadsTheLastPosition()
    {
        var input = new WindowInputState();
        input.SetCanvas(new GuiCanvas(Scale: 1, Width: 960, Height: 540));
        input.MoveCursor(new Vector2(400f, 200f));
        Assert.Equal(new Vector2(400f, 200f), input.CursorInCanvas);

        // The window was resized and the interface now scales by four.
        input.SetCanvas(new GuiCanvas(Scale: 4, Width: 240, Height: 135));

        Assert.Equal(new Vector2(100f, 50f), input.CursorInCanvas);
    }

    [Fact]
    public void LookDeltasStayInDevicePixels()
    {
        var input = new WindowInputState();
        input.SetCanvas(new GuiCanvas(Scale: 2, Width: 480, Height: 270));
        input.MouseCaptured = true;

        input.MoveCursor(new Vector2(10f, 10f));
        input.MoveCursor(new Vector2(14f, 18f));

        // Sensitivity is tuned against device pixels, so the delta must not be divided by the scale.
        Assert.Equal(new Vector2(4f, 8f), input.TakeMouseDelta());
    }
}
