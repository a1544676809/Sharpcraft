// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
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

    private WindowHost(IWindow window) => _window = window;

    /// <summary>Window width in pixels.</summary>
    public int Width => _window.Size.X;

    /// <summary>Window height in pixels.</summary>
    public int Height => _window.Size.Y;

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

    /// <summary>Processes pending window events; returns false once the user closed the window.</summary>
    public bool Pump()
    {
        ObjectDisposedException.ThrowIf(!_initialised, this);
        _window.DoEvents();
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
