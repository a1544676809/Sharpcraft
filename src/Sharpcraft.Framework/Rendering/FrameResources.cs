// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Brew.Wgpu;
using Brew.Wgpu.Native;
using System.Numerics;

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
    private FrameRequest _request = null!;
    private BindGroupHandle _bindGroup;
    private bool _disposed;

    private const ulong MinimumBufferBytes = 1 << 20;

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
    private FrameResources(
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

    /// <summary>
    /// Builds the whole frame resource set (T-843 2B-2a): the pipelines above plus the buffers, recorders, static
    /// world, OIT targets and the executor's parameter object. Pure move from <c>WindowedRenderer.Run</c>; the offscreen
    /// shell will construct the same object in 2B-2b.
    /// </summary>
    public FrameResources(Options options) : this(
        options.Backend,
        options.Request,
        options.Format,
        options.PipelineLayout,
        options.BindGroupLayout,
        options.Samplers,
        options.CameraBuffer,
        options.SupportsFloat32Blendable,
        options.OitLowPrecision,
        options.CloudOitEnabled,
        options.CloudsPerFrame)
    {
        _request = options.Request;
        _bindGroup = options.BindGroup;

        RetiredVertex = new List<BufferHandle>();
        RetiredIndex = new List<BufferHandle>();
        DeferredRelease = new DeferredBufferRelease(_backend.Destroy);
        UiBuffers = new GrowableGeometryBuffers(_backend, DeferredRelease, MinimumBufferBytes);
        DynamicBuffers = new GrowableGeometryBuffers(_backend, DeferredRelease, MinimumBufferBytes);
        UiRanges = new List<UiGeometry.Range>();
        LayerPool = new EntityLayerPool(_backend, DeferredRelease, MinimumBufferBytes);
        LayerPool.EnsureCapacity(4);
        StaticWorld = new StaticWorldBuffers(_backend, options.Request, MinimumBufferBytes, RetiredVertex, RetiredIndex);
        GeometryUploader = new FrameGeometryUploader(
            _backend, DynamicBuffers, LayerPool, options.Arena, options.ArenaCutout, options.ArenaTranslucent);
        TerrainRecorder = new TerrainPassRecorder(
            _backend, TerrainPipeline, CutoutPipeline, options.BindGroup, options.Arena, options.ArenaCutout);
        EntityRecorder = new EntityPassRecorder(
            _backend,
            EntityPipeline,
            CutoutPipeline,
            EntityCutoutPipeline,
            ItemCutoutPipeline,
            EntityElytraPipeline,
            EntityEmissivePipeline,
            EntityGlintPipeline,
            options.EntitySkin,
            options.ItemAtlasGpu,
            LayerPool,
            DynamicBuffers,
            options.BindGroup,
            StaticWorld);

        DepthTexture = FrameOitTargets.CreateDepthTexture(_backend, options.Width, options.Height);
        DepthView = _backend.CreateTextureView(DepthTexture);
        ItemOit = new ItemOitSet();
        OitTargets = new FrameOitTargets(
            _backend,
            OitTargetFormat,
            OitColorFormat,
            RefTransmittanceLayout,
            RefAccumulateLayout,
            RefCullLayout,
            RefCompositeLayout,
            BlitDepthLayout,
            BlitDepthSampler,
            ItemOit,
            CloudOit);

        Uniforms = new FrameUniformStaging();
        UniformWriter = new FrameUniformWriter(
            _backend, options.CameraBuffer, options.LightmapBuffer, options.FogBuffer, Uniforms);
        Cpu = new FrameCpuTimings();
        SkyRecorder = new SkyPassRecorder(
            _backend, SkyPipeline, options.Celestial, options.CelestialColour, options.SkyDiscColour,
            options.SkyDiscVanilla, options.CelestialPerFrame, options.SkyDiscPerFrame, Cpu);
        FrustumPlanes = new Vector4[6];

        ExecutorResources = new FrameExecutor.Resources
        {
            Backend = _backend,
            Request = options.Request,
            ClearColour = options.Request.ClearColour,
            ClearColourPerFrame = options.ClearColourPerFrame,
            LightmapPerFrame = options.LightmapPerFrame,
            FogPerFrame = options.FogPerFrame,
            FogRangePerFrame = options.FogRangePerFrame,
            WorldTextPerFrame = options.WorldTextPerFrame,
            CloudsPerFrame = options.CloudsPerFrame,
            VisibleSections = options.VisibleSections,
            Cpu = Cpu,
            TerrainPipeline = TerrainPipeline,
            UiPipeline = UiPipeline,
            UiTextPipeline = UiTextPipeline,
            UiSpritePipeline = UiSpritePipeline,
            BlitDepthPipeline = BlitDepthPipeline,
            RefDepthBoundsPipeline = RefDepthBoundsPipeline,
            RefCullPipeline = RefCullPipeline,
            RefTransmittancePipeline = RefTransmittancePipeline,
            RefAccumulatePipeline = RefAccumulatePipeline,
            RefCompositePipeline = RefCompositePipeline,
            CloudDepthBoundsPipeline = CloudDepthBoundsPipeline,
            CloudTransmittancePipeline = CloudTransmittancePipeline,
            CloudAccumulatePipeline = CloudAccumulatePipeline,
            TerrainRecorder = TerrainRecorder,
            EntityRecorder = EntityRecorder,
            SkyRecorder = SkyRecorder,
            UniformWriter = UniformWriter,
            UiBuffers = UiBuffers,
            LayerPool = LayerPool,
            SpriteBindGroups = options.SpriteBindGroups,
            UiRanges = UiRanges,
            FrustumPlanes = FrustumPlanes,
            WorldText = options.WorldText,
            CloudColour = CloudColour,
            CloudOit = CloudOit,
            ItemOit = ItemOit,
            ItemAtlas = options.ItemAtlasGpu,
            OitTargets = OitTargets
        };
    }

    /// <summary>Executor parameter object built from this resource set.</summary>
    public FrameExecutor.Resources ExecutorResources { get; } = null!;

    /// <summary>Buffers replaced by a geometry rebuild, waited on before shutdown.</summary>
    public List<BufferHandle> RetiredVertex { get; } = null!;

    /// <summary>Buffers replaced by a geometry rebuild, waited on before shutdown.</summary>
    public List<BufferHandle> RetiredIndex { get; } = null!;

    /// <summary>Defers buffer destruction until the frame that used them is done.</summary>
    public DeferredBufferRelease DeferredRelease { get; } = null!;

    /// <summary>Interface geometry buffers.</summary>
    public GrowableGeometryBuffers UiBuffers { get; } = null!;

    /// <summary>Dynamic entity geometry buffers.</summary>
    public GrowableGeometryBuffers DynamicBuffers { get; } = null!;

    /// <summary>Interface draw ranges.</summary>
    public List<UiGeometry.Range> UiRanges { get; } = null!;

    /// <summary>Entity layer buffers.</summary>
    public EntityLayerPool LayerPool { get; } = null!;

    /// <summary>Combined static geometry used when there is no arena.</summary>
    public StaticWorldBuffers StaticWorld { get; } = null!;

    /// <summary>Uploads geometry updates into the persistent buffers.</summary>
    public FrameGeometryUploader GeometryUploader { get; } = null!;

    /// <summary>Records the terrain draws.</summary>
    public TerrainPassRecorder TerrainRecorder { get; } = null!;

    /// <summary>Records the entity draws and the static world fallback.</summary>
    public EntityPassRecorder EntityRecorder { get; } = null!;

    /// <summary>Main depth texture, recreated on resize.</summary>
    public TextureHandle DepthTexture { get; private set; }

    /// <summary>View of <see cref="DepthTexture"/>, recreated on resize.</summary>
    public TextureViewHandle DepthView { get; private set; }

    /// <summary>Item transparency target set.</summary>
    public ItemOitSet ItemOit { get; } = null!;

    /// <summary>Executor-owned size-dependent OIT targets.</summary>
    public FrameOitTargets OitTargets { get; } = null!;

    /// <summary>Double buffered uniform staging bytes.</summary>
    public FrameUniformStaging Uniforms { get; } = null!;

    /// <summary>Writes the camera, lightmap and fog blocks.</summary>
    public FrameUniformWriter UniformWriter { get; } = null!;

    /// <summary>Stage timing sink.</summary>
    public FrameCpuTimings Cpu { get; } = null!;

    /// <summary>Records the sky and celestial bodies.</summary>
    public SkyPassRecorder SkyRecorder { get; } = null!;

    /// <summary>Scratch for the six frustum planes.</summary>
    public Vector4[] FrustumPlanes { get; } = null!;

    /// <summary>Builds a per-frame input with offscreen-friendly defaults: the request's camera, no UI, no entities.</summary>
    /// <remarks>
    /// The windowed shell keeps its explicit per-frame <see cref="FrameExecutor.Frame"/> because its camera, skip flags
    /// and arena change every frame. The offscreen shell has no such state and uses this method unchanged.
    /// </remarks>
    /// <param name="target">Colour/depth views and size of the frame target.</param>
    /// <returns>A frame input that records the request's geometry through the shared executor.</returns>
    public FrameExecutor.Frame CreateFrame(FrameTarget target) => new()
    {
        Target = target,
        Eye = _request.CameraPosition,
        CameraTarget = _request.CameraTarget,
        CameraStarted = System.Diagnostics.Stopwatch.GetTimestamp(),
        CurrentRequest = _request,
        BindGroup = _bindGroup,
        UiBindGroup = default,
        UiTextBindGroup = default,
        Arena = null,
        ArenaCutout = null,
        ArenaTranslucent = null,
        DynamicIndexCount = 0,
        EntityLayerDrawn = 0,
        UiScale = 1f,
        SkipClear = false,
        SkipSky = false,
        SkipCelestial = false,
        SkipTerrain = false,
        SkipEntities = false,
        SkipOit = false,
        SkipClouds = false,
        SkipTranslucent = false,
        SkipInterface = true,
        DisableCulling = false
    };

    /// <summary>Rebuilds the executor-owned OIT targets and the depth texture for a new framebuffer size.</summary>
    public void Resize(uint width, uint height)
    {
        OitTargets.Resize(width, height);
        _backend.Destroy(DepthView);
        _backend.Destroy(DepthTexture);
        DepthTexture = FrameOitTargets.CreateDepthTexture(_backend, width, height);
        DepthView = _backend.CreateTextureView(DepthTexture);
    }

    /// <summary>Everything the frame resources need from the shell; unset options mean "not present".</summary>
    public sealed class Options
    {
        /// <summary>Backend that owns every handle.</summary>
        public required WgpuRenderBackend Backend { get; init; }

        /// <summary>Frame being rendered.</summary>
        public required FrameRequest Request { get; init; }

        /// <summary>Colour target format of every pipeline.</summary>
        public required WGPUTextureFormat Format { get; init; }

        /// <summary>Shared world pipeline layout.</summary>
        public required PipelineLayoutHandle PipelineLayout { get; init; }

        /// <summary>Shared world bind group layout.</summary>
        public required BindGroupLayoutHandle BindGroupLayout { get; init; }

        /// <summary>Sampler cache.</summary>
        public required SamplerCache Samplers { get; init; }

        /// <summary>Camera uniform buffer.</summary>
        public required BufferHandle CameraBuffer { get; init; }

        /// <summary>Fog uniform buffer.</summary>
        public BufferHandle FogBuffer { get; init; }

        /// <summary>Lightmap uniform buffer.</summary>
        public BufferHandle LightmapBuffer { get; init; }

        /// <summary>World bind group.</summary>
        public BindGroupHandle BindGroup { get; init; }

        /// <summary>Entity skin atlas, or one that was never uploaded.</summary>
        public required EntitySkinTextureGpu EntitySkin { get; init; }

        /// <summary>Item atlas, or one that was never uploaded.</summary>
        public required EntitySkinTextureGpu ItemAtlasGpu { get; init; }

        /// <summary>Sprite bind groups for GUI sprites.</summary>
        public IReadOnlyList<BindGroupHandle> SpriteBindGroups { get; init; } = [];

        /// <summary>World text resources, or null when the host draws no text.</summary>
        public WorldTextResources? WorldText { get; init; }

        /// <summary>Textured sun and moon.</summary>
        public OffscreenRenderer.CelestialResources? Celestial { get; init; }

        /// <summary>Sunrise band and stars.</summary>
        public OffscreenRenderer.CelestialColorResources? CelestialColour { get; init; }

        /// <summary>Colour-only sky disc when the vanilla disc is off.</summary>
        public OffscreenRenderer.CelestialColorResources? SkyDiscColour { get; init; }

        /// <summary>Per-pixel vanilla sky disc.</summary>
        public SkyDiscResources? SkyDiscVanilla { get; init; }

        /// <summary>Opaque chunk arena, or null for the static world fallback.</summary>
        public ChunkGeometryStore? Arena { get; init; }

        /// <summary>Cutout chunk arena, or null.</summary>
        public ChunkGeometryStore? ArenaCutout { get; init; }

        /// <summary>Translucent chunk arena, or null.</summary>
        public ChunkGeometryStore? ArenaTranslucent { get; init; }

        /// <summary>Framebuffer width in pixels.</summary>
        public uint Width { get; init; }

        /// <summary>Framebuffer height in pixels.</summary>
        public uint Height { get; init; }

        /// <summary>Whether the adapter offers RGBA32F blending.</summary>
        public bool SupportsFloat32Blendable { get; init; }

        /// <summary>Host asked for 8-bit OIT targets.</summary>
        public bool OitLowPrecision { get; init; }

        /// <summary>Host asked for cloud OIT.</summary>
        public bool CloudOitEnabled { get; init; }

        /// <summary>Per-frame cloud draws.</summary>
        public Func<CelestialColorDraw?>? CloudsPerFrame { get; init; }

        /// <summary>Per-frame clear colour override.</summary>
        public Func<Vector4>? ClearColourPerFrame { get; init; }

        /// <summary>Per-frame lightmap override.</summary>
        public Func<LightmapUniform>? LightmapPerFrame { get; init; }

        /// <summary>Per-frame fog colour override.</summary>
        public Func<Vector4>? FogPerFrame { get; init; }

        /// <summary>Per-frame atmospheric fog range override.</summary>
        public Func<(float Start, float End)>? FogRangePerFrame { get; init; }

        /// <summary>Per-frame world text draws.</summary>
        public Func<IReadOnlyList<WorldTextDraw>?>? WorldTextPerFrame { get; init; }

        /// <summary>Per-section visibility predicate.</summary>
        public Func<long, ulong>? VisibleSections { get; init; }

        /// <summary>Per-frame celestial draw rebuild.</summary>
        public Func<(CelestialQuadDraw[] Draws, CelestialColorDraw[] Colors, CelestialColorDraw[] SkyDiscs)?>? CelestialPerFrame { get; init; }

        /// <summary>Per-frame vanilla sky disc state.</summary>
        public Func<(Vector3 Sky, Vector3 Fog, float SkyFogEnd, Vector3 Eye)>? SkyDiscPerFrame { get; init; }
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
        // The blit bind group samples the depth view, so the OIT targets and depth go before the pipelines.
        OitTargets.Dispose();
        _backend.Destroy(DepthView);
        _backend.Destroy(DepthTexture);
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
