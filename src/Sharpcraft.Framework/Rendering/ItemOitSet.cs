// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Brew.Wgpu;

namespace Sharpcraft.Framework.Rendering;

/// <summary>
/// The reference OIT target set that translucent item geometry composites through.
/// </summary>
/// <remarks>
/// The reference binds a dedicated <c>OitPipelineSet("item")</c> to <c>RenderTypes.ITEM_TRANSLUCENT</c>, so item depth
/// bounds never widen the terrain bins; sharing them stretches the eight bins across the whole scene and bands thin
/// items such as a dropped pane of glass (docs/modules/item-rendering.md 3.5.11). This type owns the four targets, their
/// views and the bind groups the three stages and the composite need, so the renderer only has to record four passes.
/// Every resource is a seam handle: the class creates its targets through the backend and releases them through it.
/// </remarks>
internal sealed class ItemOitSet : IDisposable
{
    private IRenderBackend? _backend;
    private TextureHandle _bounds = default;
    private TextureViewHandle _boundsView = default;
    private TextureHandle _coeff0 = default;
    private TextureViewHandle _coeff0View = default;
    private TextureHandle _coeff1 = default;
    private TextureViewHandle _coeff1View = default;
    private TextureHandle _accumulate = default;
    private TextureViewHandle _accumulateView = default;

    /// <summary>True once the targets exist for the current framebuffer size.</summary>
    public bool Created { get; private set; }

    /// <summary>Group 1 for the transmittance stage: the item depth bounds.</summary>
    public BindGroupHandle TransmittanceBindGroup { get; private set; } = default;

    /// <summary>Group 1 for the accumulate stage: bounds plus both coefficient targets.</summary>
    public BindGroupHandle AccumulateBindGroup { get; private set; } = default;

    /// <summary>Group 0 for the composite: accumulate, both coefficients and the bounds.</summary>
    public BindGroupHandle CompositeBindGroup { get; private set; } = default;

    /// <summary>(Re)creates the targets for a framebuffer size, disposing the previous ones.</summary>
    public void Create(
        IRenderBackend backend,
        uint width,
        uint height,
        TextureFormat format,
        BindGroupLayoutHandle transmittanceLayout,
        BindGroupLayoutHandle accumulateLayout,
        BindGroupLayoutHandle compositeLayout)
    {
        ArgumentNullException.ThrowIfNull(backend);
        Dispose();

        // Same description as the terrain OIT targets: renderable and sampleable, one mip level.
        var desc = new TextureDesc(width, height, format, 1, TextureUsages.RenderAttachment | TextureUsages.TextureBinding);
        _bounds = backend.CreateTexture(desc);
        _boundsView = backend.CreateTextureView(_bounds);
        _coeff0 = backend.CreateTexture(desc);
        _coeff0View = backend.CreateTextureView(_coeff0);
        _coeff1 = backend.CreateTexture(desc);
        _coeff1View = backend.CreateTextureView(_coeff1);
        _accumulate = backend.CreateTexture(desc);
        _accumulateView = backend.CreateTextureView(_accumulate);

        TransmittanceBindGroup = backend.CreateBindGroup(
            transmittanceLayout,
            [BindGroupEntryDesc.Texture(0, _boundsView)]);
        AccumulateBindGroup = backend.CreateBindGroup(
            accumulateLayout,
            [
                BindGroupEntryDesc.Texture(0, _boundsView),
                BindGroupEntryDesc.Texture(1, _coeff0View),
                BindGroupEntryDesc.Texture(2, _coeff1View)
            ]);
        CompositeBindGroup = backend.CreateBindGroup(
            compositeLayout,
            [
                BindGroupEntryDesc.Texture(0, _accumulateView),
                BindGroupEntryDesc.Texture(1, _coeff0View),
                BindGroupEntryDesc.Texture(2, _coeff1View),
                BindGroupEntryDesc.Texture(3, _boundsView)
            ]);
        _backend = backend;
        Created = true;
    }

    /// <summary>
    /// Records the four item passes: depth bounds, wavelet transmittance, accumulate, then the composite over the frame.
    /// </summary>
    /// <param name="drawGeometry">Draws the item geometry with the item atlas already bound.</param>
    public void Record(
        IRenderBackend backend,
        CommandEncoderHandle encoder,
        ColorAttachmentDesc frame,
        DepthAttachmentDesc depth,
        PipelineHandle boundsPipeline,
        PipelineHandle transmittancePipeline,
        PipelineHandle accumulatePipeline,
        PipelineHandle compositePipeline,
        BindGroupHandle atlas,
        Action<RenderPassEncoder> drawGeometry)
    {
        ArgumentNullException.ThrowIfNull(drawGeometry);
        if (!Created)
        {
            return;
        }

        // The passes are recorded through the seam; only the geometry callback still speaks the native pass, because the
        // arena draw helpers have not moved behind the seam yet.
        var wgpu = backend as WgpuRenderBackend
            ?? throw new InvalidOperationException("the seam implementation must adopt its own handles");

        var boundsPass = backend.BeginRenderPass(
            encoder,
            [ColorAttachmentDesc.Clear(_boundsView, new Vector4(-65504f, -65504f, -65504f, -65504f))],
            depth);
        try
        {
            backend.SetPipeline(boundsPass, boundsPipeline);
            backend.SetBindGroup(boundsPass, 0, atlas);
            drawGeometry(wgpu.AsRenderPass(boundsPass));
        }
        finally
        {
            backend.EndRenderPass(boundsPass);
        }

        var transmittancePass = backend.BeginRenderPass(
            encoder,
            [
                ColorAttachmentDesc.Clear(_coeff0View, Vector4.Zero),
                ColorAttachmentDesc.Clear(_coeff1View, Vector4.Zero)
            ],
            depth);
        try
        {
            backend.SetPipeline(transmittancePass, transmittancePipeline);
            backend.SetBindGroup(transmittancePass, 0, atlas);
            backend.SetBindGroup(transmittancePass, 1, TransmittanceBindGroup);
            drawGeometry(wgpu.AsRenderPass(transmittancePass));
        }
        finally
        {
            backend.EndRenderPass(transmittancePass);
        }

        var accumulatePass = backend.BeginRenderPass(
            encoder,
            [ColorAttachmentDesc.Clear(_accumulateView, Vector4.Zero)],
            depth);
        try
        {
            backend.SetPipeline(accumulatePass, accumulatePipeline);
            backend.SetBindGroup(accumulatePass, 0, atlas);
            backend.SetBindGroup(accumulatePass, 1, AccumulateBindGroup);
            drawGeometry(wgpu.AsRenderPass(accumulatePass));
        }
        finally
        {
            backend.EndRenderPass(accumulatePass);
        }

        var compositePass = backend.BeginRenderPass(encoder, [frame], depth);
        try
        {
            backend.SetPipeline(compositePass, compositePipeline);
            backend.SetBindGroup(compositePass, 0, CompositeBindGroup);
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

        var backend = _backend ?? throw new InvalidOperationException("the item OIT set was not created through a backend");
        backend.Destroy(TransmittanceBindGroup);
        backend.Destroy(AccumulateBindGroup);
        backend.Destroy(CompositeBindGroup);
        backend.Destroy(_boundsView);
        backend.Destroy(_bounds);
        backend.Destroy(_coeff0View);
        backend.Destroy(_coeff0);
        backend.Destroy(_coeff1View);
        backend.Destroy(_coeff1);
        backend.Destroy(_accumulateView);
        backend.Destroy(_accumulate);
        _backend = null;
        Created = false;
    }
}
