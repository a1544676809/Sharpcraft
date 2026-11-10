// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

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
        var encoder = p.Encoder;
        var clouds = p.Clouds;

        // Stage 1: depth bounds. Cleared very negative so MAX blending keeps the closest and furthest.
        var boundsPass = encoder.BeginRenderPass(
            [
                new ColorAttachment
                {
                    View = p.Bounds,
                    LoadOp = WGPULoadOp.Clear,
                    StoreOp = WGPUStoreOp.Store,
                    ClearValue = new WGPUColor { r = -65504f, g = -65504f, b = -65504f, a = -65504f }
                }
            ],
            p.Depth);
        boundsPass.SetPipeline(p.BoundsPipeline);
        boundsPass.SetBindGroup(0, p.TerrainAtlas);
        p.DrawTerrain(boundsPass);
        boundsPass.End();

        // Stage 1b: clip the furthest bound to the nearest opaque surface.
        var cullPass = encoder.BeginRenderPass(
            [
                new ColorAttachment
                {
                    View = p.BoundsCulled,
                    LoadOp = WGPULoadOp.Clear,
                    StoreOp = WGPUStoreOp.Store,
                    ClearValue = new WGPUColor { r = 0f, g = 0f, b = 0f, a = 0f }
                }
            ],
            p.Depth);
        cullPass.SetPipeline(p.CullPipeline);
        cullPass.SetBindGroup(0, p.Cull);
        cullPass.Draw(3);
        cullPass.End();

        // Stage 1c: clouds add their own depth bounds into the culled target, after the cull, so cloud bounds never
        // widen the cull applied to the terrain translucent layer and never leak into the composite's depth write.
        if (clouds is not null)
        {
            var cloudBoundsPass = encoder.BeginRenderPass(
                [new ColorAttachment { View = p.BoundsCulled, LoadOp = WGPULoadOp.Load, StoreOp = WGPUStoreOp.Store }],
                p.CloudDepth);
            cloudBoundsPass.SetPipeline(p.CloudBoundsPipeline);
            cloudBoundsPass.SetBindGroup(0, clouds.BindGroup);
            clouds.DrawGeometry(cloudBoundsPass);
            cloudBoundsPass.End();
        }

        // Stage 2: wavelet transmittance, additively blended into two RGBA16F targets.
        var transmittancePass = encoder.BeginRenderPass(
            [
                new ColorAttachment
                {
                    View = p.Transmittance0,
                    LoadOp = WGPULoadOp.Clear,
                    StoreOp = WGPUStoreOp.Store,
                    ClearValue = new WGPUColor { r = 0f, g = 0f, b = 0f, a = 0f }
                },
                new ColorAttachment
                {
                    View = p.Transmittance1,
                    LoadOp = WGPULoadOp.Clear,
                    StoreOp = WGPUStoreOp.Store,
                    ClearValue = new WGPUColor { r = 0f, g = 0f, b = 0f, a = 0f }
                }
            ],
            p.Depth);
        transmittancePass.SetPipeline(p.TransmittancePipeline);
        transmittancePass.SetBindGroup(0, p.TerrainAtlas);
        transmittancePass.SetBindGroup(1, p.TransmittanceGroup);
        p.DrawTerrain(transmittancePass);
        transmittancePass.End();

        if (clouds is not null)
        {
            var cloudTransPass = encoder.BeginRenderPass(
                [
                    new ColorAttachment { View = p.Transmittance0, LoadOp = WGPULoadOp.Load, StoreOp = WGPUStoreOp.Store },
                    new ColorAttachment { View = p.Transmittance1, LoadOp = WGPULoadOp.Load, StoreOp = WGPUStoreOp.Store }
                ],
                p.CloudDepth);
            cloudTransPass.SetPipeline(p.CloudTransmittancePipeline);
            cloudTransPass.SetBindGroup(0, clouds.BindGroup);
            cloudTransPass.SetBindGroup(1, p.TransmittanceGroup);
            clouds.DrawGeometry(cloudTransPass);
            cloudTransPass.End();
        }

        // Stage 3: accumulate colour weighted by the transmittance in front of each fragment.
        var accumulatePass = encoder.BeginRenderPass(
            [
                new ColorAttachment
                {
                    View = p.Accumulate,
                    LoadOp = WGPULoadOp.Clear,
                    StoreOp = WGPUStoreOp.Store,
                    ClearValue = new WGPUColor { r = 0f, g = 0f, b = 0f, a = 0f }
                }
            ],
            p.Depth);
        accumulatePass.SetPipeline(p.AccumulatePipeline);
        accumulatePass.SetBindGroup(0, p.TerrainAtlas);
        accumulatePass.SetBindGroup(1, p.AccumulateGroup);
        p.DrawTerrain(accumulatePass);
        accumulatePass.End();

        if (clouds is not null)
        {
            var cloudAccumPass = encoder.BeginRenderPass(
                [new ColorAttachment { View = p.Accumulate, LoadOp = WGPULoadOp.Load, StoreOp = WGPUStoreOp.Store }],
                p.CloudDepth);
            cloudAccumPass.SetPipeline(p.CloudAccumulatePipeline);
            cloudAccumPass.SetBindGroup(0, clouds.BindGroup);
            cloudAccumPass.SetBindGroup(1, p.AccumulateGroup);
            clouds.DrawGeometry(cloudAccumPass);
            cloudAccumPass.End();
        }

        // Composite over the frame, writing the nearest translucent depth.
        var compositePass = encoder.BeginRenderPass(
            [new ColorAttachment { View = p.Frame, LoadOp = WGPULoadOp.Load, StoreOp = WGPUStoreOp.Store }],
            new DepthStencilAttachment { View = p.Depth.View, DepthLoadOp = WGPULoadOp.Load, DepthStoreOp = WGPUStoreOp.Store });
        compositePass.SetPipeline(p.CompositePipeline);
        compositePass.SetBindGroup(0, p.CompositeGroup);
        compositePass.Draw(3);
        compositePass.End();

        // Translucent items composite through their own set (the reference binds OitPipelineSet("item") to
        // RenderTypes.ITEM_TRANSLUCENT); sharing the terrain bins would band thin items.
        p.Items.Record(
            p.Backend,
            encoder,
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
