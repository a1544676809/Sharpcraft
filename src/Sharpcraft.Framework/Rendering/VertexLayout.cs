// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Brew.Wgpu;
using Brew.Wgpu.Native;

namespace Sharpcraft.Framework.Rendering;

/// <summary>
/// The single vertex layout shared by every pipeline that draws world geometry: position, light
/// colour, tile UV and sprite rect. Keeping one definition matters — the offscreen and windowed
/// pipelines each had their own copy, and when texture coordinates were added only one was updated, so
/// windowed rendering failed pipeline validation because the shader consumed location 2 and nothing
/// provided it (docs/modules/render.md 6.1).
/// </summary>
public static class VertexLayout
{
    /// <summary>Bytes per vertex.</summary>
    public const int Stride = 52;

    /// <summary>Builds the attribute list; a fresh array each call because the API copies it.</summary>
    public static VertexAttribute[] Attributes() =>
    [
        new(WGPUVertexFormat.Float32x4, 0, 0),
        new(WGPUVertexFormat.Float32x3, 16, 1),
        new(WGPUVertexFormat.Float32x2, 28, 2),
        new(WGPUVertexFormat.Float32x4, 36, 3)
    ];
}
