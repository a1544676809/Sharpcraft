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

    /// <summary>Layout for a bind group of <paramref name="textureCount"/> sampled textures, fetched with texelFetch.</summary>
    /// <remarks>The targets are Rgba32Float by default, which is sampleable but not filterable, so the entries declare an
    /// unfilterable float binding.</remarks>
    internal static BindGroupLayoutHandle CreateOitTextureLayout(WgpuRenderBackend backend, int textureCount)
    {
        var entries = new BindGroupLayoutEntryDesc[textureCount];

        for (var index = 0; index < textureCount; index++)
        {
            entries[index] = BindGroupLayoutEntryDesc.Texture((uint)index, ShaderStages.Fragment)
                with { SampleType = TextureSampleType.UnfilterableFloat };
        }

        return backend.CreateBindGroupLayout(entries);
    }

    /// <summary>
    /// Reference transparency stage 1: record the per-pixel depth range of the translucent layer. RGBA32F with a MAX
    /// blend, so the closest and furthest bounds survive in different channels.
    /// </summary>
    internal static PipelineHandle CreateDepthBoundsPipeline(WgpuRenderBackend backend, PipelineLayoutHandle layout, string shaderName = "oit_reference", bool positionColor = false, WGPUTextureFormat format = WGPUTextureFormat.RGBA16Float, bool depthWrite = false)
    {
        var source = System.Text.Encoding.UTF8.GetBytes(Shaders.ShaderLibrary.Load(shaderName));
        return backend.CreateRenderPipeline(new PipelineDesc(
            source, "vs_main", source, "fs_depth_bounds", layout,
            [new ColorTargetDesc(BackendConvert.FromWgpu(format), new BlendStateDesc(
                new BlendComponentDesc(BlendOperation.Max, BlendFactor.One, BlendFactor.One),
                new BlendComponentDesc(BlendOperation.Max, BlendFactor.One, BlendFactor.One)))],
            new DepthStateDesc(TextureFormat.Depth32Float, CompareFunction.GreaterEqual, depthWrite),
            CullMode.Back, FrontFace.Ccw,
            positionColor ? 0UL : (ulong)VertexLayout.Stride, VertexStepMode.Vertex,
            positionColor ? [] : LayoutAttributes.From(VertexLayout.Attributes())));
    }

    /// <summary>Reference transparency stage 2: wavelet-transmittance coefficients, additively blended into two targets.</summary>
    internal static PipelineHandle CreateTransmittancePipeline(WgpuRenderBackend backend, PipelineLayoutHandle layout, string shaderName = "oit_reference", bool positionColor = false, WGPUTextureFormat format = WGPUTextureFormat.RGBA16Float, bool depthWrite = false)
    {
        var source = System.Text.Encoding.UTF8.GetBytes(Shaders.ShaderLibrary.Load(shaderName));
        static ColorTargetDesc Additive(WGPUTextureFormat format) => new(
            BackendConvert.FromWgpu(format), new BlendStateDesc(
                new BlendComponentDesc(BlendOperation.Add, BlendFactor.One, BlendFactor.One),
                new BlendComponentDesc(BlendOperation.Add, BlendFactor.One, BlendFactor.One)));
        return backend.CreateRenderPipeline(new PipelineDesc(
            source, "vs_main", source, "fs_transmittance", layout,
            [Additive(format), Additive(format)],
            new DepthStateDesc(TextureFormat.Depth32Float, CompareFunction.GreaterEqual, depthWrite),
            CullMode.Back, FrontFace.Ccw,
            positionColor ? 0UL : (ulong)VertexLayout.Stride, VertexStepMode.Vertex,
            positionColor ? [] : LayoutAttributes.From(VertexLayout.Attributes())));
    }

    /// <summary>Reference transparency stage 3: colour weighted by the transmittance in front of each fragment.</summary>
    internal static PipelineHandle CreateAccumulatePipeline(WgpuRenderBackend backend, PipelineLayoutHandle layout, string shaderName = "oit_reference", bool positionColor = false, WGPUTextureFormat format = WGPUTextureFormat.RGBA16Float, bool depthWrite = false)
    {
        var source = System.Text.Encoding.UTF8.GetBytes(Shaders.ShaderLibrary.Load(shaderName));
        return backend.CreateRenderPipeline(new PipelineDesc(
            source, "vs_main", source, "fs_accumulate", layout,
            [new ColorTargetDesc(BackendConvert.FromWgpu(format), new BlendStateDesc(
                new BlendComponentDesc(BlendOperation.Add, BlendFactor.One, BlendFactor.One),
                new BlendComponentDesc(BlendOperation.Add, BlendFactor.One, BlendFactor.One)))],
            new DepthStateDesc(TextureFormat.Depth32Float, CompareFunction.GreaterEqual, depthWrite),
            CullMode.Back, FrontFace.Ccw,
            positionColor ? 0UL : (ulong)VertexLayout.Stride, VertexStepMode.Vertex,
            positionColor ? [] : LayoutAttributes.From(VertexLayout.Attributes())));
    }

    /// <summary>Reference transparency: clip the far depth bound to the nearest opaque surface.</summary>
    internal static PipelineHandle CreateCullPipeline(WgpuRenderBackend backend, PipelineLayoutHandle layout, WGPUTextureFormat format = WGPUTextureFormat.RGBA16Float)
    {
        var source = System.Text.Encoding.UTF8.GetBytes(Shaders.ShaderLibrary.Load("oit_depth_bounds_cull"));
        return backend.CreateRenderPipeline(new PipelineDesc(
            source, "vs_fullscreen", source, "fs_cull", layout,
            [new ColorTargetDesc(BackendConvert.FromWgpu(format))],
            new DepthStateDesc(TextureFormat.Depth32Float, CompareFunction.Always, false),
            CullMode.None, FrontFace.Ccw,
            0, VertexStepMode.Vertex,
            []));
    }

    /// <summary>Reference transparency composite: premultiplied over the frame, writing the nearest translucent depth.</summary>
    internal static PipelineHandle CreateBlitDepthPipeline(WgpuRenderBackend backend, PipelineLayoutHandle layout)
    {
        var source = System.Text.Encoding.UTF8.GetBytes(Shaders.ShaderLibrary.Load("blit_depth"));
        return backend.CreateRenderPipeline(new PipelineDesc(
            source, "vs_fullscreen", source, "fs_blit", layout,
            [],
            new DepthStateDesc(TextureFormat.Depth32Float, CompareFunction.Always, true),
            CullMode.None, FrontFace.Ccw,
            0, VertexStepMode.Vertex,
            []));
    }

    internal static PipelineHandle CreateCompositePipeline(WgpuRenderBackend backend, WGPUTextureFormat format, PipelineLayoutHandle layout, WGPUCompareFunction depthCompare = WGPUCompareFunction.Always)
    {
        var source = System.Text.Encoding.UTF8.GetBytes(Shaders.ShaderLibrary.Load("oit_composite"));
        return backend.CreateRenderPipeline(new PipelineDesc(
            source, "vs_fullscreen", source, "fs_composite", layout,
            [new ColorTargetDesc(BackendConvert.FromWgpu(format), new BlendStateDesc(
                new BlendComponentDesc(BlendOperation.Add, BlendFactor.One, BlendFactor.OneMinusSrcAlpha),
                new BlendComponentDesc(BlendOperation.Add, BlendFactor.One, BlendFactor.OneMinusSrcAlpha)))],
            new DepthStateDesc(TextureFormat.Depth32Float, BackendConvert.FromWgpu(depthCompare), true),
            CullMode.None, FrontFace.Ccw,
            0, VertexStepMode.Vertex,
            []));
    }

    /// <summary>Builds the sky pipeline: a full-screen triangle with no vertex buffer and no depth writes.</summary>
    /// <param name="device">Device.</param>
    /// <param name="format">Colour target format.</param>
    internal static PipelineHandle CreateSkyPipeline(WgpuRenderBackend backend, WGPUTextureFormat format)
    {
        var skySource = System.Text.Encoding.UTF8.GetBytes(OffscreenRenderer.SkyShaderSourceText);
        return backend.CreateRenderPipeline(new PipelineDesc(
            skySource, "vs_sky", skySource, "fs_sky", null,
            [new ColorTargetDesc(BackendConvert.FromWgpu(format))],
            new DepthStateDesc(TextureFormat.Depth32Float, CompareFunction.Always, false),
            CullMode.None, FrontFace.Ccw,
            0, VertexStepMode.Vertex,
            []));
    }

    /// <summary>
    /// The cutout pipeline: alpha testing instead of blending, depth writes on.
    /// </summary>
    internal static PipelineHandle CreateCutoutPipeline(WgpuRenderBackend backend, WGPUTextureFormat format, PipelineLayoutHandle layout)
    {
        var source = System.Text.Encoding.UTF8.GetBytes(OffscreenRenderer.ShaderSourceText);
        return backend.CreateRenderPipeline(new PipelineDesc(
            source, "vs_main", source, "fs_cutout", layout,
            [new ColorTargetDesc(BackendConvert.FromWgpu(format))],
            new DepthStateDesc(TextureFormat.Depth32Float, CompareFunction.GreaterEqual, true),
            CullMode.Back, FrontFace.Ccw,
            (ulong)VertexLayout.Stride, VertexStepMode.Vertex,
            LayoutAttributes.From(VertexLayout.Attributes())));
    }

    /// <summary>
    /// Entity models follow the reference entityCutout pipeline: alpha testing and no back-face culling.
    /// </summary>
    internal static PipelineHandle CreatePipeline(WgpuRenderBackend backend, WGPUTextureFormat format, PipelineLayoutHandle layout)
    {
        var source = System.Text.Encoding.UTF8.GetBytes(OffscreenRenderer.ShaderSourceText);
        return backend.CreateRenderPipeline(new PipelineDesc(
            source, "vs_main", source, "fs_main", layout,
            [new ColorTargetDesc(BackendConvert.FromWgpu(format))],
            new DepthStateDesc(TextureFormat.Depth32Float, CompareFunction.GreaterEqual, true),
            CullMode.Back, FrontFace.Ccw,
            (ulong)VertexLayout.Stride, VertexStepMode.Vertex,
            LayoutAttributes.From(VertexLayout.Attributes())));
    }
}
