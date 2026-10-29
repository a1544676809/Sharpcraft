// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Framework.Platform;
using Xunit;

namespace Sharpcraft.Framework.Tests.Repro;

/// <summary>
/// Cursor capture behaviour (docs/modules/input.md). Three defects were reported together: the pointer was
/// grabbed even when the window had no focus, releasing with escape did not stop the camera from following the
/// mouse, and clicking could not capture it again. The first two are described entirely by the input state, so
/// they are pinned here rather than left to a manual check.
/// </summary>
public sealed class Repro_008_CursorCaptureTests
{
    [Fact]
    public void MovementIsIgnoredWhileTheCursorIsReleased()
    {
        var input = new WindowInputState();
        input.MoveCursor(new Vector2(100f, 100f));

        // Not captured: moving across the desktop must not turn the camera.
        input.MoveCursor(new Vector2(160f, 130f));

        Assert.Equal(Vector2.Zero, input.TakeMouseDelta());
    }

    [Fact]
    public void MovementIsAccumulatedWhileCaptured()
    {
        var input = new WindowInputState { MouseCaptured = true };
        input.MoveCursor(new Vector2(100f, 100f));

        input.MoveCursor(new Vector2(160f, 130f));

        Assert.Equal(new Vector2(60f, 30f), input.TakeMouseDelta());
    }

    [Fact]
    public void ReleasingAndCapturingAgainDoesNotProduceAJump()
    {
        var input = new WindowInputState { MouseCaptured = true };
        input.MoveCursor(new Vector2(10f, 10f));
        input.TakeMouseDelta();

        // Released, the pointer wanders far away.
        input.MouseCaptured = false;
        input.MoveCursor(new Vector2(900f, 700f));
        input.TakeMouseDelta();

        // Captured again: the first movement after this must be relative to where the pointer is now, not a jump
        // from wherever it was before.
        input.MouseCaptured = true;
        input.MoveCursor(new Vector2(905f, 703f));

        Assert.Equal(new Vector2(5f, 3f), input.TakeMouseDelta());
    }

    [Fact]
    public void AClickIsReportedOnce()
    {
        var input = new WindowInputState();

        input.PressButton(0);

        Assert.True(input.TakeClick());
        Assert.False(input.TakeClick());
    }

    [Fact]
    public void LosingFocusClearsHeldStateIncludingAPendingClick()
    {
        var input = new WindowInputState();
        input.Press(65);
        input.PressButton(0);

        input.Reset();

        Assert.Empty(input.PressedKeys);
        Assert.False(input.TakeClick());
    }
}
