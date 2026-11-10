// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Brew.Wgpu;
using Brew.Wgpu.Native;

namespace Sharpcraft.Framework.Rendering;

/// <summary>
/// The windowed path's frame target and its presentation onto the swapchain.
/// </summary>
/// <remarks>
/// Every world and interface pass renders into <see cref="Ensure"/>'s view instead of the surface, and one full-screen
/// triangle copies it onto the swapchain. Two reasons (docs/specs/render/screenshot.md 2.1): swapchain textures are not
/// guaranteed copyable, so a screenshot needs a target it owns, and the presented image is then the same pixels an F2
/// capture reads.
/// <para>
/// The layout, bind group, pipeline and the target itself are all seam handles: the capture path copies from the target
/// through <see cref="IRenderBackend.CopyTextureToBuffer"/>, so nothing here needs the native texture.
/// </para>
/// </remarks>
internal sealed class FramePresentation : IDisposable
{
    private readonly IRenderBackend _backend;
    private readonly TextureFormat _format;
    private readonly BindGroupLayoutHandle _layout;
    private readonly PipelineLayoutHandle _pipelineLayout;
    private readonly PipelineHandle _pipeline;
    private readonly SamplerHandle _samplerHandle;

    private TextureHandle _target;
    private TextureViewHandle _targetViewHandle;
    private BindGroupHandle _bindGroup;
    private uint _width;
    private uint _height;

    // The sampler is owned by the caller's cache, so only a handle to it is kept and it is never disposed here (T-784 S1b).
    private FramePresentation(WgpuRenderBackend backend, TextureFormat format, SamplerHandle sampler)
    {
        _backend = backend;
        _format = format;
        _samplerHandle = sampler;
        _layout = backend.CreateBindGroupLayout(
        [
            BindGroupLayoutEntryDesc.Texture(0, ShaderStages.Fragment),
            BindGroupLayoutEntryDesc.Sampler(1, ShaderStages.Fragment)
        ]);

        _pipelineLayout = backend.CreatePipelineLayout([_layout]);
        _pipeline = CreatePipeline(backend, _pipelineLayout, format);
    }

    /// <summary>Creates the presentation resources for the given surface format.</summary>
    /// <param name="sampler">Sampling state handle the presentation blit uses; owned by the caller's cache.</param>
    /// <returns>The presentation resources; dispose them with the frame.</returns>
    public static FramePresentation Create(WgpuRenderBackend backend, TextureFormat format, SamplerHandle sampler) => new(backend, format, sampler);

    /// <summary>The colour texture every pass renders into, as the capture path copies from it.</summary>
    public TextureHandle TargetHandle => _target;

    /// <summary>Returns the frame target's view, recreating the target when the frame size changed.</summary>
    public TextureViewHandle Ensure(uint width, uint height)
    {
        if (_target.IsValid && _width == width && _height == height)
        {
            return _targetViewHandle;
        }

        if (_bindGroup.IsValid)
        {
            _backend.Destroy(_bindGroup);
            _bindGroup = default;
        }

        if (_targetViewHandle.IsValid)
        {
            _backend.Destroy(_targetViewHandle);
            _backend.Destroy(_target);
        }

        _target = _backend.CreateTexture(new TextureDesc(
            width,
            height,
            _format,
            1,
            TextureUsages.RenderAttachment | TextureUsages.TextureBinding | TextureUsages.CopySrc));
        _targetViewHandle = _backend.CreateTextureView(_target);
        _bindGroup = _backend.CreateBindGroup(
            _layout,
            [
                BindGroupEntryDesc.Texture(0, _targetViewHandle),
                BindGroupEntryDesc.Sampler(1, _samplerHandle)
            ]);
        _width = width;
        _height = height;
        return _targetViewHandle;
    }

    /// <summary>Records the full-screen blit that presents the frame target on the swapchain.</summary>
    public void RecordPresent(IRenderBackend backend, CommandEncoderHandle encoder, TextureViewHandle surfaceView, in DepthAttachmentDesc? depth)
    {
        ArgumentNullException.ThrowIfNull(backend);

        var pass = backend.BeginRenderPass(
            encoder,
            [ColorAttachmentDesc.Clear(surfaceView, new Vector4(0f, 0f, 0f, 1f))],
            depth);
        try
        {
            backend.SetPipeline(pass, _pipeline);
            backend.SetBindGroup(pass, 0, _bindGroup);
            backend.Draw(pass, 3, 1, 0, 0);
        }
        finally
        {
            backend.EndRenderPass(pass);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_bindGroup.IsValid)
        {
            _backend.Destroy(_bindGroup);
        }

        if (_targetViewHandle.IsValid)
        {
            _backend.Destroy(_targetViewHandle);
            _backend.Destroy(_target);
        }

        _backend.Destroy(_pipeline);
        _backend.Destroy(_pipelineLayout);
        _backend.Destroy(_layout);
    }

    private static PipelineHandle CreatePipeline(WgpuRenderBackend backend, PipelineLayoutHandle layout, TextureFormat format)
    {
        // Described to the backend rather than built here, so presentation joins the same path as every other pass.
        var source = System.Text.Encoding.UTF8.GetBytes(Shaders.ShaderLibrary.Load("blit_color"));
        return backend.CreateRenderPipeline(new PipelineDesc(
            source,
            "vs_fullscreen",
            source,
            "fs_blit",
            layout,
            [new ColorTargetDesc(format)],
            new DepthStateDesc(TextureFormat.Depth32Float, CompareFunction.Always, false),
            CullMode.None,
            FrontFace.Ccw,
            0,
            VertexStepMode.Vertex,
            []));
    }
}
