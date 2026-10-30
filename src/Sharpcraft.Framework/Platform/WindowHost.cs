// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Silk.NET.GLFW;
using Silk.NET.Maths;
using Silk.NET.Windowing;

namespace Sharpcraft.Framework.Platform;

/// <summary>
/// Creates a real OS window and exposes the native handle WGPU needs to build a surface
/// (docs/decisions/0004-window-layer.md).
/// </summary>
/// <remarks>
/// Platform-specific handle extraction lives here and nowhere else; the renderer only consumes a
/// <see cref="NativeSurfaceSource"/>. Silk.NET's GLFW backend covers Windows, X11/Wayland and
/// macOS, so the branching below is about handle shape, not about different window systems.
/// </remarks>
public sealed class WindowHost : IDisposable
{
    private readonly IWindow _window;
    private bool _initialised;
    private Glfw? _glfw;
    private unsafe WindowHandle* _handle;

    private WindowHost(IWindow window) => _window = window;

    /// <summary>Window width in pixels.</summary>
    public int Width => _window.Size.X;

    /// <summary>Window height in pixels.</summary>
    public int Height => _window.Size.Y;

    /// <summary>True once the user asked the window to close; the frame loop must stop presenting.</summary>
    public bool IsClosing => _window.IsClosing;

    /// <summary>Framebuffer width in physical pixels (differs from the logical size under DPI scaling).</summary>
    public int FramebufferWidth => _window.FramebufferSize.X;

    /// <summary>Framebuffer height in physical pixels.</summary>
    public int FramebufferHeight => _window.FramebufferSize.Y;

    /// <summary>Window title.</summary>
    public string Title
    {
        get => _window.Title;
        set => _window.Title = value;
    }

    /// <summary>Creates and initialises a window.</summary>
    public static WindowHost Create(string title, int width, int height)
    {
        var options = WindowOptions.Default with
        {
            Title = title,
            Size = new Vector2D<int>(width, height),
            VSync = false,
            API = GraphicsAPI.None,
            ShouldSwapAutomatically = false,
            IsVisible = true
        };

        var host = new WindowHost(Window.Create(options));
        host._window.Initialize();
        host._initialised = true;
        host.AttachInput();
        return host;
    }

    /// <summary>Native handles for the WGPU surface.</summary>
    public NativeSurfaceSource GetSurfaceSource()
    {
        ObjectDisposedException.ThrowIf(!_initialised, this);

        var native = _window.Native ?? throw new PlatformNotSupportedException("the window exposes no native handles");
        if (native.Win32 is { } win32)
        {
            return NativeSurfaceSource.ForWindows(win32.Hwnd, win32.HInstance);
        }

        if (native.X11 is { } x11)
        {
            return NativeSurfaceSource.ForX11(x11.Display, (nint)x11.Window);
        }

        if (native.Wayland is { } wayland)
        {
            return NativeSurfaceSource.ForWayland(wayland.Display, wayland.Surface);
        }

        if (native.Cocoa is { } cocoa)
        {
            return NativeSurfaceSource.ForMetalLayer(cocoa);
        }

        throw new PlatformNotSupportedException("this platform exposes no window handle the renderer knows how to bind");
    }

    /// <summary>Resizes the window in logical pixels; the framebuffer follows on the next frame.</summary>
    public void Resize(int width, int height)
    {
        ObjectDisposedException.ThrowIf(!_initialised, this);
        _window.Size = new Vector2D<int>(width, height);
    }

    private int _scaleOption;
    private int _framebufferSeenWidth;
    private int _framebufferSeenHeight;

    /// <summary>The interface scale and the virtual canvas it produces (docs/modules/ui.md 9.1).</summary>
    public GuiCanvas Gui { get; private set; } = new(1, 1, 1);

    /// <summary>Input state collected from the platform, refreshed by <see cref="Pump"/>.</summary>
    public WindowInputState Input { get; } = new();

    /// <summary>
    /// Hooks the GLFW callbacks of the backend the window already uses. Silk.NET.Windowing exposes no key
    /// or mouse surface of its own, and adding the separate input package would pull in another native
    /// library; the windowing backend's own callbacks keep the dependency set unchanged
    /// (docs/modules/input.md 3).
    /// </summary>
    private unsafe void AttachInput()
    {
        if (_window.Native?.Glfw is not { } handleValue)
        {
            return;
        }

        var handle = (WindowHandle*)handleValue;
        var glfw = Glfw.GetApi();
        _glfw = glfw;
        _handle = handle;

        // Zero means "as large as fits", which is the reference's default. A fixed value is honoured for testing.
        _scaleOption = int.TryParse(Environment.GetEnvironmentVariable("MC_GUI_SCALE"), out var configured)
            ? configured
            : GuiScale.Auto;

        RefreshGuiScale();

        var trace = Environment.GetEnvironmentVariable("MC_INPUT_TRACE") == "1";

        glfw.SetKeyCallback(handle, (window, key, scancode, action, mods) =>
        {
            _ = window;

            if (trace)
            {
                // Verification hook: remote desktop tools and on-screen keyboards inject keys differently, so when
                // input looks dead the first question is whether anything arrives at all.
                Console.WriteLine($"input: key {key} scancode {scancode} action {action} mods {mods}");
            }

            if (action == InputAction.Press)
            {
                Input.Press((int)key);
            }
            else if (action == InputAction.Release)
            {
                Input.Release((int)key);
            }
        });

        // Characters come from the character callback rather than from key codes, so the keyboard layout and any
        // input method are already applied. Keys give the actions, characters give the text.
        glfw.SetCharCallback(handle, (_, codepoint) =>
        {
            if (trace)
            {
                Console.WriteLine($"input: char {codepoint} '{(codepoint is > 0 and <= 0xFFFF ? ((char)codepoint).ToString() : "?")}'");
            }

            if (codepoint is > 0 and <= 0xFFFF)
            {
                Input.AppendText((char)codepoint);
            }
            else if (codepoint > 0xFFFF)
            {
                var pair = char.ConvertFromUtf32((int)codepoint);
                Input.AppendText(pair[0]);
                Input.AppendText(pair[1]);
            }
        });

        glfw.SetCursorPosCallback(handle, (_, x, y) => Input.MoveCursor(new Vector2((float)x, (float)y)));

        glfw.SetMouseButtonCallback(handle, (_, button, action, _) =>
        {
            if (action == InputAction.Press)
            {
                Input.PressButton((int)button);
            }
        });

        glfw.SetWindowFocusCallback(handle, (_, focused) =>
        {
            Input.HasFocus = focused;

            if (!focused)
            {
                Input.Reset();

                // Losing focus must also give the cursor back. Keeping it disabled would confine the pointer to a
                // window the player is not using, which makes every other application unusable.
                SetMouseCaptured(false);
            }
        });
    }

    /// <summary>Reads whether the window currently has keyboard focus.</summary>
    /// <remarks>
    /// Queried from the backend rather than tracked from the focus callback alone: the callback may not have fired
    /// when the first frame runs, and the state field defaults to true, which would make the very first frame
    /// capture the pointer into an unfocused window.
    /// </remarks>
    public unsafe bool QueryFocus() =>
        _glfw is not null && _handle is not null &&
        _glfw.GetWindowAttrib(_handle, WindowAttributeGetter.Focused);

    /// <summary>Turns relative mouse capture on or off.</summary>
    /// <param name="captured">True to capture the cursor for look control.</param>
    public unsafe void SetMouseCaptured(bool captured)
    {
        ObjectDisposedException.ThrowIf(!_initialised, this);
        if (_glfw is null || _handle is null)
        {
            return;
        }

        // Refuse to take the pointer while the window is unfocused: the player's other applications must keep
        // working, and a captured cursor in a background window cannot even be seen.
        captured &= QueryFocus();

        _glfw.SetInputMode(
            _handle,
            CursorStateAttribute.Cursor,
            captured ? CursorModeValue.CursorDisabled : CursorModeValue.CursorNormal);

        Input.MouseCaptured = captured;
        if (!captured)
        {
            Input.Reset();
        }
    }

    /// <summary>Recomputes the interface scale when the framebuffer size changes.</summary>
    private void RefreshGuiScale()
    {
        var width = FramebufferWidth;
        var height = FramebufferHeight;

        if (width == _framebufferSeenWidth && height == _framebufferSeenHeight && Gui.Width > 1)
        {
            return;
        }

        _framebufferSeenWidth = width;
        _framebufferSeenHeight = height;

        var scale = GuiScale.Calculate(width, height, _scaleOption);
        var (scaledWidth, scaledHeight) = GuiScale.ScaledSize(width, height, scale);
        Gui = new GuiCanvas(scale, scaledWidth, scaledHeight);
        Input.SetCanvas(Gui);
    }

    /// <summary>Processes pending window events; returns false once the user closed the window.</summary>
    public bool Pump()
    {
        ObjectDisposedException.ThrowIf(!_initialised, this);

        // The reference recomputes the interface scale when the framebuffer changes, so the virtual canvas follows a
        // resize instead of stretching.
        RefreshGuiScale();

        // Snapshot before processing, so this frame's edges describe what changed since the caller last looked.
        Input.BeginFrame();
        _window.DoEvents();
        Input.HasFocus = QueryFocus();
        return !_window.IsClosing;
    }

    /// <summary>Requests the window to close.</summary>
    public void Close()
    {
        if (_initialised)
        {
            _window.Close();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_initialised)
        {
            _initialised = false;
            _window.Dispose();
        }
    }
}

/// <summary>Which platform handle a <see cref="NativeSurfaceSource"/> carries.</summary>
public enum NativeSurfaceKind
{
    /// <summary>Win32 <c>HWND</c> plus <c>HINSTANCE</c>.</summary>
    Windows,

    /// <summary>X11 display plus window id.</summary>
    X11,

    /// <summary>Wayland display plus surface.</summary>
    Wayland,

    /// <summary>macOS <c>CAMetalLayer</c>.</summary>
    MetalLayer
}

/// <summary>Platform-neutral description of the native handle a surface is created from.</summary>
public readonly record struct NativeSurfaceSource(NativeSurfaceKind Tag, nint Handle0, nint Handle1)
{

    /// <summary>Win32 handle pair.</summary>
    public static NativeSurfaceSource ForWindows(nint hwnd, nint hinstance) =>
        new(NativeSurfaceKind.Windows, hwnd, hinstance);

    /// <summary>X11 handle pair.</summary>
    public static NativeSurfaceSource ForX11(nint display, nint window) =>
        new(NativeSurfaceKind.X11, display, window);

    /// <summary>Wayland handle pair.</summary>
    public static NativeSurfaceSource ForWayland(nint display, nint surface) =>
        new(NativeSurfaceKind.Wayland, display, surface);

    /// <summary>macOS Metal layer handle.</summary>
    public static NativeSurfaceSource ForMetalLayer(nint layer) =>
        new(NativeSurfaceKind.MetalLayer, layer, 0);
}
