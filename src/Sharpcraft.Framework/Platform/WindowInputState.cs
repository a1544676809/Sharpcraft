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
    private readonly HashSet<int> _previousKeys = [];
    private readonly System.Text.StringBuilder _text = new();

    /// <summary>Accumulated relative mouse movement since the last <see cref="TakeMouseDelta"/>.</summary>
    private Vector2 _mouseDelta;

    /// <summary>Last reported absolute cursor position, used to derive the delta.</summary>
    private Vector2? _lastCursor;

    /// <summary>Set when the primary button is pressed and cleared when the click is consumed.</summary>
    private bool _clickPending;
    private double _scroll;

    /// <summary>The interface scale and canvas currently in force.</summary>
    public GuiCanvas Canvas { get; private set; } = new(1, 1, 1);

    /// <summary>
    /// The pointer position in interface pixels, which is the coordinate system the interface is laid out in
    /// (docs/modules/ui.md 9.1). Hit testing uses this rather than the device position, otherwise a click lands
    /// somewhere else as soon as the scale is not one.
    /// </summary>
    public Vector2 CursorInCanvas { get; private set; }

    /// <summary>True while the window has focus.</summary>
    public bool HasFocus { get; internal set; } = true;

    /// <summary>True while the cursor is captured for relative look control.</summary>
    public bool MouseCaptured { get; internal set; }

    /// <summary>Keys currently held, as platform scancodes/key codes.</summary>
    public IReadOnlyCollection<int> PressedKeys => _pressedKeys;

    /// <summary>True when the key is held.</summary>
    /// <param name="key">Platform-neutral key.</param>
    public bool IsKeyDown(InputKey key) => _pressedKeys.Contains(Translate(key));



    /// <summary>True when the key went down since the previous frame.</summary>
    /// <param name="key">Platform-neutral key.</param>
    public bool JustPressed(InputKey key)
    {
        var code = Translate(key);
        return _pressedKeys.Contains(code) && !_previousKeys.Contains(code);
    }

    /// <summary>True when the key came up since the previous frame.</summary>
    /// <param name="key">Platform-neutral key.</param>
    public bool JustReleased(InputKey key)
    {
        var code = Translate(key);
        return !_pressedKeys.Contains(code) && _previousKeys.Contains(code);
    }

    /// <summary>Characters typed since the last <see cref="TakeText"/>.</summary>
    public int PendingTextLength => _text.Length;

    /// <summary>Takes the characters typed since the last call and clears them.</summary>
    public string TakeText()
    {
        if (_text.Length == 0)
        {
            return string.Empty;
        }

        var typed = _text.ToString();
        _text.Clear();
        return typed;
    }

    /// <summary>Records a typed character from the backend's character callback.</summary>
    /// <param name="character">Character produced by the platform, which already accounts for layout and IME.</param>
    internal void AppendText(char character) => _text.Append(character);

    /// <summary>Records a wheel movement from the backend.</summary>
    /// <param name="offset">Vertical detents; positive is away from the user.</param>
    internal void AddScroll(double offset) => _scroll += offset;

    /// <summary>Takes the wheel movement since the last call and clears it.</summary>
    /// <remarks>
    /// Wheel events arrive between frames and can be several per frame, so they accumulate and are consumed once;
    /// reading per event would let a fast wheel do nothing.
    /// </remarks>
    public double TakeScroll()
    {
        var offset = _scroll;
        _scroll = 0;
        return offset;
    }

    /// <summary>
    /// Starts a frame by snapshotting the keys, so the edge queries describe what changed since the last frame the
    /// caller actually read. Losing focus clears both sides, otherwise restoring focus would replay presses that
    /// happened while the window was in the background.
    /// </summary>
    internal void BeginFrame()
    {
        _previousKeys.Clear();

        if (!HasFocus)
        {
            _pressedKeys.Clear();
            _text.Clear();
            return;
        }

        foreach (var code in _pressedKeys)
        {
            _previousKeys.Add(code);
        }
    }

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
        InputKey.Tab => (int)Keys.Tab,
        InputKey.T => (int)Keys.T,
        InputKey.Enter => (int)Keys.Enter,
        InputKey.Slash => (int)Keys.Slash,
        InputKey.Backspace => (int)Keys.Backspace,
        InputKey.LeftArrow => (int)Keys.Left,
        InputKey.RightArrow => (int)Keys.Right,
        InputKey.Home => (int)Keys.Home,
        InputKey.End => (int)Keys.End,
        InputKey.Delete => (int)Keys.Delete,
        InputKey.PageUp => (int)Keys.PageUp,
        InputKey.PageDown => (int)Keys.PageDown,
        InputKey.C => (int)Keys.C,
        InputKey.V => (int)Keys.V,
        InputKey.X => (int)Keys.X,
        _ => -1
    };

    /// <summary>Clears every held key and the accumulated movement; used on focus loss.</summary>
    internal void Reset()
    {
        _pressedKeys.Clear();
        _mouseDelta = Vector2.Zero;
        _lastCursor = null;
        _clickPending = false;
    }

    /// <summary>Records a cursor position and accumulates the relative movement.</summary>
    /// <param name="position">Absolute cursor position in window pixels.</param>
    /// <remarks>
    /// Movement is only accumulated while the cursor is captured. Otherwise a released cursor would keep turning
    /// the camera as it moves across the desktop, which is exactly the bug a player sees after pressing escape.
    /// The last position is still tracked while released so the first delta after capturing is not a jump across
    /// the window.
    /// </remarks>
    internal void MoveCursor(Vector2 position)
    {
        if (MouseCaptured && _lastCursor is { } last)
        {
            _mouseDelta += position - last;
        }

        _lastCursor = position;

        var scale = Math.Max(1, Canvas.Scale);
        CursorInCanvas = new Vector2(
            MathF.Floor(position.X / scale),
            MathF.Floor(position.Y / scale));
    }

    /// <summary>Records the interface scale in force, so pointer positions can be reported in its pixels.</summary>
    /// <param name="canvas">Current scale and canvas.</param>
    internal void SetCanvas(GuiCanvas canvas)
    {
        Canvas = canvas;

        if (_lastCursor is { } last)
        {
            var scale = Math.Max(1, canvas.Scale);
            CursorInCanvas = new Vector2(MathF.Floor(last.X / scale), MathF.Floor(last.Y / scale));
        }
    }

    /// <summary>Records a mouse button press from the backend.</summary>
    /// <param name="button">Backend button index; zero is the primary button.</param>
    internal void PressButton(int button)
    {
        if (button == 0)
        {
            _clickPending = true;
        }
    }

    /// <summary>Takes the pending primary click, if any.</summary>
    public bool TakeClick()
    {
        var clicked = _clickPending;
        _clickPending = false;
        return clicked;
    }

    /// <summary>Returns the accumulated relative movement and clears it.</summary>
    public Vector2 TakeMouseDelta()
    {
        var delta = _mouseDelta;
        _mouseDelta = Vector2.Zero;
        return delta;
    }
}
