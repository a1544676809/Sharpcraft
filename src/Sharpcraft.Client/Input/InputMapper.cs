// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Framework.Platform;

namespace Sharpcraft.Client.Input;

/// <summary>Actions the client understands, decoupled from physical keys.</summary>
public enum InputAction
{
    /// <summary>Move forward (W).</summary>
    MoveForward,

    /// <summary>Move backward (S).</summary>
    MoveBackward,

    /// <summary>Move left (A).</summary>
    MoveLeft,

    /// <summary>Move right (D).</summary>
    MoveRight,

    /// <summary>Jump (space).</summary>
    Jump,

    /// <summary>Sprint (ctrl).</summary>
    Sprint,

    /// <summary>Sneak (shift).</summary>
    Sneak,

    /// <summary>Release the cursor (escape).</summary>
    ReleaseCursor,

    /// <summary>Show the player list while held (tab).</summary>
    PlayerList,

    /// <summary>Open the chat prompt (t).</summary>
    OpenChat,

    /// <summary>Submit the chat prompt (enter).</summary>
    SendMessage,

    /// <summary>Close the current screen (escape).</summary>
    CloseScreen
}

/// <summary>
/// Maps actions to physical keys. Immutable so a frame can read it without locking, and so a future
/// binding UI can swap the whole table atomically (docs/modules/input.md 4).
/// </summary>
public sealed class KeyBindings
{
    private readonly Dictionary<InputAction, InputKey> _keys;

    private KeyBindings(Dictionary<InputAction, InputKey> keys) => _keys = keys;

    /// <summary>Vanilla-compatible default layout.</summary>
    public static KeyBindings Default { get; } = new(new Dictionary<InputAction, InputKey>
    {
        [InputAction.MoveForward] = InputKey.W,
        [InputAction.MoveBackward] = InputKey.S,
        [InputAction.MoveLeft] = InputKey.A,
        [InputAction.MoveRight] = InputKey.D,
        [InputAction.Jump] = InputKey.Space,
        [InputAction.Sprint] = InputKey.LeftControl,
        [InputAction.Sneak] = InputKey.LeftShift,
        [InputAction.ReleaseCursor] = InputKey.Escape,
        [InputAction.PlayerList] = InputKey.Tab,
        [InputAction.OpenChat] = InputKey.T,
        [InputAction.SendMessage] = InputKey.Enter,
        [InputAction.CloseScreen] = InputKey.Escape
    });

    /// <summary>Key bound to an action, or null when it is unbound.</summary>
    /// <param name="action">Action to look up.</param>
    /// <remarks>
    /// TryGetValue rather than GetValueOrDefault: the latter returns the default of a value type, which here is the
    /// first key in the enum, so an unbound action would silently behave as though that key were held.
    /// </remarks>
    public InputKey? this[InputAction action] => _keys.TryGetValue(action, out var key) ? key : null;

    /// <summary>True while the key bound to the action is held.</summary>
    /// <param name="input">Platform input state.</param>
    /// <param name="action">Action to test.</param>
    public bool IsDown(WindowInputState input, InputAction action)
    {
        ArgumentNullException.ThrowIfNull(input);
        return this[action] is { } key && input.IsKeyDown(key);
    }

    /// <summary>True when the key bound to the action went down since the previous frame.</summary>
    /// <param name="input">Platform input state.</param>
    /// <param name="action">Action to test.</param>
    public bool JustPressed(WindowInputState input, InputAction action)
    {
        ArgumentNullException.ThrowIfNull(input);
        return this[action] is { } key && input.JustPressed(key);
    }

    /// <summary>True when the key bound to the action came up since the previous frame.</summary>
    /// <param name="input">Platform input state.</param>
    /// <param name="action">Action to test.</param>
    public bool JustReleased(WindowInputState input, InputAction action)
    {
        ArgumentNullException.ThrowIfNull(input);
        return this[action] is { } key && input.JustReleased(key);
    }

    /// <summary>Creates a copy with one action rebound.</summary>
    /// <param name="action">Action to rebind.</param>
    /// <param name="key">New key.</param>
    public KeyBindings With(InputAction action, InputKey key)
    {
        var copy = new Dictionary<InputAction, InputKey>(_keys) { [action] = key };
        return new KeyBindings(copy);
    }
}

/// <summary>
/// One frame of intent, produced from the platform input state. A readonly struct so the frame loop can
/// create it every tick without allocating (docs/modules/input.md 4).
/// </summary>
/// <param name="Forward">−1 backward … +1 forward.</param>
/// <param name="Strafe">−1 left … +1 right.</param>
/// <param name="Jump">True while jump is held.</param>
/// <param name="Sprint">True while sprint is held.</param>
/// <param name="Sneak">True while sneak is held.</param>
/// <param name="ReleaseCursor">True when escape was pressed this frame.</param>
/// <param name="LookDelta">Mouse movement in pixels for this frame.</param>
public readonly record struct FrameInput(
    float Forward,
    float Strafe,
    bool Jump,
    bool Sprint,
    bool Sneak,
    bool ReleaseCursor,
    Vector2 LookDelta)
{
    /// <summary>No input at all.</summary>
    public static FrameInput None => default;
}

/// <summary>
/// Turns platform input into a frame snapshot. Pure logic: it reads key state and mouse movement, applies
/// the bindings and the look sensitivity, and allocates nothing per frame.
/// </summary>
public sealed class InputMapper
{
    private readonly KeyBindings _bindings;

    /// <summary>Creates a mapper.</summary>
    /// <param name="bindings">Action to key table.</param>
    /// <param name="lookSensitivity">Degrees of rotation per pixel of mouse movement.</param>
    public InputMapper(KeyBindings? bindings = null, float lookSensitivity = 0.15f)
    {
        _bindings = bindings ?? KeyBindings.Default;
        LookSensitivity = lookSensitivity;
    }

    /// <summary>Degrees of rotation per mouse pixel.</summary>
    public float LookSensitivity { get; }

    /// <summary>Builds the frame snapshot from the current platform state.</summary>
    /// <param name="input">Platform input state.</param>
    public FrameInput Map(WindowInputState input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var forward = 0f;
        var strafe = 0f;

        if (IsHeld(input, InputAction.MoveForward))
        {
            forward += 1f;
        }

        if (IsHeld(input, InputAction.MoveBackward))
        {
            forward -= 1f;
        }

        if (IsHeld(input, InputAction.MoveRight))
        {
            strafe += 1f;
        }

        if (IsHeld(input, InputAction.MoveLeft))
        {
            strafe -= 1f;
        }

        return new FrameInput(
            forward,
            strafe,
            IsHeld(input, InputAction.Jump),
            IsHeld(input, InputAction.Sprint),
            IsHeld(input, InputAction.Sneak),
            IsHeld(input, InputAction.ReleaseCursor),
            input.TakeMouseDelta());
    }

    private bool IsHeld(WindowInputState input, InputAction action) =>
        _bindings[action] is { } key && input.IsKeyDown(key);
}
