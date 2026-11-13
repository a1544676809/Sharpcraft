// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Framework.Rendering;

/// <summary>
/// The uniform bytes a frame rewrites before it submits: the lightmap block, the fog block and the floats
/// that make up the fog block.
/// </summary>
/// <remarks>
/// These were created inside the frame loop and thrown away each time — three allocations per frame, for
/// buffers that are fully overwritten before they are read. Holding them here allocates once instead.
/// <para>
/// They are also the arrays the frame pipeline has to give two copies of: recording the next frame while the
/// previous one is still in flight would overwrite a block the GPU has not read yet. Keeping them in one
/// type is what makes that change a local one (T-837).
/// </para>
/// </remarks>
internal sealed class FrameUniformStaging
{
    /// <summary>Lightmap uniform bytes, rewritten every frame from the day/night state.</summary>
    public byte[] Lightmap { get; } = new byte[Shaders.ShaderBindings.LightmapBytes];

    /// <summary>Fog uniform bytes, rewritten every frame from the fog colour and range.</summary>
    public byte[] Fog { get; } = new byte[Shaders.ShaderBindings.FogBytes];

    /// <summary>The eight floats that <see cref="Fog"/> is a view of.</summary>
    public float[] FogValues { get; } = new float[8];
}
