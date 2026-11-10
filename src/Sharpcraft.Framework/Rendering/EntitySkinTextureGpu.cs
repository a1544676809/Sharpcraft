// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Brew.Wgpu;
using Brew.Wgpu.Native;
using WgpuBuffer = Brew.Wgpu.Buffer;

namespace Sharpcraft.Framework.Rendering;

/// <summary>
/// The entity-skin texture and its bind group. Player skins arrive after the window starts, so the upload is
/// replaceable while the frame loop is running; keeping the GPU handles here stops the windowed shell from growing
/// with texture-cache bookkeeping.
/// </summary>
internal sealed class EntitySkinTextureGpu : IDisposable
{
    private IRenderBackend? _backend;
    private TextureHandle _texture = default;
    private TextureViewHandle _view = default;

    public BindGroupHandle BindGroup { get; private set; } = default;

    public bool Created { get; private set; }

    /// <summary>Uploads a texture, replacing the previous GPU objects when one was already created.</summary>
    public void Upload(
        IRenderBackend backend,
        BindGroupLayoutHandle layout,
        TextureData data,
        BufferHandle camera,
        BufferHandle fog,
        BufferHandle lightmap,
        SamplerHandle sampler)
    {
        ArgumentNullException.ThrowIfNull(data);
        DisposeUpload();

        _backend = backend;
        _texture = backend.CreateTexture(new TextureDesc(
            (uint)data.Width,
            (uint)data.Height,
            TextureFormat.Rgba8Unorm,
            1,
            TextureUsages.TextureBinding | TextureUsages.CopyDst));
        backend.WriteTexture(_texture, data.Rgba, (uint)(data.Width * 4), (uint)data.Width, (uint)data.Height, 0);
        _view = backend.CreateTextureView(_texture);
        BindGroup = backend.CreateBindGroup(
            layout,
            [
                BindGroupEntryDesc.Texture(0, _view),
                BindGroupEntryDesc.Sampler(1, sampler),
                BindGroupEntryDesc.Uniform(VertexLayout.CameraBinding, camera, 0, VertexLayout.CameraUniformSize),
                BindGroupEntryDesc.Uniform(3, fog, 0, 32),
                BindGroupEntryDesc.Uniform(Shaders.ShaderBindings.Lightmap, lightmap, 0, Shaders.ShaderBindings.LightmapBytes)
            ]);
        Created = true;
    }

    /// <summary>Drops the current texture and bind group, leaving the holder empty.</summary>
    public void Clear()
    {
        DisposeUpload();
        BindGroup = default;
    }

    public void Dispose() => DisposeUpload();

    private void DisposeUpload()
    {
        if (!Created)
        {
            return;
        }

        _backend?.Destroy(BindGroup);
        _backend?.Destroy(_view);
        _backend?.Destroy(_texture);
        Created = false;
    }
}
