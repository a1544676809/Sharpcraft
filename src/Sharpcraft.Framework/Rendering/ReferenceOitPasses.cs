// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Brew.Wgpu;
using Brew.Wgpu.Native;

namespace Sharpcraft.Framework.Rendering;

/// <summary>
/// The reference transparency pass sequence: depth bounds, the opaque-depth cull, the cloud sub-passes, wavelet
/// transmittance, accumulate, and the composite, plus the item OIT set that follows it.
/// </summary>
/// <remarks>
/// Extracted from <see cref="WindowedRenderer"/> so the renderer keeps to the "one pass, one place" rule
/// (docs/specs/render/pipeline.md 15, T-776). Every view, pipeline and bind group is supplied by the caller: this type
/// owns no resources and only records the passes in the order the reference uses.
/// </remarks>
internal static class ReferenceOitPasses
{
    /// <summary>Everything the sequence draws with, supplied by the windowed renderer for this frame.</summary>
    internal readonly struct Parameters
    {
        /// <summary>Encoder the passes are recorded into.</summary>
        /// <summary>The seam the passes are recorded through.</summary>
    public required IRenderBackend Backend { get; init; }

    /// <summary>The encoder this frame records into.</summary>
    public CommandEncoder Encoder { get; init; }

        /// <summary>Frame colour target the final composite writes over.</summary>
        public TextureView Frame { get; init; }

        /// <summary>Main depth used by the terrain stages.</summary>
        public DepthStencilAttachment Depth { get; init; }

        /// <summary>Independent cloud depth, or <see cref="Depth"/> when clouds do not use OIT.</summary>
        public DepthStencilAttachment CloudDepth { get; init; }

        /// <summary>Terrain depth bounds (MAX blended).</summary>
        public TextureView Bounds { get; init; }

        /// <summary>Terrain depth bounds after the opaque-depth cull; the coefficient stages read this one.</summary>
        public TextureView BoundsCulled { get; init; }

        /// <summary>First wavelet coefficient target.</summary>
        public TextureView Transmittance0 { get; init; }

        /// <summary>Second wavelet coefficient target.</summary>
        public TextureView Transmittance1 { get; init; }

        /// <summary>Weighted colour accumulation target.</summary>
        public TextureView Accumulate { get; init; }

        /// <summary>Depth-bounds pipeline for the terrain geometry.</summary>
        public RenderPipeline BoundsPipeline { get; init; }

        /// <summary>Full-screen cull pipeline that clips the furthest bound to the nearest opaque surface.</summary>
        public RenderPipeline CullPipeline { get; init; }

        /// <summary>Wavelet transmittance pipeline.</summary>
        public RenderPipeline TransmittancePipeline { get; init; }

        /// <summary>Accumulate pipeline.</summary>
        public RenderPipeline AccumulatePipeline { get; init; }

        /// <summary>Composite pipeline.</summary>
        public RenderPipeline CompositePipeline { get; init; }

        /// <summary>The main atlas bind group the terrain stages sample.</summary>
        public BindGroup TerrainAtlas { get; init; }

        /// <summary>Group 0 of the cull pass: the terrain bounds.</summary>
        public BindGroup Cull { get; init; }

        /// <summary>Group 1 of the transmittance stage.</summary>
        public BindGroup TransmittanceGroup { get; init; }

        /// <summary>Group 1 of the accumulate stage.</summary>
        public BindGroup AccumulateGroup { get; init; }

        /// <summary>Group 0 of the composite.</summary>
        public BindGroup CompositeGroup { get; init; }

        /// <summary>Cloud OIT geometry and atlas, or null when clouds do not use OIT this frame.</summary>
        public CloudOitResources? Clouds { get; init; }

        /// <summary>Cloud depth-bounds pipeline; unused when <see cref="Clouds"/> is null.</summary>
        public RenderPipeline CloudBoundsPipeline { get; init; }

        /// <summary>Cloud transmittance pipeline; unused when <see cref="Clouds"/> is null.</summary>
        public RenderPipeline CloudTransmittancePipeline { get; init; }

        /// <summary>Cloud accumulate pipeline; unused when <see cref="Clouds"/> is null.</summary>
        public RenderPipeline CloudAccumulatePipeline { get; init; }

        /// <summary>The translucent item set, recorded after the terrain composite.</summary>
        public ItemOitSet Items { get; init; }

        /// <summary>Item atlas bind group the item stages sample.</summary>
        public BindGroup ItemAtlas { get; init; }

        /// <summary>Records the translucent terrain geometry.</summary>
        public Action<RenderPassEncoder> DrawTerrain { get; init; }

        /// <summary>Records the translucent item geometry.</summary>
        public Action<RenderPassEncoder> DrawItems { get; init; }
    }

    /// <summary>Records every reference transparency pass for the frame.</summary>
    public static void Record(in Parameters p)
    {
        // Every pass is recorded through the seam. The native attachments arrive in the parameter object and are
        // adopted for the call, so this class owns nothing it did not create.
        var backend = p.Backend;
        var wgpu = backend as WgpuRenderBackend
            ?? throw new InvalidOperationException("the seam implementation must adopt its own handles");
        var encoder = wgpu.AdoptCommandEncoder(p.Encoder);
        var clouds = p.Clouds;
        var zero = Vector4.Zero;

        static DepthAttachmentDesc DepthOf(WgpuRenderBackend w, DepthStencilAttachment depth) =>
            depth.DepthLoadOp == WGPULoadOp.Clear
                ? DepthAttachmentDesc.Clear(w.AdoptTextureView(depth.View), depth.DepthClearValue)
                : DepthAttachmentDesc.Keep(w.AdoptTextureView(depth.View));

        // Stage 1: depth bounds. Cleared very negative so MAX blending keeps the closest and furthest.
        var boundsPass = backend.BeginRenderPass(
            encoder,
            [new ColorAttachmentDesc(wgpu.AdoptTextureView(p.Bounds), LoadOp.Clear, StoreOp.Store, new Vector4(-65504f, -65504f, -65504f, -65504f))],
            DepthOf(wgpu, p.Depth));
        try
        {
            backend.SetPipeline(boundsPass, wgpu.AdoptPipeline(p.BoundsPipeline));
            backend.SetBindGroup(boundsPass, 0, wgpu.AdoptBindGroup(p.TerrainAtlas));
            p.DrawTerrain(wgpu.AsRenderPass(boundsPass));
        }
        finally
        {
            backend.EndRenderPass(boundsPass);
        }

        // Stage 1b: clip the furthest bound to the nearest opaque surface.
        var cullPass = backend.BeginRenderPass(
            encoder,
            [new ColorAttachmentDesc(wgpu.AdoptTextureView(p.BoundsCulled), LoadOp.Clear, StoreOp.Store, zero)],
            DepthOf(wgpu, p.Depth));
        try
        {
            backend.SetPipeline(cullPass, wgpu.AdoptPipeline(p.CullPipeline));
            backend.SetBindGroup(cullPass, 0, wgpu.AdoptBindGroup(p.Cull));
            backend.Draw(cullPass, 3, 1, 0, 0);
        }
        finally
        {
            backend.EndRenderPass(cullPass);
        }

        // Stage 1c: clouds add their own depth bounds into the culled target, after the cull, so cloud bounds never
        // widen the cull applied to the terrain translucent layer and never leak into the composite's depth write.
        if (clouds is not null)
        {
            var cloudBoundsPass = backend.BeginRenderPass(
                encoder,
                [ColorAttachmentDesc.Keep(wgpu.AdoptTextureView(p.BoundsCulled))],
                DepthOf(wgpu, p.CloudDepth));
            try
            {
                backend.SetPipeline(cloudBoundsPass, wgpu.AdoptPipeline(p.CloudBoundsPipeline));
                backend.SetBindGroup(cloudBoundsPass, 0, wgpu.AdoptBindGroup(clouds.BindGroup));
                clouds.DrawGeometry(wgpu.AsRenderPass(cloudBoundsPass));
            }
            finally
            {
                backend.EndRenderPass(cloudBoundsPass);
            }
        }

        // Stage 2: wavelet transmittance, additively blended into two RGBA16F targets.
        var transmittancePass = backend.BeginRenderPass(
            encoder,
            [
                new ColorAttachmentDesc(wgpu.AdoptTextureView(p.Transmittance0), LoadOp.Clear, StoreOp.Store, zero),
                new ColorAttachmentDesc(wgpu.AdoptTextureView(p.Transmittance1), LoadOp.Clear, StoreOp.Store, zero)
            ],
            DepthOf(wgpu, p.Depth));
        try
        {
            backend.SetPipeline(transmittancePass, wgpu.AdoptPipeline(p.TransmittancePipeline));
            backend.SetBindGroup(transmittancePass, 0, wgpu.AdoptBindGroup(p.TerrainAtlas));
            backend.SetBindGroup(transmittancePass, 1, wgpu.AdoptBindGroup(p.TransmittanceGroup));
            p.DrawTerrain(wgpu.AsRenderPass(transmittancePass));
        }
        finally
        {
            backend.EndRenderPass(transmittancePass);
        }

        if (clouds is not null)
        {
            var cloudTransPass = backend.BeginRenderPass(
                encoder,
                [
                    ColorAttachmentDesc.Keep(wgpu.AdoptTextureView(p.Transmittance0)),
                    ColorAttachmentDesc.Keep(wgpu.AdoptTextureView(p.Transmittance1))
                ],
                DepthOf(wgpu, p.CloudDepth));
            try
            {
                backend.SetPipeline(cloudTransPass, wgpu.AdoptPipeline(p.CloudTransmittancePipeline));
                backend.SetBindGroup(cloudTransPass, 0, wgpu.AdoptBindGroup(clouds.BindGroup));
                backend.SetBindGroup(cloudTransPass, 1, wgpu.AdoptBindGroup(p.TransmittanceGroup));
                clouds.DrawGeometry(wgpu.AsRenderPass(cloudTransPass));
            }
            finally
            {
                backend.EndRenderPass(cloudTransPass);
            }
        }

        // Stage 3: accumulate colour weighted by the transmittance in front of each fragment.
        var accumulatePass = backend.BeginRenderPass(
            encoder,
            [new ColorAttachmentDesc(wgpu.AdoptTextureView(p.Accumulate), LoadOp.Clear, StoreOp.Store, zero)],
            DepthOf(wgpu, p.Depth));
        try
        {
            backend.SetPipeline(accumulatePass, wgpu.AdoptPipeline(p.AccumulatePipeline));
            backend.SetBindGroup(accumulatePass, 0, wgpu.AdoptBindGroup(p.TerrainAtlas));
            backend.SetBindGroup(accumulatePass, 1, wgpu.AdoptBindGroup(p.AccumulateGroup));
            p.DrawTerrain(wgpu.AsRenderPass(accumulatePass));
        }
        finally
        {
            backend.EndRenderPass(accumulatePass);
        }

        if (clouds is not null)
        {
            var cloudAccumPass = backend.BeginRenderPass(
                encoder,
                [ColorAttachmentDesc.Keep(wgpu.AdoptTextureView(p.Accumulate))],
                DepthOf(wgpu, p.CloudDepth));
            try
            {
                backend.SetPipeline(cloudAccumPass, wgpu.AdoptPipeline(p.CloudAccumulatePipeline));
                backend.SetBindGroup(cloudAccumPass, 0, wgpu.AdoptBindGroup(clouds.BindGroup));
                backend.SetBindGroup(cloudAccumPass, 1, wgpu.AdoptBindGroup(p.AccumulateGroup));
                clouds.DrawGeometry(wgpu.AsRenderPass(cloudAccumPass));
            }
            finally
            {
                backend.EndRenderPass(cloudAccumPass);
            }
        }

        // Composite over the frame, writing the nearest translucent depth.
        var compositePass = backend.BeginRenderPass(
            encoder,
            [ColorAttachmentDesc.Keep(wgpu.AdoptTextureView(p.Frame))],
            DepthOf(wgpu, p.Depth));
        try
        {
            backend.SetPipeline(compositePass, wgpu.AdoptPipeline(p.CompositePipeline));
            backend.SetBindGroup(compositePass, 0, wgpu.AdoptBindGroup(p.CompositeGroup));
            backend.Draw(compositePass, 3, 1, 0, 0);
        }
        finally
        {
            backend.EndRenderPass(compositePass);
        }

        // Translucent items composite through their own set (the reference binds OitPipelineSet("item") to
        // RenderTypes.ITEM_TRANSLUCENT); sharing the terrain bins would band thin items.
        p.Items.Record(
            p.Backend,
            p.Encoder,
            new ColorAttachment { View = p.Frame, LoadOp = WGPULoadOp.Load, StoreOp = WGPUStoreOp.Store },
            new DepthStencilAttachment { View = p.Depth.View, DepthLoadOp = WGPULoadOp.Load, DepthStoreOp = WGPUStoreOp.Store },
            p.BoundsPipeline,
            p.TransmittancePipeline,
            p.AccumulatePipeline,
            p.CompositePipeline,
            p.ItemAtlas,
            p.DrawItems);
    }
}
