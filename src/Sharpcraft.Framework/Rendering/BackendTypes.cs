// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

// No [ReferenceClass] annotation on these types: Sharpcraft.Framework must not reference any Sharpcraft.* assembly
// (tests/Sharpcraft.Architecture.Tests/AssemblyBoundaryTests.cs, FrameworkMustNotReferenceGameOrHostAssemblies), so the
// Sharpcraft.Common.Reference.ReferenceClass attribute cannot resolve here. tools/Sharpcraft.ClassMap does not scan the
// Framework project either (ClassMap.cs, ScanProjects). See the task report for this deviation from the S1 instructions.

using Brew.Wgpu.Native;

namespace Sharpcraft.Framework.Rendering;

/// <summary>
/// Backend-neutral colour and depth texture formats (docs/analysis/architecture/render-backend-seam.md §2).
/// Callers describe textures with this enum; only the backend implementation converts it to a native format.
/// </summary>
public enum TextureFormat
{
    /// <summary>8-bit RGBA, linear.</summary>
    Rgba8Unorm,

    /// <summary>8-bit RGBA, sRGB.</summary>
    Rgba8UnormSrgb,

    /// <summary>8-bit BGRA, linear.</summary>
    Bgra8Unorm,

    /// <summary>8-bit BGRA, sRGB.</summary>
    Bgra8UnormSrgb,

    /// <summary>32-bit floating-point depth.</summary>
    Depth32Float,

    /// <summary>Single-channel 32-bit float.</summary>
    R32Float,

    /// <summary>Four-channel 16-bit float.</summary>
    Rgba16Float,

    /// <summary>Four-channel 32-bit float.</summary>
    Rgba32Float
}

/// <summary>
/// Backend-neutral swapchain presentation modes (docs/analysis/architecture/render-backend-seam.md §2).
/// </summary>
public enum PresentMode
{
    /// <summary>Vsync, FIFO queue (the default).</summary>
    Fifo,

    /// <summary>Vsync with a replaceable mailbox slot.</summary>
    Mailbox,

    /// <summary>No vsync; present immediately.</summary>
    Immediate
}

/// <summary>
/// Converts the backend-neutral enums to their WGPU native counterparts at the boundary of the rendering
/// abstraction layer. Nothing above this layer needs to name a WGPU type.
/// </summary>
internal static class BackendConvert
{
    /// <summary>Maps a neutral texture format onto the matching WGPU native format.</summary>
    /// <summary>Maps a native format back to the neutral one, for call sites that still receive one from the surface.</summary>
    /// <param name="format">Native format.</param>
    internal static TextureFormat FromWgpu(WGPUTextureFormat format) => format switch
    {
        WGPUTextureFormat.RGBA8Unorm => TextureFormat.Rgba8Unorm,
        WGPUTextureFormat.RGBA8UnormSrgb => TextureFormat.Rgba8UnormSrgb,
        WGPUTextureFormat.BGRA8Unorm => TextureFormat.Bgra8Unorm,
        WGPUTextureFormat.BGRA8UnormSrgb => TextureFormat.Bgra8UnormSrgb,
        WGPUTextureFormat.Depth32Float => TextureFormat.Depth32Float,
        WGPUTextureFormat.R32Float => TextureFormat.R32Float,
        WGPUTextureFormat.RGBA16Float => TextureFormat.Rgba16Float,
        WGPUTextureFormat.RGBA32Float => TextureFormat.Rgba32Float,
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "no neutral counterpart")
    };
    internal static WGPUTextureFormat ToWgpu(TextureFormat format) => format switch
    {
        TextureFormat.Rgba8Unorm => WGPUTextureFormat.RGBA8Unorm,
        TextureFormat.Rgba8UnormSrgb => WGPUTextureFormat.RGBA8UnormSrgb,
        TextureFormat.Bgra8Unorm => WGPUTextureFormat.BGRA8Unorm,
        TextureFormat.Bgra8UnormSrgb => WGPUTextureFormat.BGRA8UnormSrgb,
        TextureFormat.Depth32Float => WGPUTextureFormat.Depth32Float,
        TextureFormat.R32Float => WGPUTextureFormat.R32Float,
        TextureFormat.Rgba16Float => WGPUTextureFormat.RGBA16Float,
        TextureFormat.Rgba32Float => WGPUTextureFormat.RGBA32Float,
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "unknown texture format")
    };

    /// <summary>Maps a neutral presentation mode onto the matching WGPU native mode.</summary>
    internal static WGPUPresentMode ToWgpu(PresentMode mode) => mode switch
    {
        PresentMode.Fifo => WGPUPresentMode.Fifo,
        PresentMode.Mailbox => WGPUPresentMode.Mailbox,
        PresentMode.Immediate => WGPUPresentMode.Immediate,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "unknown present mode")
    };
}
