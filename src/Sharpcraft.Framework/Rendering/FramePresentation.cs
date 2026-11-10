// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Brew.Wgpu;
using Brew.Wgpu.Native;

namespace Sharpcraft.Framework.Rendering;

/// <summary>
/// The windowed path's frame target and its presentation onto the swapchain.
/// </summary>
/// <remarks>
/// Every world and interface pass renders into <see cref="Target"/> instead of the surface, and one full-screen triangle
/// copies it onto the swapchain. Two reasons (docs/specs/render/screenshot.md 2.1): swapchain textures are not guaranteed
/// copyable, so a screenshot needs a target it owns, and the presented image is then the same pixels an F2 capture reads.
/// </remarks>
internal sealed class FramePresentation : IDisposable
{
    private readonly Device _device;
    private readonly TextureFormat _format;
    private readonly BindGroupLayout _layout;
    private readonly RenderPipeline _pipeline;
    // Owned by the shared SamplerCache, so this type must never dispose it (T-784 S1b).
    private readonly Sampler _sampler;

    private Texture? _target;
    private TextureView? _targetView;
    private BindGroup? _bindGroup;
    private uint _width;
    private uint _height;

    private FramePresentation(Device device, TextureFormat format, Sampler sampler)
    {
        _device = device;
        _format = format;
        _sampler = sampler;
        _layout = device.CreateBindGroupLayout(
        [
            BindGroupLayoutEntry.Texture(0, ShaderStage.Fragment, WGPUTextureSampleType.Float, WGPUTextureViewDimension._2D, false),
            BindGroupLayoutEntry.Sampler(1, ShaderStage.Fragment, WGPUSamplerBindingType.Filtering)
        ]);

        _pipeline = CreatePipeline(device, device.CreatePipelineLayout([_layout]), format);
    }

    /// <summary>Creates the presentation resources for the given surface format.</summary>
    /// <param name="sampler">Sampler the presentation blit uses; owned by the caller's cache.</param>
    public static FramePresentation Create(Device device, TextureFormat format, Sampler sampler) => new(device, format, sampler);

    /// <summary>The colour texture every pass renders into; recreated when the size changes.</summary>
    public Texture Target => _target ?? throw new InvalidOperationException("the frame target was not created");

    /// <summary>Returns the frame target's view, recreating the target when the frame size changed.</summary>
    public TextureView Ensure(uint width, uint height)
    {
        if (_target is not null && _width == width && _height == height)
        {
            return _targetView ?? throw new InvalidOperationException("the frame target was not created");
        }

        _bindGroup?.Dispose();
        _targetView?.Dispose();
        _target?.Dispose();

        var created = _device.CreateTexture(new TextureDescriptor
        {
            Size = new WGPUExtent3D { width = width, height = height, depthOrArrayLayers = 1 },
            Format = BackendConvert.ToWgpu(_format),
            Usage = TextureUsage.RenderAttachment | TextureUsage.TextureBinding | TextureUsage.CopySrc,
            Dimension = WGPUTextureDimension._2D,
            MipLevelCount = 1,
            SampleCount = 1
        });
        TextureView createdView = created.CreateView();
        _target = created;
        _targetView = createdView;
        _bindGroup = _device.CreateBindGroup(_layout, [BindGroupEntry.TextureView(0, createdView), BindGroupEntry.Sampler(1, _sampler)]);
        _width = width;
        _height = height;
        return createdView;
    }

    /// <summary>Records the full-screen blit that presents the frame target on the swapchain.</summary>
    public void RecordPresent(CommandEncoder encoder, TextureView surfaceView, DepthStencilAttachment depth)
    {
        var pass = encoder.BeginRenderPass(
        [
            new ColorAttachment
            {
                View = surfaceView,
                LoadOp = WGPULoadOp.Clear,
                StoreOp = WGPUStoreOp.Store,
                ClearValue = new WGPUColor { r = 0f, g = 0f, b = 0f, a = 1f }
            }
        ],
        depth);
        pass.SetPipeline(_pipeline);
        pass.SetBindGroup(0, _bindGroup ?? throw new InvalidOperationException("the frame target was not created"));
        pass.Draw(3);
        pass.End();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _bindGroup?.Dispose();
        _targetView?.Dispose();
        _target?.Dispose();
        _pipeline.Dispose();
        _layout.Dispose();
    }

    private static RenderPipeline CreatePipeline(Device device, PipelineLayout layout, TextureFormat format)
    {
        using var shader = device.CreateShaderModule(new ShaderModuleDescriptor
        {
            Source = ShaderSource.FromWgsl(System.Text.Encoding.UTF8.GetBytes(Shaders.ShaderLibrary.Load("blit_color")))
        });

        return device.CreateRenderPipeline(
            shader,
            "vs_fullscreen"u8,
            shader,
            "fs_blit"u8,
            [new ColorTargetState { Format = BackendConvert.ToWgpu(format), WriteMask = ColorWriteMask.All }],
            layout: layout,
            primitive: PrimitiveState.Default with { CullMode = WGPUCullMode.None },
            multisample: MultisampleState.Default,
            depthStencil: DepthStencilState.Depth(WGPUTextureFormat.Depth32Float, WGPUCompareFunction.Always, write: false),
            vertexStride: 0,
            vertexStepMode: WGPUVertexStepMode.Vertex,
            vertexAttributes: []);
    }
}
