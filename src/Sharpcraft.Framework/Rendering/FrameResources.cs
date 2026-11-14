// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Brew.Wgpu;
using Brew.Wgpu.Native;

namespace Sharpcraft.Framework.Rendering;

/// <summary>
/// Owns the render pipelines, bind group layouts and cloud / OIT resources one frame is recorded with.
/// </summary>
/// <remarks>
/// Extracted from <c>WindowedRenderer.Run</c> (T-843 2B-1) so the offscreen shell can build the same pipelines
/// instead of keeping a second set of descriptions. The descriptions themselves live in <see cref="FramePipelines"/>;
/// this class only decides which pipelines a frame needs and owns their handles. Creation and destruction order are
/// the ones the windowed shell used, so this slice is a pure move with no change to rendering behaviour.
/// </remarks>
internal sealed class FrameResources : IDisposable
{
    private readonly WgpuRenderBackend _backend;
    private bool _disposed;

    /// <summary>Builds every pipeline and layout for the given colour format.</summary>
    /// <param name="backend">Backend that owns the handles.</param>
    /// <param name="request">Frame whose celestial and atlas data decide which optional resources exist.</param>
    /// <param name="format">Colour target format of every pipeline.</param>
    /// <param name="pipelineLayout">Shared world pipeline layout.</param>
    /// <param name="bindGroupLayout">Shared world bind group layout.</param>
    /// <param name="samplers">Sampler cache the blit and atlas samplers resolve through.</param>
    /// <param name="cameraBuffer">Camera uniform the cloud resources bind.</param>
    /// <param name="supportsFloat32Blendable">Whether the adapter offers RGBA32F blending.</param>
    /// <param name="oitLowPrecision">Host asked for 8-bit OIT targets.</param>
    /// <param name="cloudOitEnabled">Host asked for cloud order independent transparency.</param>
    /// <param name="cloudsPerFrame">Per-frame cloud draw callback, or null when clouds are disabled.</param>
    public FrameResources(
        WgpuRenderBackend backend,
        FrameRequest request,
        WGPUTextureFormat format,
        PipelineLayoutHandle pipelineLayout,
        BindGroupLayoutHandle bindGroupLayout,
        SamplerCache samplers,
        BufferHandle cameraBuffer,
        bool supportsFloat32Blendable,
        bool oitLowPrecision,
        bool cloudOitEnabled,
        Func<CelestialColorDraw?>? cloudsPerFrame)
    {
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));

        // Clouds change every frame (scroll/camera), so their position-colour draw set is updated per frame.
        CloudColour = cloudsPerFrame is null
            ? null
            : new OffscreenRenderer.CelestialColorResources(backend, request with { CelestialColorDraws = [] }, cameraBuffer, VertexLayout.CameraUniformSize, BackendConvert.FromWgpu(format), WGPUCompareFunction.GreaterEqual);
        CloudOit = cloudOitEnabled && CloudColour is not null
            ? new CloudOitResources(backend, cameraBuffer, VertexLayout.CameraUniformSize)
            : null;

        TerrainPipeline = FramePipelines.CreatePipeline(backend, format, pipelineLayout);
        CutoutPipeline = FramePipelines.CreateCutoutPipeline(backend, format, pipelineLayout);
        EntityPipeline = FramePipelines.CreateEntityPipeline(backend, format, pipelineLayout);
        EntityCutoutPipeline = FramePipelines.CreateEntityCutoutPipeline(backend, format, pipelineLayout);
        ItemCutoutPipeline = FramePipelines.CreateItemCutoutPipeline(backend, format, pipelineLayout);
        EntityElytraPipeline = FramePipelines.CreateEntityElytraPipeline(backend, format, pipelineLayout);
        EntityEmissivePipeline = FramePipelines.CreateEntityEmissivePipeline(backend, format, pipelineLayout);
        EntityGlintPipeline = FramePipelines.CreateEntityGlintPipeline(backend, format, pipelineLayout);
        SkyPipeline = FramePipelines.CreateSkyPipeline(backend, format);
        UiPipeline = UiRenderer.CreatePipeline(backend, BackendConvert.FromWgpu(format), pipelineLayout);
        UiTextPipeline = UiRenderer.CreateTextPipeline(backend, BackendConvert.FromWgpu(format), pipelineLayout);
        UiSpritePipeline = UiRenderer.CreateSpritePipeline(backend, BackendConvert.FromWgpu(format), pipelineLayout);

        // Transparency is the reference depth-bounds + wavelet-transmittance OIT (docs/specs/render/oit-reference.md).
        // The earlier weighted-blended OIT looked markedly worse in play and is shelved, not compiled; git history keeps
        // it if it is ever wanted again.
        OitTargetFormat = oitLowPrecision ? TextureFormat.Rgba8Unorm : (supportsFloat32Blendable ? TextureFormat.Rgba32Float : TextureFormat.Rgba16Float);
        OitColorFormat = oitLowPrecision ? TextureFormat.Rgba8Unorm : TextureFormat.Rgba16Float; // half floats for transmittance and accumulate, as the reference does
        RefTransmittanceLayout = FramePipelines.CreateOitTextureLayout(backend, 1);
        RefAccumulateLayout = FramePipelines.CreateOitTextureLayout(backend, 3);
        RefCullLayout = FramePipelines.CreateOitTextureLayout(backend, 1);
        RefCompositeLayout = FramePipelines.CreateOitTextureLayout(backend, 4);
        // The four layouts are released by hand when the frame loop exits: a handle is pure identity, not a disposable.
        RefTransmittancePipelineLayout = backend.CreatePipelineLayout([bindGroupLayout, RefTransmittanceLayout]);
        RefAccumulatePipelineLayout = backend.CreatePipelineLayout([bindGroupLayout, RefAccumulateLayout]);
        RefCullPipelineLayout = backend.CreatePipelineLayout([RefCullLayout]);
        RefCompositePipelineLayout = backend.CreatePipelineLayout([RefCompositeLayout]);

        // The cloud layouts are created through the seam and released as soon as their pipelines exist: a pipeline
        // keeps what it needs from its layout, so nothing outlives this block. Destroying rather than waiting for the
        // method to end is what lets these stay handles instead of needing a Dispose on the handle itself.
        var cloudDbPipelineLayout = CloudOit is null ? default : backend.CreatePipelineLayout([CloudOit.Layout]);
        CloudDepthBoundsPipeline = CloudOit is null ? default : FramePipelines.CreateDepthBoundsPipeline(backend, cloudDbPipelineLayout, "cloud_oit", positionColor: true, format: BackendConvert.ToWgpu(OitTargetFormat), depthWrite: true);
        if (cloudDbPipelineLayout.IsValid)
        {
            backend.Destroy(cloudDbPipelineLayout);
        }

        var cloudTransPipelineLayout = CloudOit is null ? default : backend.CreatePipelineLayout([CloudOit.Layout, RefTransmittanceLayout]);
        CloudTransmittancePipeline = CloudOit is null ? default : FramePipelines.CreateTransmittancePipeline(backend, cloudTransPipelineLayout, "cloud_oit", positionColor: true, format: BackendConvert.ToWgpu(OitColorFormat));
        if (cloudTransPipelineLayout.IsValid)
        {
            backend.Destroy(cloudTransPipelineLayout);
        }

        var cloudAccumPipelineLayout = CloudOit is null ? default : backend.CreatePipelineLayout([CloudOit.Layout, RefAccumulateLayout]);
        CloudAccumulatePipeline = CloudOit is null ? default : FramePipelines.CreateAccumulatePipeline(backend, cloudAccumPipelineLayout, "cloud_oit", positionColor: true, format: BackendConvert.ToWgpu(OitColorFormat));
        if (cloudAccumPipelineLayout.IsValid)
        {
            backend.Destroy(cloudAccumPipelineLayout);
        }

        RefDepthBoundsPipeline = FramePipelines.CreateDepthBoundsPipeline(backend, pipelineLayout, format: BackendConvert.ToWgpu(OitTargetFormat));
        RefTransmittancePipeline = FramePipelines.CreateTransmittancePipeline(backend, RefTransmittancePipelineLayout, format: BackendConvert.ToWgpu(OitColorFormat));
        RefAccumulatePipeline = FramePipelines.CreateAccumulatePipeline(backend, RefAccumulatePipelineLayout, format: BackendConvert.ToWgpu(OitColorFormat));
        RefCullPipeline = FramePipelines.CreateCullPipeline(backend, RefCullPipelineLayout, BackendConvert.ToWgpu(OitTargetFormat));
        RefCompositePipeline = FramePipelines.CreateCompositePipeline(backend, format, RefCompositePipelineLayout);
        CloudCompositePipeline = FramePipelines.CreateCompositePipeline(backend, format, RefCompositePipelineLayout, WGPUCompareFunction.GreaterEqual);
        BlitDepthLayout = backend.CreateBindGroupLayout([
            BindGroupLayoutEntryDesc.Texture(0, ShaderStages.Fragment) with { SampleType = TextureSampleType.Depth, ViewDimension = TextureViewDimension.D2 },
            BindGroupLayoutEntryDesc.Sampler(1, ShaderStages.Fragment)
        ]);
        BlitDepthPipelineLayout = backend.CreatePipelineLayout([BlitDepthLayout]);
        BlitDepthPipeline = FramePipelines.CreateBlitDepthPipeline(backend, BlitDepthPipelineLayout);
        BlitDepthSampler = samplers.GetClampToEdge(FilterMode.Nearest);
    }

    /// <summary>Colour-only cloud draw resources, or null when clouds are off.</summary>
    public OffscreenRenderer.CelestialColorResources? CloudColour { get; }

    /// <summary>Cloud OIT resources, or null when cloud OIT is off.</summary>
    public CloudOitResources? CloudOit { get; }

    /// <summary>Main terrain pipeline.</summary>
    public PipelineHandle TerrainPipeline { get; }

    /// <summary>Terrain cutout pipeline.</summary>
    public PipelineHandle CutoutPipeline { get; }

    /// <summary>Opaque and translucent entity pipeline.</summary>
    public PipelineHandle EntityPipeline { get; }

    /// <summary>Entity cutout pipeline.</summary>
    public PipelineHandle EntityCutoutPipeline { get; }

    /// <summary>Item cutout pipeline.</summary>
    public PipelineHandle ItemCutoutPipeline { get; }

    /// <summary>Entity elytra pipeline.</summary>
    public PipelineHandle EntityElytraPipeline { get; }

    /// <summary>Entity emissive pipeline.</summary>
    public PipelineHandle EntityEmissivePipeline { get; }

    /// <summary>Entity glint pipeline.</summary>
    public PipelineHandle EntityGlintPipeline { get; }

    /// <summary>Gradient backdrop pipeline.</summary>
    public PipelineHandle SkyPipeline { get; }

    /// <summary>Interface pipeline.</summary>
    public PipelineHandle UiPipeline { get; }

    /// <summary>Interface text pipeline.</summary>
    public PipelineHandle UiTextPipeline { get; }

    /// <summary>Interface sprite pipeline.</summary>
    public PipelineHandle UiSpritePipeline { get; }

    /// <summary>Format of the depth-bounds OIT targets.</summary>
    public TextureFormat OitTargetFormat { get; }

    /// <summary>Format of the transmittance and accumulation OIT targets.</summary>
    public TextureFormat OitColorFormat { get; }

    /// <summary>Transmittance bind group layout.</summary>
    public BindGroupLayoutHandle RefTransmittanceLayout { get; }

    /// <summary>Accumulate bind group layout.</summary>
    public BindGroupLayoutHandle RefAccumulateLayout { get; }

    /// <summary>Depth-bounds cull bind group layout.</summary>
    public BindGroupLayoutHandle RefCullLayout { get; }

    /// <summary>Composite bind group layout.</summary>
    public BindGroupLayoutHandle RefCompositeLayout { get; }

    /// <summary>Transmittance pipeline layout.</summary>
    public PipelineLayoutHandle RefTransmittancePipelineLayout { get; }

    /// <summary>Accumulate pipeline layout.</summary>
    public PipelineLayoutHandle RefAccumulatePipelineLayout { get; }

    /// <summary>Cull pipeline layout.</summary>
    public PipelineLayoutHandle RefCullPipelineLayout { get; }

    /// <summary>Composite pipeline layout.</summary>
    public PipelineLayoutHandle RefCompositePipelineLayout { get; }

    /// <summary>Cloud depth-bounds pipeline, or default when cloud OIT is off.</summary>
    public PipelineHandle CloudDepthBoundsPipeline { get; }

    /// <summary>Cloud transmittance pipeline, or default when cloud OIT is off.</summary>
    public PipelineHandle CloudTransmittancePipeline { get; }

    /// <summary>Cloud accumulate pipeline, or default when cloud OIT is off.</summary>
    public PipelineHandle CloudAccumulatePipeline { get; }

    /// <summary>Terrain depth-bounds pipeline.</summary>
    public PipelineHandle RefDepthBoundsPipeline { get; }

    /// <summary>Terrain transmittance pipeline.</summary>
    public PipelineHandle RefTransmittancePipeline { get; }

    /// <summary>Terrain accumulate pipeline.</summary>
    public PipelineHandle RefAccumulatePipeline { get; }

    /// <summary>Depth-bounds cull pipeline.</summary>
    public PipelineHandle RefCullPipeline { get; }

    /// <summary>Terrain composite pipeline.</summary>
    public PipelineHandle RefCompositePipeline { get; }

    /// <summary>Cloud composite pipeline.</summary>
    public PipelineHandle CloudCompositePipeline { get; }

    /// <summary>Cloud depth blit bind group layout.</summary>
    public BindGroupLayoutHandle BlitDepthLayout { get; }

    /// <summary>Cloud depth blit pipeline layout.</summary>
    public PipelineLayoutHandle BlitDepthPipelineLayout { get; }

    /// <summary>Cloud depth blit pipeline.</summary>
    public PipelineHandle BlitDepthPipeline { get; }

    /// <summary>Sampler the cloud depth blit uses.</summary>
    public SamplerHandle BlitDepthSampler { get; }

    /// <summary>Releases every pipeline and layout this class owns, in the windowed shell's original order.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _backend.Destroy(UiSpritePipeline);
        _backend.Destroy(UiTextPipeline);
        _backend.Destroy(UiPipeline);
        _backend.Destroy(CloudCompositePipeline);
        _backend.Destroy(RefCompositePipeline);
        _backend.Destroy(RefCullPipeline);
        _backend.Destroy(RefAccumulatePipeline);
        _backend.Destroy(RefTransmittancePipeline);
        _backend.Destroy(RefDepthBoundsPipeline);
        if (CloudAccumulatePipeline.IsValid)
        {
            _backend.Destroy(CloudAccumulatePipeline);
        }

        if (CloudTransmittancePipeline.IsValid)
        {
            _backend.Destroy(CloudTransmittancePipeline);
        }

        if (CloudDepthBoundsPipeline.IsValid)
        {
            _backend.Destroy(CloudDepthBoundsPipeline);
        }

        _backend.Destroy(BlitDepthPipeline);
        _backend.Destroy(SkyPipeline);
        _backend.Destroy(EntityGlintPipeline);
        _backend.Destroy(EntityEmissivePipeline);
        _backend.Destroy(EntityElytraPipeline);
        _backend.Destroy(ItemCutoutPipeline);
        _backend.Destroy(EntityCutoutPipeline);
        _backend.Destroy(EntityPipeline);
        _backend.Destroy(CutoutPipeline);
        _backend.Destroy(TerrainPipeline);
        _backend.Destroy(BlitDepthLayout);
        _backend.Destroy(BlitDepthPipelineLayout);
        _backend.Destroy(RefCompositePipelineLayout);
        _backend.Destroy(RefCullPipelineLayout);
        _backend.Destroy(RefAccumulatePipelineLayout);
        _backend.Destroy(RefTransmittancePipelineLayout);
        _backend.Destroy(RefCompositeLayout);
        _backend.Destroy(RefCullLayout);
        _backend.Destroy(RefAccumulateLayout);
        _backend.Destroy(RefTransmittanceLayout);
        CloudOit?.Dispose();
        CloudColour?.Dispose();
    }
}
