// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Brew.Wgpu;

namespace Sharpcraft.Framework.Rendering;

/// <summary>
/// The reference transparency pass sequence: depth bounds, the opaque-depth cull, the cloud sub-passes, wavelet
/// transmittance, accumulate, and the composite, plus the item OIT set that follows it.
/// </summary>
/// <remarks>
/// Extracted from <see cref="WindowedRenderer"/> so the renderer keeps to the "one pass, one place" rule
/// (docs/specs/render/pipeline.md 15, T-776). Every pipeline, view and bind group is supplied by the caller: this type
/// owns no resources and only records the passes in the order the reference uses.
/// </remarks>
internal static class ReferenceOitPasses
{
    /// <summary>Everything the sequence draws with, supplied by the windowed renderer for this frame.</summary>
    internal readonly struct Parameters
    {
        /// <summary>The seam the passes are recorded through.</summary>
        public required IRenderBackend Backend { get; init; }

        /// <summary>The encoder this frame records into.</summary>
        public CommandEncoderHandle Encoder { get; init; }

        /// <summary>Frame colour target the final composite writes over.</summary>
        public TextureViewHandle Frame { get; init; }

        /// <summary>Main depth used by the terrain stages.</summary>
        public DepthAttachmentDesc Depth { get; init; }

        /// <summary>Independent cloud depth, or <see cref="Depth"/> when clouds do not use OIT.</summary>
        public DepthAttachmentDesc CloudDepth { get; init; }

        /// <summary>Terrain depth bounds (MAX blended).</summary>
        public TextureViewHandle Bounds { get; init; }

        /// <summary>Terrain depth bounds after the opaque-depth cull; the coefficient stages read this one.</summary>
        public TextureViewHandle BoundsCulled { get; init; }

        /// <summary>First wavelet coefficient target.</summary>
        public TextureViewHandle Transmittance0 { get; init; }

        /// <summary>Second wavelet coefficient target.</summary>
        public TextureViewHandle Transmittance1 { get; init; }

        /// <summary>Weighted colour accumulation target.</summary>
        public TextureViewHandle Accumulate { get; init; }

        /// <summary>Depth-bounds pipeline for the terrain geometry.</summary>
        public PipelineHandle BoundsPipeline { get; init; }

        /// <summary>Full-screen cull pipeline that clips the furthest bound to the nearest opaque surface.</summary>
        public PipelineHandle CullPipeline { get; init; }

        /// <summary>Wavelet transmittance pipeline.</summary>
        public PipelineHandle TransmittancePipeline { get; init; }

        /// <summary>Accumulate pipeline.</summary>
        public PipelineHandle AccumulatePipeline { get; init; }

        /// <summary>Composite pipeline.</summary>
        public PipelineHandle CompositePipeline { get; init; }

        /// <summary>The main atlas bind group the terrain stages sample.</summary>
        public BindGroupHandle TerrainAtlas { get; init; }

        /// <summary>Group 0 of the cull pass: the terrain bounds.</summary>
        public BindGroupHandle Cull { get; init; }

        /// <summary>Group 1 of the transmittance stage.</summary>
        public BindGroupHandle TransmittanceGroup { get; init; }

        /// <summary>Group 1 of the accumulate stage.</summary>
        public BindGroupHandle AccumulateGroup { get; init; }

        /// <summary>Group 0 of the composite.</summary>
        public BindGroupHandle CompositeGroup { get; init; }

        /// <summary>Cloud OIT geometry and atlas, or null when clouds do not use OIT this frame.</summary>
        public CloudOitResources? Clouds { get; init; }

        /// <summary>Cloud depth-bounds pipeline; unused when <see cref="Clouds"/> is null.</summary>
        public PipelineHandle CloudBoundsPipeline { get; init; }

        /// <summary>Cloud transmittance pipeline; unused when <see cref="Clouds"/> is null.</summary>
        public PipelineHandle CloudTransmittancePipeline { get; init; }

        /// <summary>Cloud accumulate pipeline; unused when <see cref="Clouds"/> is null.</summary>
        public PipelineHandle CloudAccumulatePipeline { get; init; }

        /// <summary>The translucent item set, recorded after the terrain composite.</summary>
        public ItemOitSet Items { get; init; }

        /// <summary>Item atlas bind group the item stages sample.</summary>
        public BindGroupHandle ItemAtlas { get; init; }

        /// <summary>Records the translucent terrain geometry.</summary>
        public Action<RenderPassHandle> DrawTerrain { get; init; }

        /// <summary>Records the translucent item geometry.</summary>
        public Action<RenderPassHandle> DrawItems { get; init; }
    }

    /// <summary>Records every reference transparency pass for the frame.</summary>
    public static void Record(in Parameters p)
    {
        // Every pass is recorded through the seam. Only the item geometry callback still takes the native pass, because
        // the per-layer entity buffers it binds have not moved behind the seam yet.
        var backend = p.Backend;
        var encoder = p.Encoder;
        var clouds = p.Clouds;
        var zero = Vector4.Zero;

        // Stage 1: depth bounds. Cleared very negative so MAX blending keeps the closest and furthest.
        var boundsPass = backend.BeginRenderPass(
            encoder,
            [new ColorAttachmentDesc(p.Bounds, LoadOp.Clear, StoreOp.Store, new Vector4(-65504f, -65504f, -65504f, -65504f))],
            p.Depth);
        try
        {
            backend.SetPipeline(boundsPass, p.BoundsPipeline);
            backend.SetBindGroup(boundsPass, 0, p.TerrainAtlas);
            p.DrawTerrain(boundsPass);
        }
        finally
        {
            backend.EndRenderPass(boundsPass);
        }

        // Stage 1b: clip the furthest bound to the nearest opaque surface.
        var cullPass = backend.BeginRenderPass(
            encoder,
            [new ColorAttachmentDesc(p.BoundsCulled, LoadOp.Clear, StoreOp.Store, zero)],
            p.Depth);
        try
        {
            backend.SetPipeline(cullPass, p.CullPipeline);
            backend.SetBindGroup(cullPass, 0, p.Cull);
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
                [ColorAttachmentDesc.Keep(p.BoundsCulled)],
                p.CloudDepth);
            try
            {
                backend.SetPipeline(cloudBoundsPass, p.CloudBoundsPipeline);
                backend.SetBindGroup(cloudBoundsPass, 0, clouds.BindGroup);
                clouds.DrawGeometry(backend, cloudBoundsPass);
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
                new ColorAttachmentDesc(p.Transmittance0, LoadOp.Clear, StoreOp.Store, zero),
                new ColorAttachmentDesc(p.Transmittance1, LoadOp.Clear, StoreOp.Store, zero)
            ],
            p.Depth);
        try
        {
            backend.SetPipeline(transmittancePass, p.TransmittancePipeline);
            backend.SetBindGroup(transmittancePass, 0, p.TerrainAtlas);
            backend.SetBindGroup(transmittancePass, 1, p.TransmittanceGroup);
            p.DrawTerrain(transmittancePass);
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
                    ColorAttachmentDesc.Keep(p.Transmittance0),
                    ColorAttachmentDesc.Keep(p.Transmittance1)
                ],
                p.CloudDepth);
            try
            {
                backend.SetPipeline(cloudTransPass, p.CloudTransmittancePipeline);
                backend.SetBindGroup(cloudTransPass, 0, clouds.BindGroup);
                backend.SetBindGroup(cloudTransPass, 1, p.TransmittanceGroup);
                clouds.DrawGeometry(backend, cloudTransPass);
            }
            finally
            {
                backend.EndRenderPass(cloudTransPass);
            }
        }

        // Stage 3: accumulate colour weighted by the transmittance in front of each fragment.
        var accumulatePass = backend.BeginRenderPass(
            encoder,
            [new ColorAttachmentDesc(p.Accumulate, LoadOp.Clear, StoreOp.Store, zero)],
            p.Depth);
        try
        {
            backend.SetPipeline(accumulatePass, p.AccumulatePipeline);
            backend.SetBindGroup(accumulatePass, 0, p.TerrainAtlas);
            backend.SetBindGroup(accumulatePass, 1, p.AccumulateGroup);
            p.DrawTerrain(accumulatePass);
        }
        finally
        {
            backend.EndRenderPass(accumulatePass);
        }

        if (clouds is not null)
        {
            var cloudAccumPass = backend.BeginRenderPass(
                encoder,
                [ColorAttachmentDesc.Keep(p.Accumulate)],
                p.CloudDepth);
            try
            {
                backend.SetPipeline(cloudAccumPass, p.CloudAccumulatePipeline);
                backend.SetBindGroup(cloudAccumPass, 0, clouds.BindGroup);
                backend.SetBindGroup(cloudAccumPass, 1, p.AccumulateGroup);
                clouds.DrawGeometry(backend, cloudAccumPass);
            }
            finally
            {
                backend.EndRenderPass(cloudAccumPass);
            }
        }

        // Composite over the frame, writing the nearest translucent depth.
        var compositePass = backend.BeginRenderPass(
            encoder,
            [ColorAttachmentDesc.Keep(p.Frame)],
            p.Depth);
        try
        {
            backend.SetPipeline(compositePass, p.CompositePipeline);
            backend.SetBindGroup(compositePass, 0, p.CompositeGroup);
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
            ColorAttachmentDesc.Keep(p.Frame),
            p.Depth,
            p.BoundsPipeline,
            p.TransmittancePipeline,
            p.AccumulatePipeline,
            p.CompositePipeline,
            p.ItemAtlas,
            p.DrawItems);
    }
}
