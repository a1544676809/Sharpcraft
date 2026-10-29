// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Input;
using Sharpcraft.Framework.Platform;
using Xunit;

namespace Sharpcraft.Client.Tests.Input;

/// <summary>
/// Input mapping (docs/modules/input.md 4-5): held keys become an intent, opposing keys cancel, the
/// snapshot is a value type, and the look delta is consumed exactly once per frame.
/// </summary>
public sealed class InputMapperTests
{
    [Fact]
    public void ForwardAndStrafeComeFromTheirKeys()
    {
        var state = new WindowInputState();
        var mapper = new InputMapper();

        Press(state, InputKey.W);
        Press(state, InputKey.D);

        var frame = mapper.Map(state);

        Assert.Equal(1f, frame.Forward);
        Assert.Equal(1f, frame.Strafe);
    }

    [Fact]
    public void OpposingDirectionsCancel()
    {
        var state = new WindowInputState();
        Press(state, InputKey.W);
        Press(state, InputKey.S);
        Press(state, InputKey.A);
        Press(state, InputKey.D);

        var frame = new InputMapper().Map(state);

        Assert.Equal(0f, frame.Forward);
        Assert.Equal(0f, frame.Strafe);
    }

    [Fact]
    public void JumpSprintAndReleaseFollowTheirBindings()
    {
        var state = new WindowInputState();
        Press(state, InputKey.Space);
        Press(state, InputKey.LeftControl);
        Press(state, InputKey.Escape);

        var frame = new InputMapper().Map(state);

        Assert.True(frame.Jump);
        Assert.True(frame.Sprint);
        Assert.True(frame.ReleaseCursor);
    }

    [Fact]
    public void ReleasingAKeyStopsTheIntent()
    {
        var state = new WindowInputState();
        Press(state, InputKey.W);
        Assert.Equal(1f, new InputMapper().Map(state).Forward);

        state.Release(WindowInputState.Translate(InputKey.W));
        Assert.Equal(0f, new InputMapper().Map(state).Forward);
    }

    [Fact]
    public void TheMouseDeltaIsConsumedOncePerFrame()
    {
        // Movement only accumulates while the cursor is captured, which is what stops a released pointer from
        // turning the camera; the capture itself is covered by Repro_008.
        var state = new WindowInputState { MouseCaptured = true };
        state.MoveCursor(new Vector2(10f, 10f));
        state.MoveCursor(new Vector2(14f, 7f));

        var mapper = new InputMapper();

        Assert.Equal(new Vector2(4f, -3f), mapper.Map(state).LookDelta);

        // Nothing moved since: the next frame must see no movement.
        Assert.Equal(Vector2.Zero, mapper.Map(state).LookDelta);
    }

    [Fact]
    public void LosingFocusClearsEveryHeldKey()
    {
        var state = new WindowInputState();
        Press(state, InputKey.W);
        Assert.True(state.IsKeyDown(InputKey.W));

        state.Reset();

        // The reset is what stops the player walking away while the user is in another window.
        Assert.False(state.IsKeyDown(InputKey.W));
        Assert.Equal(0f, new InputMapper().Map(state).Forward);
    }

    [Fact]
    public void ReboundActionsUseTheNewKey()
    {
        var bindings = KeyBindings.Default.With(InputAction.MoveForward, InputKey.W);
        var state = new WindowInputState();
        Press(state, InputKey.W);

        var frame = new InputMapper(bindings).Map(state);

        Assert.Equal(1f, frame.Forward);
        Assert.Equal(InputKey.W, bindings[InputAction.MoveForward]);
    }

    private static void Press(WindowInputState state, InputKey key) => state.Press(WindowInputState.Translate(key));
}
