// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Framework.Rendering;

/// <summary>
/// Builds the shared bind group the terrain, cutout and reference transparency passes all draw through.
/// </summary>
/// <remarks>
/// Extracted because the same six entries were spelled out at every place that had to rebuild the group, and the line
/// ratchet on the renderer shells refuses to grow (tests/Sharpcraft.Architecture.Tests/RendererSingleTrackTests.cs).
/// </remarks>
internal static class TerrainBindGroup
{
    /// <summary>Creates the group.</summary>
    /// <param name="backend">Backend to create it on.</param>
    /// <param name="layout">Layout every member of the group agrees on.</param>
    /// <param name="atlas">Atlas view binding zero names.</param>
    /// <param name="sampler">Atlas sampler.</param>
    /// <param name="camera">Camera (or interface transform) uniform.</param>
    /// <param name="fog">Fog uniform.</param>
    /// <param name="lightmap">Lightmap uniform.</param>
    /// <param name="sprites">Sprite rectangle table the vertex stage indexes (T-832).</param>
    public static BindGroupHandle Create(
        IRenderBackend backend,
        BindGroupLayoutHandle layout,
        TextureViewHandle atlas,
        SamplerHandle sampler,
        BufferHandle camera,
        BufferHandle fog,
        BufferHandle lightmap,
        SpriteRectTable sprites) =>
        backend.CreateBindGroup(
            layout,
            [
                BindGroupEntryDesc.Texture(0, atlas),
                BindGroupEntryDesc.Sampler(1, sampler),
                BindGroupEntryDesc.Uniform(VertexLayout.CameraBinding, camera, 0, VertexLayout.CameraUniformSize),
                BindGroupEntryDesc.Uniform(3, fog, 0, 32),
                BindGroupEntryDesc.Uniform(Shaders.ShaderBindings.Lightmap, lightmap, 0, Shaders.ShaderBindings.LightmapBytes),
                BindGroupEntryDesc.ReadOnlyStorage(VertexLayout.SpriteBinding, sprites.Buffer, 0, sprites.Bytes)
            ]);
}
