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
    private Texture _texture = default;
    private TextureView _view = default;

    public BindGroup BindGroup { get; private set; } = default;

    public bool Created { get; private set; }

    /// <summary>Uploads a texture, replacing the previous GPU objects when one was already created.</summary>
    public void Upload(
        Device device,
        BindGroupLayout layout,
        TextureData data,
        WgpuBuffer camera,
        WgpuBuffer fog,
        WgpuBuffer lightmap,
        Sampler sampler)
    {
        ArgumentNullException.ThrowIfNull(data);
        DisposeUpload();

        _texture = device.CreateTexture(new TextureDescriptor
        {
            Size = new WGPUExtent3D
            {
                width = (uint)data.Width,
                height = (uint)data.Height,
                depthOrArrayLayers = 1
            },
            MipLevelCount = 1,
            SampleCount = 1,
            Dimension = WGPUTextureDimension._2D,
            Format = WGPUTextureFormat.RGBA8Unorm,
            Usage = TextureUsage.TextureBinding | TextureUsage.CopyDst
        });
        device.Queue.WriteTexture(
            _texture,
            data.Rgba,
            (uint)(data.Width * 4),
            (uint)data.Height,
            new WGPUExtent3D { width = (uint)data.Width, height = (uint)data.Height, depthOrArrayLayers = 1 },
            0,
            new WGPUOrigin3D { x = 0, y = 0, z = 0 });
        _view = _texture.CreateView();
        BindGroup = device.CreateBindGroup(
            layout,
            [
                BindGroupEntry.TextureView(0, _view),
                BindGroupEntry.Sampler(1, sampler),
                BindGroupEntry.Buffer(VertexLayout.CameraBinding, camera, 0, VertexLayout.CameraUniformSize),
                BindGroupEntry.Buffer(3, fog, 0, 32),
                BindGroupEntry.Buffer(Shaders.ShaderBindings.Lightmap, lightmap, 0, Shaders.ShaderBindings.LightmapBytes)
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

        BindGroup.Dispose();
        _view.Dispose();
        _texture.Dispose();
        Created = false;
    }
}
