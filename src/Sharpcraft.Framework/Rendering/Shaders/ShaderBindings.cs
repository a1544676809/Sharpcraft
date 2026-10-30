// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Framework.Rendering.Shaders;

/// <summary>
/// The single source of truth for binding numbers, shared by the layouts and the shaders.
/// </summary>
/// <remarks>
/// Declaring a binding in two places is how the windowed path lost binding 3 while the shader still referenced it; every
/// layout must be built from these numbers (docs/specs/render/shader-organisation.md 4).
/// </remarks>
public static class ShaderBindings
{
    /// <summary>Sampled colour atlas.</summary>
    public const int AtlasTexture = 0;

    /// <summary>Sampler for the atlas.</summary>
    public const int AtlasSampler = 1;

    /// <summary>Camera uniform: one 4x4 matrix, 64 bytes.</summary>
    public const int Camera = 2;

    /// <summary>Fog uniform: colour and range, 32 bytes.</summary>
    public const int Fog = 3;

    /// <summary>Bytes in the camera uniform.</summary>
    public const int CameraBytes = 64;

    /// <summary>Bytes in the fog uniform.</summary>
    public const int FogBytes = 32;
}
