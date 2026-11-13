// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Brew.Wgpu;
using Brew.Wgpu.Native;

namespace Sharpcraft.Framework.Rendering;

/// <summary>
/// Pipeline factories the entity pass needs.
/// </summary>
/// <remarks>
/// They live here rather than inside one renderer because the offscreen path has to draw the same entities the
/// window does - it drew none at all until now - and two copies of a pipeline description is exactly the kind of
/// divergence the single track rule exists to prevent (T-843).
/// </remarks>
internal static class FramePipelines
{
    internal static PipelineHandle CreateEntityPipeline(WgpuRenderBackend backend, WGPUTextureFormat format, PipelineLayoutHandle layout)
    {
        var source = System.Text.Encoding.UTF8.GetBytes(OffscreenRenderer.ShaderSourceText);
        return backend.CreateRenderPipeline(new PipelineDesc(
            source, "vs_main", source, "fs_entity_translucent", layout,
            [new ColorTargetDesc(BackendConvert.FromWgpu(format), new BlendStateDesc(
                new BlendComponentDesc(BlendOperation.Add, BlendFactor.SrcAlpha, BlendFactor.OneMinusSrcAlpha),
                new BlendComponentDesc(BlendOperation.Add, BlendFactor.One, BlendFactor.OneMinusSrcAlpha)))],
            new DepthStateDesc(TextureFormat.Depth32Float, CompareFunction.GreaterEqual, true),
            CullMode.None, FrontFace.Ccw,
            (ulong)VertexLayout.Stride, VertexStepMode.Vertex,
            LayoutAttributes.From(VertexLayout.Attributes())));
    }

    /// <summary>
    /// Entity base/clothing layer: reference entityCutout semantics, alpha tested at 0.5 and opaque (docs/decisions/0021).
    /// </summary>
    /// <summary>
    /// Item model layer: the reference <c>item_cutout</c> pipeline, which alpha tests at 0.1 (ALPHA_CUTOUT) instead of the
    /// 0.5 the entity cutout pass uses. No blending, depth writes on, no culling (ITEM_SNIPPET leaves cullMode at none).
    /// </summary>
    internal static PipelineHandle CreateItemCutoutPipeline(WgpuRenderBackend backend, WGPUTextureFormat format, PipelineLayoutHandle layout)
    {
        var source = System.Text.Encoding.UTF8.GetBytes(OffscreenRenderer.ShaderSourceText);
        return backend.CreateRenderPipeline(new PipelineDesc(
            source, "vs_main", source, "fs_item_cutout", layout,
            [new ColorTargetDesc(BackendConvert.FromWgpu(format))],
            new DepthStateDesc(TextureFormat.Depth32Float, CompareFunction.GreaterEqual, true),
            CullMode.None, FrontFace.Ccw,
            (ulong)VertexLayout.Stride, VertexStepMode.Vertex,
            LayoutAttributes.From(VertexLayout.Attributes())));
    }
    internal static PipelineHandle CreateEntityCutoutPipeline(WgpuRenderBackend backend, WGPUTextureFormat format, PipelineLayoutHandle layout)
    {
        var source = System.Text.Encoding.UTF8.GetBytes(OffscreenRenderer.ShaderSourceText);
        return backend.CreateRenderPipeline(new PipelineDesc(
            source, "vs_main", source, "fs_entity_cutout", layout,
            [new ColorTargetDesc(BackendConvert.FromWgpu(format))],
            new DepthStateDesc(TextureFormat.Depth32Float, CompareFunction.GreaterEqual, true),
            CullMode.None, FrontFace.Ccw,
            (ulong)VertexLayout.Stride, VertexStepMode.Vertex,
            LayoutAttributes.From(VertexLayout.Attributes())));
    }

    /// <summary>
    /// Entity equipment layer (armor/elytra): reference <c>armor_cutout_no_cull</c> pipeline, whose depth state is
    /// <c>DepthStencilState.DEFAULT = (GREATER_THAN_OR_EQUAL, writeDepth=true, depthBias=0)</c> over the reference's
    /// reversed-Z buffer. Our buffer is also reversed-Z, so the compare is <c>GREATER_EQUAL</c> with depth write and no
    /// bias. No culling (armor_cutout_no_cull is <c>withCull(false)</c>). Submitted as its own draw after the base model.
    /// </summary>
    internal static PipelineHandle CreateEntityElytraPipeline(WgpuRenderBackend backend, WGPUTextureFormat format, PipelineLayoutHandle layout)
    {
        var source = System.Text.Encoding.UTF8.GetBytes(OffscreenRenderer.ShaderSourceText);
        return backend.CreateRenderPipeline(new PipelineDesc(
            source, "vs_main", source, "fs_entity_cutout", layout,
            [new ColorTargetDesc(BackendConvert.FromWgpu(format))],
            new DepthStateDesc(TextureFormat.Depth32Float, CompareFunction.GreaterEqual, true),
            CullMode.None, FrontFace.Ccw,
            (ulong)VertexLayout.Stride, VertexStepMode.Vertex,
            LayoutAttributes.From(VertexLayout.Attributes())));
    }

    /// <summary>
    /// Entity eyes layer: reference EYES pipeline — emissive colour, translucent blend, depth GREATER_THAN_OR_EQUAL and no
    /// depth writes (docs/decisions/0021).
    /// </summary>
    internal static PipelineHandle CreateEntityEmissivePipeline(WgpuRenderBackend backend, WGPUTextureFormat format, PipelineLayoutHandle layout)
    {
        var source = System.Text.Encoding.UTF8.GetBytes(OffscreenRenderer.ShaderSourceText);
        return backend.CreateRenderPipeline(new PipelineDesc(
            source, "vs_main", source, "fs_entity_emissive", layout,
            [new ColorTargetDesc(BackendConvert.FromWgpu(format), new BlendStateDesc(
                new BlendComponentDesc(BlendOperation.Add, BlendFactor.SrcAlpha, BlendFactor.OneMinusSrcAlpha),
                new BlendComponentDesc(BlendOperation.Add, BlendFactor.One, BlendFactor.OneMinusSrcAlpha)))],
            new DepthStateDesc(TextureFormat.Depth32Float, CompareFunction.GreaterEqual, false),
            CullMode.None, FrontFace.Ccw,
            (ulong)VertexLayout.Stride, VertexStepMode.Vertex,
            LayoutAttributes.From(VertexLayout.Attributes())));
    }

    /// <summary>Enchanted glint overlay: additive, depth test greater-or-equal and no depth writes.</summary>
    internal static PipelineHandle CreateEntityGlintPipeline(WgpuRenderBackend backend, WGPUTextureFormat format, PipelineLayoutHandle layout)
    {
        var source = System.Text.Encoding.UTF8.GetBytes(OffscreenRenderer.ShaderSourceText);
        return backend.CreateRenderPipeline(new PipelineDesc(
            source, "vs_main", source, "fs_entity_glint", layout,
            [new ColorTargetDesc(BackendConvert.FromWgpu(format), new BlendStateDesc(
                new BlendComponentDesc(BlendOperation.Add, BlendFactor.Src, BlendFactor.One),
                new BlendComponentDesc(BlendOperation.Add, BlendFactor.Zero, BlendFactor.One)))],
            new DepthStateDesc(TextureFormat.Depth32Float, CompareFunction.Equal, false),
            CullMode.None, FrontFace.Ccw,
            (ulong)VertexLayout.Stride, VertexStepMode.Vertex,
            LayoutAttributes.From(VertexLayout.Attributes())));
    }

}
