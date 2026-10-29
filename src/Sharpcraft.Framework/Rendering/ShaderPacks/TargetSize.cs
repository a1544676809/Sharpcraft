// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Framework.Rendering.ShaderPacks;

/// <summary>
/// How a render target is sized. The base is the window framebuffer in physical pixels, because the
/// swapchain is configured with those (ADR-0004 revision note), never the logical size
/// (docs/specs/render/shader-packs.md 2).
/// </summary>
public abstract record TargetSize
{
    private TargetSize()
    {
    }

    /// <summary>Relative to the framebuffer; clamped to at least one pixel per axis.</summary>
    /// <param name="Scale">Fraction of the framebuffer, for example 0.5 for half resolution.</param>
    public sealed record Relative(double Scale) : TargetSize;

    /// <summary>Fixed pixel size, for targets unrelated to the screen such as a shadow map.</summary>
    /// <param name="Width">Width in pixels.</param>
    /// <param name="Height">Height in pixels.</param>
    public sealed record Absolute(int Width, int Height) : TargetSize;

    /// <summary>Resolves the size for a framebuffer.</summary>
    /// <param name="framebufferWidth">Framebuffer width in pixels.</param>
    /// <param name="framebufferHeight">Framebuffer height in pixels.</param>
    public (int Width, int Height) Resolve(int framebufferWidth, int framebufferHeight) => this switch
    {
        Relative relative => (
            Math.Max(1, (int)Math.Round(framebufferWidth * relative.Scale)),
            Math.Max(1, (int)Math.Round(framebufferHeight * relative.Scale))),
        Absolute absolute => (Math.Max(1, absolute.Width), Math.Max(1, absolute.Height)),
        _ => throw new InvalidOperationException("unknown target size")
    };
}
