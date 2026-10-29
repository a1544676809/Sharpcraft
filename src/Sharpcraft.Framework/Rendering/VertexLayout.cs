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

    /// <summary>Bytes per interface vertex: position 2, texture coordinate 2, packed colour 1 word.</summary>
    public const int UiStride = 20;

    /// <summary>Offset of the interface position inside its vertex.</summary>
    public const int UiPositionOffset = 0;

    /// <summary>Offset of the packed colour inside the interface vertex.</summary>
    public const int UiColourOffset = 16;

    /// <summary>
    /// Interface attributes. They live here rather than beside the interface pipeline so that every vertex layout
    /// in the engine is declared in exactly one file, which a guard test enforces.
    /// </summary>
    public static VertexAttribute[] UiAttributes() =>
    [
        new(WGPUVertexFormat.Float32x2, UiPositionOffset, 0),
        new(WGPUVertexFormat.Unorm8x4, UiColourOffset, 1)
    ];

    /// <summary>Offset of the texture coordinate inside the interface vertex.</summary>
    public const int UiTextureOffset = 8;

    /// <summary>
    /// Interface attributes for the textured pass. Same vertex as <see cref="UiAttributes"/>, with the texture
    /// coordinate declared as location two so the two shaders can differ only where they must.
    /// </summary>
    public static VertexAttribute[] UiTextAttributes() =>
    [
        new(WGPUVertexFormat.Float32x2, UiPositionOffset, 0),
        new(WGPUVertexFormat.Float32x2, UiTextureOffset, 2),
        new(WGPUVertexFormat.Unorm8x4, UiColourOffset, 1)
    ];

    /// <summary>Builds the attribute list; a fresh array each call because the API copies it.</summary>
    public static VertexAttribute[] Attributes() =>
    [
        new(WGPUVertexFormat.Float32x3, 0, 0),
        new(WGPUVertexFormat.Float32x3, 12, 1),
        new(WGPUVertexFormat.Float32x2, 24, 2),
        new(WGPUVertexFormat.Float32x4, 32, 3)
    ];
}
