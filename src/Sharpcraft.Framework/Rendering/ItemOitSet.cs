// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

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
        CommandEncoder encoder,
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

        var boundsPass = encoder.BeginRenderPass(
            [
                new ColorAttachment
                {
                    View = _boundsView,
                    LoadOp = WGPULoadOp.Clear,
                    StoreOp = WGPUStoreOp.Store,
                    ClearValue = new WGPUColor { r = -65504f, g = -65504f, b = -65504f, a = -65504f }
                }
            ],
            depth);
        boundsPass.SetPipeline(boundsPipeline);
        boundsPass.SetBindGroup(0, atlas);
        drawGeometry(boundsPass);
        boundsPass.End();

        var transmittancePass = encoder.BeginRenderPass(
            [
                new ColorAttachment { View = _coeff0View, LoadOp = WGPULoadOp.Clear, StoreOp = WGPUStoreOp.Store, ClearValue = new WGPUColor { r = 0f, g = 0f, b = 0f, a = 0f } },
                new ColorAttachment { View = _coeff1View, LoadOp = WGPULoadOp.Clear, StoreOp = WGPUStoreOp.Store, ClearValue = new WGPUColor { r = 0f, g = 0f, b = 0f, a = 0f } }
            ],
            depth);
        transmittancePass.SetPipeline(transmittancePipeline);
        transmittancePass.SetBindGroup(0, atlas);
        transmittancePass.SetBindGroup(1, TransmittanceBindGroup);
        drawGeometry(transmittancePass);
        transmittancePass.End();

        var accumulatePass = encoder.BeginRenderPass(
            [
                new ColorAttachment { View = _accumulateView, LoadOp = WGPULoadOp.Clear, StoreOp = WGPUStoreOp.Store, ClearValue = new WGPUColor { r = 0f, g = 0f, b = 0f, a = 0f } }
            ],
            depth);
        accumulatePass.SetPipeline(accumulatePipeline);
        accumulatePass.SetBindGroup(0, atlas);
        accumulatePass.SetBindGroup(1, AccumulateBindGroup);
        drawGeometry(accumulatePass);
        accumulatePass.End();

        var compositePass = encoder.BeginRenderPass([frame], depth);
        compositePass.SetPipeline(compositePipeline);
        compositePass.SetBindGroup(0, CompositeBindGroup);
        compositePass.Draw(3);
        compositePass.End();
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
