// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Framework.Platform;
using Xunit;

namespace Sharpcraft.Framework.Tests;

/// <summary>
/// Key edges and typed characters (docs/modules/input.md 5.1, 5.2). The distinction between held and just pressed is
/// what lets one interface show while a key is held and another open on the press.
/// </summary>
public sealed class InputStateTests
{
    [Fact]
    public void AHeldKeyIsPressedOnlyOnTheFrameItWentDown()
    {
        var input = new WindowInputState();
        input.Press(WindowInputState.Translate(InputKey.Tab));

        Assert.True(input.IsKeyDown(InputKey.Tab));
        Assert.True(input.JustPressed(InputKey.Tab));

        input.BeginFrame();

        // Still held, but no longer newly pressed: a held key must not retrigger every frame.
        Assert.True(input.IsKeyDown(InputKey.Tab));
        Assert.False(input.JustPressed(InputKey.Tab));
    }

    [Fact]
    public void AReleaseIsReportedOnTheFrameItHappened()
    {
        var input = new WindowInputState();
        input.Press(WindowInputState.Translate(InputKey.T));
        input.BeginFrame();
        input.Release(WindowInputState.Translate(InputKey.T));

        Assert.False(input.IsKeyDown(InputKey.T));
        Assert.True(input.JustReleased(InputKey.T));

        input.BeginFrame();
        Assert.False(input.JustReleased(InputKey.T));
    }

    [Fact]
    public void TypedCharactersAccumulateAndAreTakenOnce()
    {
        var input = new WindowInputState();
        input.AppendText('h');
        input.AppendText('i');

        Assert.Equal(2, input.PendingTextLength);
        Assert.Equal("hi", input.TakeText());
        Assert.Equal(0, input.PendingTextLength);
        Assert.Equal(string.Empty, input.TakeText());
    }

    [Fact]
    public void LosingFocusClearsHeldKeysAndPendingText()
    {
        var input = new WindowInputState();
        input.Press(WindowInputState.Translate(InputKey.W));
        input.AppendText('x');

        input.HasFocus = false;
        input.BeginFrame();

        // No phantom movement when the window comes back, and no stale text arriving in the chat prompt.
        Assert.False(input.IsKeyDown(InputKey.W));
        Assert.Equal(0, input.PendingTextLength);
    }

    [Fact]
    public void TheWheelAccumulatesUntilItIsTaken()
    {
        var state = new Sharpcraft.Framework.Platform.WindowInputState();

        state.AddScroll(1);
        state.AddScroll(1);

        // Two events between frames add up instead of the second replacing the first.
        Assert.Equal(2d, state.TakeScroll());
        Assert.Equal(0d, state.TakeScroll());
    }
}
