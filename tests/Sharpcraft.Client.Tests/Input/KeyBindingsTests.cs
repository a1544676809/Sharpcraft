// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Input;
using Sharpcraft.Framework.Platform;
using Xunit;

namespace Sharpcraft.Client.Tests.Input;

/// <summary>
/// The default key table and the three queries over it (docs/modules/input.md 4.1, 5.1). Every expectation is the
/// reference's own binding, because a client that shows the player list on another key has changed observable
/// behaviour however good the code behind it is.
/// </summary>
public sealed class KeyBindingsTests
{
    [Theory]
    [InlineData(InputAction.PlayerList, InputKey.Tab)]
    [InlineData(InputAction.OpenChat, InputKey.T)]
    [InlineData(InputAction.SendMessage, InputKey.Enter)]
    [InlineData(InputAction.CloseScreen, InputKey.Escape)]
    [InlineData(InputAction.ReleaseCursor, InputKey.Escape)]
    [InlineData(InputAction.MoveForward, InputKey.W)]
    [InlineData(InputAction.MoveBackward, InputKey.S)]
    [InlineData(InputAction.MoveLeft, InputKey.A)]
    [InlineData(InputAction.MoveRight, InputKey.D)]
    [InlineData(InputAction.Jump, InputKey.Space)]
    [InlineData(InputAction.Sneak, InputKey.LeftShift)]
    [InlineData(InputAction.Sprint, InputKey.LeftControl)]
    public void DefaultBindingsMatchTheReference(InputAction action, InputKey expected) =>
        Assert.Equal(expected, KeyBindings.Default[action]);

    [Fact]
    public void QueriesSeeTheBoundKey()
    {
        var input = new WindowInputState();
        input.Press(WindowInputState.Translate(InputKey.Tab));

        Assert.True(KeyBindings.Default.IsDown(input, InputAction.PlayerList));
        Assert.True(KeyBindings.Default.JustPressed(input, InputAction.PlayerList));

        // An action on another key must not react.
        Assert.False(KeyBindings.Default.IsDown(input, InputAction.OpenChat));
    }

    [Fact]
    public void AnUnboundActionAnswersFalse()
    {
        var input = new WindowInputState();
        var bindings = KeyBindings.Default;

        Assert.Null(bindings[(InputAction)9999]);
        Assert.False(bindings.IsDown(input, (InputAction)9999));
        Assert.False(bindings.JustPressed(input, (InputAction)9999));
        Assert.False(bindings.JustReleased(input, (InputAction)9999));
    }

    [Fact]
    public void TheListShowsWhileHeldAndHidesOnceReleased()
    {
        var input = new WindowInputState();
        var bindings = KeyBindings.Default;

        input.Press(WindowInputState.Translate(InputKey.Tab));
        Assert.True(bindings.IsDown(input, InputAction.PlayerList));

        // Still held on the next frame: the list stays up.
        input.BeginFrame();
        Assert.True(bindings.IsDown(input, InputAction.PlayerList));

        input.Release(WindowInputState.Translate(InputKey.Tab));
        Assert.False(bindings.IsDown(input, InputAction.PlayerList));
    }

    [Fact]
    public void AReboundTableKeepsTheOtherActions()
    {
        var rebound = KeyBindings.Default.With(InputAction.PlayerList, InputKey.T);

        Assert.Equal(InputKey.T, rebound[InputAction.PlayerList]);
        Assert.Equal(InputKey.T, rebound[InputAction.OpenChat]);
        Assert.Equal(InputKey.W, rebound[InputAction.MoveForward]);
        Assert.Equal(InputKey.Tab, KeyBindings.Default[InputAction.PlayerList]);
    }
}
