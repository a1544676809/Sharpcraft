// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Silk.NET.GLFW;

namespace Sharpcraft.Framework.Platform;

/// <summary>
/// Input snapshot the window layer produces (docs/modules/input.md 4). The platform layer only reports
/// raw device state; mapping keys to actions happens in the client.
/// </summary>
public sealed class WindowInputState
{
    private readonly HashSet<int> _pressedKeys = [];

    /// <summary>Accumulated relative mouse movement since the last <see cref="TakeMouseDelta"/>.</summary>
    private Vector2 _mouseDelta;

    /// <summary>Last reported absolute cursor position, used to derive the delta.</summary>
    private Vector2? _lastCursor;

    /// <summary>True while the window has focus.</summary>
    public bool HasFocus { get; internal set; } = true;

    /// <summary>True while the cursor is captured for relative look control.</summary>
    public bool MouseCaptured { get; internal set; }

    /// <summary>Keys currently held, as platform scancodes/key codes.</summary>
    public IReadOnlyCollection<int> PressedKeys => _pressedKeys;

    /// <summary>True when the key is held.</summary>
    /// <param name="key">Platform-neutral key.</param>
    public bool IsKeyDown(InputKey key) => _pressedKeys.Contains(Translate(key));

    /// <summary>Records a raw key press from the backend.</summary>
    /// <param name="key">Backend key code.</param>
    internal void Press(int key) => _pressedKeys.Add(key);

    /// <summary>Records a raw key release from the backend.</summary>
    /// <param name="key">Backend key code.</param>
    internal void Release(int key) => _pressedKeys.Remove(key);

    /// <summary>
    /// Maps a platform-neutral key to the backend code. Only the keys the client uses are mapped; anything
    /// else stays invisible to game code.
    /// </summary>
    /// <param name="key">Platform-neutral key.</param>
    public static int Translate(InputKey key) => key switch
    {
        InputKey.W => (int)Keys.W,
        InputKey.A => (int)Keys.A,
        InputKey.S => (int)Keys.S,
        InputKey.D => (int)Keys.D,
        InputKey.Space => (int)Keys.Space,
        InputKey.LeftShift => (int)Keys.ShiftLeft,
        InputKey.LeftControl => (int)Keys.ControlLeft,
        InputKey.Escape => (int)Keys.Escape,
        _ => -1
    };

    /// <summary>Clears every held key and the accumulated movement; used on focus loss.</summary>
    internal void Reset()
    {
        _pressedKeys.Clear();
        _mouseDelta = Vector2.Zero;
        _lastCursor = null;
    }

    /// <summary>Records a cursor position and accumulates the relative movement.</summary>
    /// <param name="position">Absolute cursor position in window pixels.</param>
    internal void MoveCursor(Vector2 position)
    {
        if (_lastCursor is { } last)
        {
            _mouseDelta += position - last;
        }

        _lastCursor = position;
    }

    /// <summary>Returns the accumulated relative movement and clears it.</summary>
    public Vector2 TakeMouseDelta()
    {
        var delta = _mouseDelta;
        _mouseDelta = Vector2.Zero;
        return delta;
    }
}
