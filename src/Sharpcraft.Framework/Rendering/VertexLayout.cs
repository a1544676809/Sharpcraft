// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Brew.Wgpu;
using Brew.Wgpu.Native;

namespace Sharpcraft.Framework.Rendering;

/// <summary>
/// The vertex layout shared by every pipeline that draws world geometry: world position, light colour,
/// tile UV and sprite rect. Positions stay in world space and the camera arrives as a uniform, so moving
/// the camera costs one small buffer write instead of re-transforming and re-uploading the whole mesh
/// (docs/modules/render.md 6.1).
/// </summary>
public static class VertexLayout
{
    /// <summary>Bytes per vertex: 3 + 3 + 2 + 4 floats.</summary>
    public const int Stride = 48;

    /// <summary>Size of the camera uniform block in bytes: one 4×4 matrix.</summary>
    public const ulong CameraUniformSize = 64;

    /// <summary>Binding index of the camera uniform inside the shared bind group.</summary>
    public const uint CameraBinding = 2;

    /// <summary>Builds the attribute list; a fresh array each call because the API copies it.</summary>
    public static VertexAttribute[] Attributes() =>
    [
        new(WGPUVertexFormat.Float32x3, 0, 0),
        new(WGPUVertexFormat.Float32x3, 12, 1),
        new(WGPUVertexFormat.Float32x2, 24, 2),
        new(WGPUVertexFormat.Float32x4, 32, 3)
    ];
}
