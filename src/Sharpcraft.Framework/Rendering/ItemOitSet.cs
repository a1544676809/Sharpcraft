// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Brew.Wgpu;
using Brew.Wgpu.Native;

namespace Sharpcraft.Framework.Rendering;

/// <summary>
/// The reference OIT target set that translucent item geometry composites through.
/// </summary>
/// <remarks>
/// The reference binds a dedicated <c>OitPipelineSet("item")</c> to <c>RenderTypes.ITEM_TRANSLUCENT</c>, so item depth
/// bounds never widen the terrain bins; sharing them stretches the eight bins across the whole scene and bands thin
/// items such as a dropped pane of glass (docs/modules/item-rendering.md 3.5.11). This type owns the four targets, their
/// views and the bind groups the three stages and the composite need, so the renderer only has to record four passes.
/// </remarks>
internal sealed class ItemOitSet : IDisposable
{
    private Texture _bounds = default;
    private TextureView _boundsView = default;
    private Texture _coeff0 = default;
    private TextureView _coeff0View = default;
    private Texture _coeff1 = default;
    private TextureView _coeff1View = default;
    private Texture _accumulate = default;
    private TextureView _accumulateView = default;

    /// <summary>True once the targets exist for the current framebuffer size.</summary>
    public bool Created { get; private set; }

    /// <summary>Group 1 for the transmittance stage: the item depth bounds.</summary>
    public BindGroup TransmittanceBindGroup { get; private set; } = default;

    /// <summary>Group 1 for the accumulate stage: bounds plus both coefficient targets.</summary>
    public BindGroup AccumulateBindGroup { get; private set; } = default;

    /// <summary>Group 0 for the composite: accumulate, both coefficients and the bounds.</summary>
    public BindGroup CompositeBindGroup { get; private set; } = default;

    /// <summary>(Re)creates the targets for a framebuffer size, disposing the previous ones.</summary>
    public void Create(
        Device device,
        uint width,
        uint height,
        TextureFormat format,
        BindGroupLayout transmittanceLayout,
        BindGroupLayout accumulateLayout,
        BindGroupLayout compositeLayout,
        Func<Device, uint, uint, TextureFormat, Texture> createTarget)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(createTarget);
        Dispose();

        _bounds = createTarget(device, width, height, format);
        _boundsView = _bounds.CreateView();
        _coeff0 = createTarget(device, width, height, format);
        _coeff0View = _coeff0.CreateView();
        _coeff1 = createTarget(device, width, height, format);
        _coeff1View = _coeff1.CreateView();
        _accumulate = createTarget(device, width, height, format);
        _accumulateView = _accumulate.CreateView();

        TransmittanceBindGroup = device.CreateBindGroup(
            transmittanceLayout,
            [BindGroupEntry.TextureView(0, _boundsView)]);
        AccumulateBindGroup = device.CreateBindGroup(
            accumulateLayout,
            [
                BindGroupEntry.TextureView(0, _boundsView),
                BindGroupEntry.TextureView(1, _coeff0View),
                BindGroupEntry.TextureView(2, _coeff1View)
            ]);
        CompositeBindGroup = device.CreateBindGroup(
            compositeLayout,
            [
                BindGroupEntry.TextureView(0, _accumulateView),
                BindGroupEntry.TextureView(1, _coeff0View),
                BindGroupEntry.TextureView(2, _coeff1View),
                BindGroupEntry.TextureView(3, _boundsView)
            ]);
        Created = true;
    }

    /// <summary>
    /// Records the four item passes: depth bounds, wavelet transmittance, accumulate, then the composite over the frame.
    /// </summary>
    /// <param name="drawGeometry">Draws the item geometry with the item atlas already bound.</param>
    public void Record(
        IRenderBackend backend,
        CommandEncoderHandle encoder,
        ColorAttachment frame,
        DepthStencilAttachment depth,
        RenderPipeline boundsPipeline,
        RenderPipeline transmittancePipeline,
        RenderPipeline accumulatePipeline,
        RenderPipeline compositePipeline,
        BindGroup atlas,
        Action<RenderPassEncoder> drawGeometry)
    {
        ArgumentNullException.ThrowIfNull(drawGeometry);
        if (!Created)
        {
            return;
        }

        // The passes are recorded through the seam; the native objects arrive as parameters and are adopted for the
        // call, which transfers no ownership back to this class.
        var wgpu = backend as WgpuRenderBackend
            ?? throw new InvalidOperationException("the seam implementation must adopt its own handles");
        var encoderHandle = encoder;
        // The incoming attachments carry their own load and store choices; mapping them rather than assuming keeps the
        // callers that load an existing frame working.
        var depthHandle = depth.DepthLoadOp == WGPULoadOp.Clear
            ? DepthAttachmentDesc.Clear(wgpu.AdoptTextureView(depth.View), depth.DepthClearValue)
            : DepthAttachmentDesc.Keep(wgpu.AdoptTextureView(depth.View));
        var frameAttachment = frame.LoadOp == WGPULoadOp.Clear
            ? ColorAttachmentDesc.Clear(
                wgpu.AdoptTextureView(frame.View),
                new Vector4((float)frame.ClearValue.r, (float)frame.ClearValue.g, (float)frame.ClearValue.b, (float)frame.ClearValue.a))
            : ColorAttachmentDesc.Keep(wgpu.AdoptTextureView(frame.View));
        var boundsPass = backend.BeginRenderPass(
            encoderHandle,
            [ColorAttachmentDesc.Clear(wgpu.AdoptTextureView(_boundsView), new Vector4(-65504f, -65504f, -65504f, -65504f))],
            depthHandle);
        try
        {
            backend.SetPipeline(boundsPass, wgpu.AdoptPipeline(boundsPipeline));
            backend.SetBindGroup(boundsPass, 0, wgpu.AdoptBindGroup(atlas));
            drawGeometry(wgpu.AsRenderPass(boundsPass));
        }
        finally
        {
            backend.EndRenderPass(boundsPass);
        }

        var transmittancePass = backend.BeginRenderPass(
            encoderHandle,
            [
                ColorAttachmentDesc.Clear(wgpu.AdoptTextureView(_coeff0View), Vector4.Zero),
                ColorAttachmentDesc.Clear(wgpu.AdoptTextureView(_coeff1View), Vector4.Zero)
            ],
            depthHandle);
        try
        {
            backend.SetPipeline(transmittancePass, wgpu.AdoptPipeline(transmittancePipeline));
            backend.SetBindGroup(transmittancePass, 0, wgpu.AdoptBindGroup(atlas));
            backend.SetBindGroup(transmittancePass, 1, wgpu.AdoptBindGroup(TransmittanceBindGroup));
            drawGeometry(wgpu.AsRenderPass(transmittancePass));
        }
        finally
        {
            backend.EndRenderPass(transmittancePass);
        }

        var accumulatePass = backend.BeginRenderPass(
            encoderHandle,
            [ColorAttachmentDesc.Clear(wgpu.AdoptTextureView(_accumulateView), Vector4.Zero)],
            depthHandle);
        try
        {
            backend.SetPipeline(accumulatePass, wgpu.AdoptPipeline(accumulatePipeline));
            backend.SetBindGroup(accumulatePass, 0, wgpu.AdoptBindGroup(atlas));
            backend.SetBindGroup(accumulatePass, 1, wgpu.AdoptBindGroup(AccumulateBindGroup));
            drawGeometry(wgpu.AsRenderPass(accumulatePass));
        }
        finally
        {
            backend.EndRenderPass(accumulatePass);
        }

        var compositePass = backend.BeginRenderPass(
            encoderHandle,
            [frameAttachment],
            depthHandle);
        try
        {
            backend.SetPipeline(compositePass, wgpu.AdoptPipeline(compositePipeline));
            backend.SetBindGroup(compositePass, 0, wgpu.AdoptBindGroup(CompositeBindGroup));
            backend.Draw(compositePass, 3, 1, 0, 0);
        }
        finally
        {
            backend.EndRenderPass(compositePass);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!Created)
        {
            return;
        }

        TransmittanceBindGroup.Dispose();
        AccumulateBindGroup.Dispose();
        CompositeBindGroup.Dispose();
        _boundsView.Dispose();
        _bounds.Dispose();
        _coeff0View.Dispose();
        _coeff0.Dispose();
        _coeff1View.Dispose();
        _coeff1.Dispose();
        _accumulateView.Dispose();
        _accumulate.Dispose();
        Created = false;
    }
}
