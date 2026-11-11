// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Brew.Wgpu;
using Brew.Wgpu.Native;
using WgpuBuffer = Brew.Wgpu.Buffer;
using Sharpcraft.Framework.Platform;

namespace Sharpcraft.Framework.Rendering;

/// <summary>
/// Opens a real window and presents rendered frames to its swapchain
/// (docs/decisions/0004-window-layer.md).
/// </summary>
/// <remarks>
/// This is the windowed counterpart of <see cref="OffscreenRenderer"/>: same pipeline, same
/// geometry, but the colour target is the surface texture instead of an offscreen one. Offscreen
/// rendering stays the path used for screenshots, because swapchain textures are not guaranteed to
/// be copyable.
/// <para>
/// Everything that depends on the framebuffer size — the swapchain configuration, the depth
/// texture and the clip-space vertices (they bake in the aspect ratio) — is rebuilt whenever the
/// framebuffer size changes, so DPI scaling and window resizing stay correct.
/// </para>
/// </remarks>
public static class WindowedRenderer
{
    /// <summary>Shared canonical pass plan; the windowed shell must keep this order when extracting the pipeline.</summary>
    public static IReadOnlyList<WorldRenderPass> PassPlan => WorldRenderPipeline.DefaultPasses;

    /// <summary>Runs a window showing the supplied geometry.</summary>
    /// <param name="request">Frame request; width and height are overridden by the window size.</param>
    /// <param name="frames">How many frames to present before closing (0 means until the user closes it).</param>
    /// <param name="maxDuration">Optional wall-clock limit, so an unthrottled window cannot flash by.</param>
    /// <param name="onFrame">
    /// Optional callback invoked after each presented frame with the frame index and the window,
    /// which lets callers drive the window (for example to exercise resizing).
    /// </param>
    /// <returns>The surface format that was used, for diagnostics.</returns>
    public static WGPUTextureFormat Run(
        FrameRequest request,
        int frames,
        TimeSpan? maxDuration = null,
        Action<int, WindowHost>? onFrame = null,
        Func<WindowHost, (Vector3 Eye, Vector3 Target)>? cameraPerFrame = null,
        Func<FrameRequest?>? rebuildRequest = null,
        Func<IReadOnlyList<(long Key, GeometryData Mesh)>?>? geometryUpdates = null,
        Func<IReadOnlyList<long>?>? geometryRemovals = null,
        int expectedChunks = 0,
        Func<DynamicGeometry?>? dynamicGeometry = null,
        Func<IReadOnlyList<EntityGeometryLayer>?>? entityLayers = null,
        Func<UiFrame, UiGeometry?>? uiGeometry = null,
        AtlasTextureSource? itemAtlasSource = null,
    Func<IReadOnlyList<WorldTextDraw>?>? worldTextPerFrame = null,
        TextureData? glyphTexture = null,
    AtlasTextureSource? entitySkinSource = null,
        IReadOnlyList<TextureData>? spriteTextures = null,
        UiTextureSource? textureSource = null,
        AtlasTextureSource? atlasSource = null,
        Func<int>? geometryEpoch = null,
    FrameDiagnostics? diagnostics = null,
    Func<LightmapUniform>? lightmapPerFrame = null,
    Func<Vector4>? fogPerFrame = null,
    Func<Vector4>? clearColourPerFrame = null,
    Func<CelestialColorDraw?>? cloudsPerFrame = null,
    Func<(CelestialQuadDraw[] Draws, CelestialColorDraw[] Colors, CelestialColorDraw[] SkyDiscs)?>? celestialPerFrame = null,
    bool cloudOitEnabled = false,
    bool oitLowPrecision = false,
    bool cloudDepthEnabled = false,
    bool skyDiscVanilla = true,
    Func<(Vector3 Sky, Vector3 Fog, float SkyFogEnd, Vector3 Eye)>? skyDiscPerFrame = null,
    WGPUPresentMode presentMode = WGPUPresentMode.Fifo,
    Func<WindowHost, string?>? screenshotRequest = null,
    Action<string, byte[], uint, uint>? screenshotCaptured = null)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var window = WindowHost.Create("Sharpcraft", request.Width, request.Height);

        var instance = Instance.Create();
        var source = window.GetSurfaceSource();
        // Surface, adapter and device are deliberately not disposed. Dropping them destroys the swapchain, and wgpu-hal
        // panics when a swapchain acquire semaphore is still referenced by the last presented texture ("Trying to destroy
        // a SwapchainAcquireSemaphore that is still in use"). The process is exiting anyway, so the driver reclaims these
        // when it goes; leaking them on the way out is cheaper than an abort on the way out.
        var surface = instance.CreateSurface(ToSurfaceSource(source));
        var adapter = NativeAdapterSelection.Resolve(instance, surface, request.Diagnostics?.Gpu);
        // Ask for the limits this adapter actually offers rather than the defaults, whose maxBufferSize is 256 MiB. A full
        // amplified view needs more than that for one geometry buffer, and the adapter's own figure is the honest ceiling.
        var deviceLimits = adapter.GetLimits();
        var supportsFloat32Blendable = adapter.HasFeature(WGPUFeatureName.Float32Blendable);
        WGPUFeatureName[] requiredFeatures = supportsFloat32Blendable ? [WGPUFeatureName.Float32Blendable] : [];
        var deviceDescriptor = new DeviceDescriptor
        {
            RequiredLimits = deviceLimits,
            RequiredFeatures = requiredFeatures
        };
        var device = adapter.RequestDeviceBlocking(in deviceDescriptor);

        Console.WriteLine($"window: surface ready on {adapter.GetInfo().Description}");
        // A non-sRGB surface blends in gamma space, which is what the reference does
        // (docs/decisions/0013-gamma-space-ui-blending.md). Using the preferred sRGB format made every translucent
        // overlay lighter than the reference.
        var format = WGPUTextureFormat.BGRA8Unorm;

        // Atlas resources: the fragment shader samples it, so the pipeline layout and the bind group
        // have to exist before anything is drawn (docs/modules/render.md 6.1).
        var backend = new WgpuRenderBackend(device);
        var wgpu = backend;
        var atlasData = request.Atlas ?? TextureData.White;

        var atlasMips = atlasData.Mips ?? [];
        var atlasTexture = backend.CreateTexture(new TextureDesc(
            (uint)atlasData.Width,
            (uint)atlasData.Height,
            TextureFormat.Rgba8Unorm,
            1 + (uint)atlasMips.Length,
            TextureUsages.TextureBinding | TextureUsages.CopyDst));

        backend.WriteTexture(
            atlasTexture,
            atlasData.Rgba,
            (uint)(atlasData.Width * 4),
            (uint)atlasData.Width,
            (uint)atlasData.Height,
            0);

        var mipWidth = atlasData.Width;
        var mipHeight = atlasData.Height;

        for (var level = 0; level < atlasMips.Length; level++)
        {
            mipWidth = Math.Max(1, mipWidth / 2);
            mipHeight = Math.Max(1, mipHeight / 2);

            backend.WriteTexture(
                atlasTexture,
                atlasMips[level],
                (uint)(mipWidth * 4),
                (uint)mipWidth,
                (uint)mipHeight,
                (uint)(level + 1));
        }

        var atlasView = backend.CreateTextureView(atlasTexture);

        // One sampler per address/filter state for the whole renderer (T-784 S1b): the reference keeps a SamplerCache so
        // a state always resolves to the same sampler instead of a fresh device object.
        using var samplers = new SamplerCache();
        samplers.Initialize(device);

        // The block atlas carries a mip chain, so it samples like the reference MipmappedTexture: linear minification
        // with an unbounded LOD clamp. Clamping to the sprite edge is kept because the atlas relies on padding.
        // Magnification stays nearest: with a linear magnifier every block face reads visibly soft even at close range,
        // because a 16 texel sprite is stretched over a whole face. Minification is linear and the LOD clamp is
        // unbounded, so the derived mip chain still resolves distant ground.
        var atlasSamplerHandle = samplers.GetMipmapped(AddressMode.ClampToEdge, FilterMode.Nearest);

        Shaders.ShaderPipelines.EnsureValid();

        // The layout is described once, in the seam's terms, with the minimum binding sizes the shaders declare; the
        // backend turns it into its own structure and the pipeline layout consumes the handle directly.
        var bindGroupLayout = backend.CreateBindGroupLayout(
        [
            BindGroupLayoutEntryDesc.Texture(0, ShaderStages.Fragment),
            BindGroupLayoutEntryDesc.Sampler(1, ShaderStages.Fragment),
            BindGroupLayoutEntryDesc.UniformBuffer(VertexLayout.CameraBinding, ShaderStages.Vertex)
                with { MinBindingSize = VertexLayout.CameraUniformSize },
            BindGroupLayoutEntryDesc.UniformBuffer(3, ShaderStages.Vertex | ShaderStages.Fragment)
                with { MinBindingSize = 32 },
            BindGroupLayoutEntryDesc.UniformBuffer(Shaders.ShaderBindings.Lightmap, ShaderStages.Fragment)
                with { MinBindingSize = Shaders.ShaderBindings.LightmapBytes }
        ]);

        var pipelineLayout = backend.CreatePipelineLayout([bindGroupLayout]);

        var cameraBuffer = backend.CreateBufferInit(
            new BufferDesc(VertexLayout.CameraUniformSize, BufferUsages.Uniform | BufferUsages.CopyDst),
            new byte[VertexLayout.CameraUniformSize].AsSpan());

        // Fog: colour plus start and end distance in blocks, shared by the terrain shader.
        var fogBytes = new byte[32];
        var fogValues = new float[8];
        fogValues[0] = request.FogColour.X;
        fogValues[1] = request.FogColour.Y;
        fogValues[2] = request.FogColour.Z;
        fogValues[3] = request.FogColour.W;
        fogValues[4] = diagnostics?.FogStart ?? 64f;
        fogValues[5] = diagnostics?.FogEnd ?? 160f;
        System.Runtime.InteropServices.MemoryMarshal.AsBytes(fogValues.AsSpan()).CopyTo(fogBytes);
        var fogBuffer = backend.CreateBufferInit(
            new BufferDesc(32, BufferUsages.Uniform | BufferUsages.CopyDst),
            fogBytes.AsSpan());
        backend.WriteBuffer(fogBuffer, fogBytes, 0);

        // Lightmap parameters (sky/block factors and colours), rewritten per frame so day/night needs no re-mesh.
        var lightmapBytes = new byte[Shaders.ShaderBindings.LightmapBytes];
        LightmapUniformCodec.Write(LightmapUniform.Day, lightmapBytes);
        var lightmapBuffer = backend.CreateBufferInit(
            new BufferDesc(Shaders.ShaderBindings.LightmapBytes, BufferUsages.Uniform | BufferUsages.CopyDst),
            lightmapBytes.AsSpan());
        backend.WriteBuffer(lightmapBuffer, lightmapBytes, 0);

        // The world bind group is the one every textured pass binds first; it is described in seam terms so the passes
        // receive a handle instead of unwrapping it at each draw.
        var bindGroup = backend.CreateBindGroup(
            bindGroupLayout,
            [
                BindGroupEntryDesc.Texture(0, atlasView),
                BindGroupEntryDesc.Sampler(1, atlasSamplerHandle),
                BindGroupEntryDesc.Uniform(VertexLayout.CameraBinding, cameraBuffer, 0, VertexLayout.CameraUniformSize),
                BindGroupEntryDesc.Uniform(3, fogBuffer, 0, 32),
                BindGroupEntryDesc.Uniform(Shaders.ShaderBindings.Lightmap, lightmapBuffer, 0, Shaders.ShaderBindings.LightmapBytes)
            ]);

        using var entitySkin = new EntitySkinTextureGpu();
        if (entitySkinSource?.Atlas is { } skinData)
        {
            entitySkin.Upload(backend, bindGroupLayout, skinData, cameraBuffer, fogBuffer, lightmapBuffer, atlasSamplerHandle);
        }

        using var itemAtlasGpu = new EntitySkinTextureGpu();
        if (itemAtlasSource?.Atlas is { } itemData)
            itemAtlasGpu.Upload(backend, bindGroupLayout, itemData, cameraBuffer, fogBuffer, lightmapBuffer, atlasSamplerHandle);
        // The frame pipelines are seam handles now, so they are released by hand where the using declarations used to
        // stand (see the cleanup at the end of this method).
        var pipeline = CreatePipeline(backend, format, pipelineLayout);
        var cutoutPipeline = CreateCutoutPipeline(backend, format, pipelineLayout);
        var entityPipeline = CreateEntityPipeline(backend, format, pipelineLayout);
        var entityCutoutPipeline = CreateEntityCutoutPipeline(backend, format, pipelineLayout);
        var itemCutoutPipeline = CreateItemCutoutPipeline(backend, format, pipelineLayout);
        var entityElytraPipeline = CreateEntityElytraPipeline(backend, format, pipelineLayout);
        var entityEmissivePipeline = CreateEntityEmissivePipeline(backend, format, pipelineLayout);
        var entityGlintPipeline = CreateEntityGlintPipeline(backend, format, pipelineLayout);
        var skyPipeline = CreateSkyPipeline(backend, format);
        // Celestial bodies and stars: the same backdrop draws the offscreen path uses, so the window and the exported
        // frame agree (docs/modules/clouds.md 6).
        using var celestial = request.CelestialDraws is { Length: > 0 } && request.CelestialTextures is { Length: > 0 }
            ? new OffscreenRenderer.CelestialResources(backend, request, cameraBuffer, VertexLayout.CameraUniformSize, BackendConvert.FromWgpu(format))
            : null;
        using var celestialColour = request.CelestialColorDraws is { Length: > 0 } || celestialPerFrame is not null
            ? new OffscreenRenderer.CelestialColorResources(backend, request, cameraBuffer, VertexLayout.CameraUniformSize, BackendConvert.FromWgpu(format))
            : null;
        using var skyDiscColour = !skyDiscVanilla && request.SkyDiscDraws is { Length: > 0 }
            ? new OffscreenRenderer.CelestialColorResources(backend, request with { CelestialColorDraws = request.SkyDiscDraws }, cameraBuffer, VertexLayout.CameraUniformSize, BackendConvert.FromWgpu(format))
            : null;
        using var skyDiscVanillaResources = skyDiscVanilla && skyDiscPerFrame is not null
            ? new SkyDiscResources(backend, cameraBuffer, VertexLayout.CameraUniformSize, BackendConvert.FromWgpu(format))
            : null;
        // Clouds change every frame (scroll/camera), so their position-colour draw set is updated per frame.
        using var cloudColour = cloudsPerFrame is null
            ? null
            : new OffscreenRenderer.CelestialColorResources(backend, request with { CelestialColorDraws = [] }, cameraBuffer, VertexLayout.CameraUniformSize, BackendConvert.FromWgpu(format), WGPUCompareFunction.GreaterEqual);
        using var cloudOit = cloudOitEnabled && cloudColour is not null
            ? new CloudOitResources(backend, cameraBuffer, VertexLayout.CameraUniformSize)
            : null;
        var uiPipeline = UiRenderer.CreatePipeline(backend, BackendConvert.FromWgpu(format), pipelineLayout);
        var uiTextPipeline = UiRenderer.CreateTextPipeline(backend, BackendConvert.FromWgpu(format), pipelineLayout);

        // Transparency is the reference depth-bounds + wavelet-transmittance OIT (docs/specs/render/oit-reference.md).
        // The earlier weighted-blended OIT looked markedly worse in play and is shelved, not compiled; git history keeps
        // it if it is ever wanted again.
        var oitTargetFormat = oitLowPrecision ? TextureFormat.Rgba8Unorm : (supportsFloat32Blendable ? TextureFormat.Rgba32Float : TextureFormat.Rgba16Float);
        var refTransmittanceLayout = CreateOitTextureLayout(backend, 1);
        var refAccumulateLayout = CreateOitTextureLayout(backend, 3);
        var refCullLayout = CreateOitTextureLayout(backend, 1);
        var refCompositeLayout = CreateOitTextureLayout(backend, 4);
        // The four layouts are released by hand when the frame loop exits: a handle is pure identity, not a disposable.
        var refTransmittancePipelineLayout = backend.CreatePipelineLayout([bindGroupLayout, refTransmittanceLayout]);
        var refAccumulatePipelineLayout = backend.CreatePipelineLayout([bindGroupLayout, refAccumulateLayout]);
        var refCullPipelineLayout = backend.CreatePipelineLayout([refCullLayout]);
        var refCompositePipelineLayout = backend.CreatePipelineLayout([refCompositeLayout]);

        // The cloud layouts are created through the seam and released as soon as their pipelines exist: a pipeline
        // keeps what it needs from its layout, so nothing outlives this block. Destroying rather than waiting for the
        // method to end is what lets these stay handles instead of needing a Dispose on the handle itself.
        var cloudDbPipelineLayout = cloudOit is null ? default : backend.CreatePipelineLayout([cloudOit.Layout]);
        var cloudDepthBoundsPipeline = cloudOit is null ? default : CreateDepthBoundsPipeline(backend, cloudDbPipelineLayout, "cloud_oit", positionColor: true, format: BackendConvert.ToWgpu(oitTargetFormat), depthWrite: true);
        if (cloudDbPipelineLayout.IsValid)
        {
            backend.Destroy(cloudDbPipelineLayout);
        }

        var cloudTransPipelineLayout = cloudOit is null ? default : backend.CreatePipelineLayout([cloudOit.Layout, refTransmittanceLayout]);
        var cloudTransmittancePipeline = cloudOit is null ? default : CreateTransmittancePipeline(backend, cloudTransPipelineLayout, "cloud_oit", positionColor: true, format: BackendConvert.ToWgpu(oitTargetFormat));
        if (cloudTransPipelineLayout.IsValid)
        {
            backend.Destroy(cloudTransPipelineLayout);
        }

        var cloudAccumPipelineLayout = cloudOit is null ? default : backend.CreatePipelineLayout([cloudOit.Layout, refAccumulateLayout]);
        var cloudAccumulatePipeline = cloudOit is null ? default : CreateAccumulatePipeline(backend, cloudAccumPipelineLayout, "cloud_oit", positionColor: true, format: BackendConvert.ToWgpu(oitTargetFormat));
        if (cloudAccumPipelineLayout.IsValid)
        {
            backend.Destroy(cloudAccumPipelineLayout);
        }

        var refDepthBoundsPipeline = CreateDepthBoundsPipeline(backend, pipelineLayout, format: BackendConvert.ToWgpu(oitTargetFormat));
        var refTransmittancePipeline = CreateTransmittancePipeline(backend, refTransmittancePipelineLayout, format: BackendConvert.ToWgpu(oitTargetFormat));
        var refAccumulatePipeline = CreateAccumulatePipeline(backend, refAccumulatePipelineLayout, format: BackendConvert.ToWgpu(oitTargetFormat));
        var refCullPipeline = CreateCullPipeline(backend, refCullPipelineLayout, BackendConvert.ToWgpu(oitTargetFormat));
        var refCompositePipeline = CreateCompositePipeline(backend, format, refCompositePipelineLayout);
        var cloudCompositePipeline = CreateCompositePipeline(backend, format, refCompositePipelineLayout, WGPUCompareFunction.GreaterEqual);
        var blitDepthLayout = backend.CreateBindGroupLayout([
            BindGroupLayoutEntryDesc.Texture(0, ShaderStages.Fragment) with { SampleType = TextureSampleType.Depth, ViewDimension = TextureViewDimension.D2 },
            BindGroupLayoutEntryDesc.Sampler(1, ShaderStages.Fragment)
        ]);
        using var presentation = FramePresentation.Create(backend, BackendConvert.FromWgpu(format), samplers.GetClampToEdge(FilterMode.Nearest));

        var blitDepthPipelineLayout = backend.CreatePipelineLayout([blitDepthLayout]);
        var blitDepthPipeline = CreateBlitDepthPipeline(backend, blitDepthPipelineLayout);
        var blitDepthSamplerHandle = samplers.GetClampToEdge(FilterMode.Nearest);

        // The interface layer needs its own transform: one uniform buffer cannot hold both the world camera and
        // the orthographic projection while a single pass reads from it.
        var uiTransformBuffer = backend.CreateBuffer(
            new BufferDesc(VertexLayout.CameraUniformSize, BufferUsages.Uniform | BufferUsages.CopyDst));

        // Glyphs live in their own sheet rather than the block atlas: the two have different sampling needs and
        // mixing them would make text blurry at small sizes.
        var glyphData = glyphTexture ?? TextureData.White;
        using var worldText = worldTextPerFrame is not null
            ? new WorldTextResources(backend, device, glyphData, cameraBuffer, VertexLayout.CameraUniformSize, BackendConvert.FromWgpu(format))
            : null;
        var glyphSheet = backend.CreateTexture(new TextureDesc(
            (uint)glyphData.Width,
            (uint)glyphData.Height,
            TextureFormat.Rgba8Unorm,
            1,
            TextureUsages.TextureBinding | TextureUsages.CopyDst));

        backend.WriteTexture(
            glyphSheet,
            glyphData.Rgba,
            (uint)(glyphData.Width * 4),
            (uint)glyphData.Width,
            (uint)glyphData.Height,
            0);

        var glyphView = backend.CreateTextureView(glyphSheet);
        // The interface bind groups differ only in the texture they bind, so they are described in one shape and built
        // with the seam's own factories.
        BindGroupHandle CreateUiBindGroup(TextureViewHandle view) => backend.CreateBindGroup(
            bindGroupLayout,
            [
                BindGroupEntryDesc.Texture(0, view),
                BindGroupEntryDesc.Sampler(1, atlasSamplerHandle),
                BindGroupEntryDesc.Uniform(VertexLayout.CameraBinding, uiTransformBuffer, 0, VertexLayout.CameraUniformSize),
                // The layout declares the fog buffer for the world shader; the interface shader ignores it, but a bind
                // group must still supply every binding the layout declares.
                BindGroupEntryDesc.Uniform(3, fogBuffer, 0, 32),
                BindGroupEntryDesc.Uniform(Shaders.ShaderBindings.Lightmap, lightmapBuffer, 0, Shaders.ShaderBindings.LightmapBytes)
            ]);

        var uiBindGroup = CreateUiBindGroup(atlasView);

        var uiSpritePipeline = UiRenderer.CreateSpritePipeline(backend, BackendConvert.FromWgpu(format), pipelineLayout);

        // GUI sprites (tooltip background and frame) each need their own texture and binding, because one draw call can
        // only bind one texture (docs/decisions/0014-gui-sprite-atlas-and-nine-slice.md).
        var spriteDataList = spriteTextures ?? [];
        var spriteSheets = new List<TextureHandle>(spriteDataList.Count);
        var spriteViews = new List<TextureViewHandle>(spriteDataList.Count);
        var spriteBindGroups = new List<BindGroupHandle>(spriteDataList.Count);

        foreach (var spriteData in spriteDataList)
        {
            var sheet = backend.CreateTexture(new TextureDesc(
                (uint)spriteData.Width,
                (uint)spriteData.Height,
                TextureFormat.Rgba8Unorm,
                1,
                TextureUsages.TextureBinding | TextureUsages.CopyDst));

            backend.WriteTexture(
                sheet,
                spriteData.Rgba,
                (uint)(spriteData.Width * 4),
                (uint)spriteData.Width,
                (uint)spriteData.Height,
                0);

            var view = backend.CreateTextureView(sheet);

            spriteBindGroups.Add(CreateUiBindGroup(view));

            spriteSheets.Add(sheet);
            spriteViews.Add(view);
        }

        var uiTextBindGroup = CreateUiBindGroup(glyphView);

        // The source starts at the textures just uploaded, so the first frame does not re-upload them.
        textureSource?.Update(glyphTexture, spriteTextures ?? []);
        atlasSource?.Update(request.Atlas);

        // A DPI-scaled display reports a logical size smaller than the framebuffer; the swapchain
        // must be configured in physical pixels or every acquire reports "outdated".
        var frameWidth = (uint)window.FramebufferWidth;
        var frameHeight = (uint)window.FramebufferHeight;
        Console.WriteLine($"window: framebuffer {frameWidth}x{frameHeight}, logical {window.Width}x{window.Height}, "
                + $"gui scale {window.Gui.Scale} -> canvas {window.Gui.Width}x{window.Gui.Height}");

        surface.Configure(device, format, TextureUsage.RenderAttachment, frameWidth, frameHeight, presentMode, WGPUCompositeAlphaMode.Auto);

        var currentRequest = request;

        // One store per layer: every chunk keeps its own vertex/index buffer, drawn with CPU frustum culling. Memory
        // follows the resident meshes, so view distances 12-32 need no single-buffer ceiling and never drop far chunks
        // (ADR-0020).
        var arena = geometryUpdates is null ? null : new ChunkGeometryStore(backend, device);
        var arenaCutout = geometryUpdates is null ? null : new ChunkGeometryStore(backend, device);
        var arenaTranslucent = geometryUpdates is null ? null : new ChunkGeometryStore(backend, device);

        // Geometry that is rebuilt every frame, such as placeholder entity boxes. It gets its own small pair of
        // buffers so a per frame rewrite never touches the world arena.
        var dynamicVertex = backend.CreateBuffer(new BufferDesc(MinimumBufferBytes, BufferUsages.Vertex | BufferUsages.CopyDst));
        var dynamicIndex = backend.CreateBuffer(new BufferDesc(MinimumBufferBytes, BufferUsages.Index | BufferUsages.CopyDst));

        var uiVertex = backend.CreateBuffer(new BufferDesc(MinimumBufferBytes, BufferUsages.Vertex | BufferUsages.CopyDst));
        var uiIndex = backend.CreateBuffer(new BufferDesc(MinimumBufferBytes, BufferUsages.Index | BufferUsages.CopyDst));
        var uiVertexCapacity = MinimumBufferBytes;
        var uiIndexCapacity = MinimumBufferBytes;
        var uiIndexCount = 0u;
        var uiRanges = Array.Empty<UiGeometry.Range>();
        var uiScale = 1f;
        var dynamicVertexCapacity = MinimumBufferBytes;
        var dynamicIndexCapacity = MinimumBufferBytes;

        // Entity layers (docs/decisions/0021): persistent buffers grow with the layer plan instead of a fixed cap, so
        // an explicit per-layer definition can add a new pass without truncation. The buffers are seam handles, so both
        // the layer pass and the item OIT callback can bind them without unwrapping.
        var entityLayerVertex = new List<BufferHandle>();
        var entityLayerIndex = new List<BufferHandle>();
        var entityLayerVertexCapacity = new List<ulong>();
        var entityLayerIndexCapacity = new List<ulong>();
        var entityLayerIndexCount = new List<uint>();
        var entityLayerKind = new List<EntityGeometryLayerKind>();
        var entityLayerUsesItemAtlas = new List<bool>();
        var entityLayerDrawn = 0;

        void EnsureEntityLayerCapacity(int count)
        {
            while (entityLayerVertex.Count < count)
            {
                entityLayerVertex.Add(backend.CreateBuffer(new BufferDesc(MinimumBufferBytes, BufferUsages.Vertex | BufferUsages.CopyDst)));
                entityLayerIndex.Add(backend.CreateBuffer(new BufferDesc(MinimumBufferBytes, BufferUsages.Index | BufferUsages.CopyDst)));
                entityLayerVertexCapacity.Add(MinimumBufferBytes);
                entityLayerIndexCapacity.Add(MinimumBufferBytes);
                entityLayerIndexCount.Add(0);
                entityLayerKind.Add(default);
                entityLayerUsesItemAtlas.Add(false);
            }
        }
        EnsureEntityLayerCapacity(4);
        // The helper is defined further down; the buffers above are created eagerly so no nullable state leaks.


        // Geometry lives in persistent buffers that are rewritten in place, so a rebuild costs one upload
        // instead of two allocations plus a GPU sync. They only grow, and the grown-out buffers are released
        // when the window closes (a rebuild that needs more room is rare)
        // (docs/specs/network/play-movement.md 7.23).
        var retiredVertex = new List<BufferHandle>();
        var retiredIndex = new List<BufferHandle>();

        var vertexCapacity = Math.Max((ulong)currentRequest.Vertices.Length * sizeof(float), MinimumBufferBytes);
        var indexCapacity = Math.Max((ulong)currentRequest.Indices.Length * sizeof(uint), MinimumBufferBytes);

        var vertexBuffer = backend.CreateBuffer(new BufferDesc(vertexCapacity, BufferUsages.Vertex | BufferUsages.CopyDst));
        var indexBuffer = backend.CreateBuffer(new BufferDesc(indexCapacity, BufferUsages.Index | BufferUsages.CopyDst));
        backend.WriteBuffer(vertexBuffer, System.Runtime.InteropServices.MemoryMarshal.AsBytes(currentRequest.Vertices.AsSpan()), 0);
        backend.WriteBuffer(indexBuffer, System.Runtime.InteropServices.MemoryMarshal.AsBytes(currentRequest.Indices.AsSpan()), 0);
        var depthTexture = CreateDepthTexture(backend, frameWidth, frameHeight);
        // The reference OIT passes take depth as a seam descriptor, so the view comes from the seam as well and is
        // refreshed with the texture it belongs to on resize.
        var depthViewHandle = backend.CreateTextureView(depthTexture);

        // Reference transparency targets and bind groups. Every target is a seam handle, so the bind groups and every
        // pass downstream take handles too (docs/analysis/architecture/render-backend-seam.md 9.9). Handles are structs,
        // so they are held as defaults and rebuilt by CreateReferenceTargets.
        TextureHandle refDepthBoundsTexture = default;
        TextureViewHandle refDepthBoundsView = default;
        TextureHandle refDepthBoundsCulledTexture = default;
        TextureViewHandle refDepthBoundsCulledView = default;
        TextureHandle refTransmittance0Texture = default;
        TextureViewHandle refTransmittance0View = default;
        TextureHandle refTransmittance1Texture = default;
        TextureViewHandle refTransmittance1View = default;
        TextureHandle refAccumulateTexture = default;
        TextureViewHandle refAccumulateView = default;
        BindGroupHandle refCullBindGroup = default;
        BindGroupHandle refTransmittanceBindGroup = default;
        BindGroupHandle refAccumulateBindGroup = default;
        BindGroupHandle refCompositeBindGroup = default;
        TextureHandle cloudDepthTexture = default;
        TextureViewHandle cloudDepthViewHandle = default;
        BindGroupHandle blitDepthBindGroup = default;
        TextureViewHandle blitDepthBoundFor = default;
        var blitDepthRebuilds = 0;
        var cloudDepthTargetsCreated = false;
        var refTargetsCreated = false;

        // Translucent item geometry composites through its own reference OIT set, as the reference does with
        // OitPipelineSet("item") bound to RenderTypes.ITEM_TRANSLUCENT (ItemOitSet).
        var itemOit = new ItemOitSet();

        // The cloud depth blit samples the main depth, so its bind group belongs to that view: it is rebuilt only when the
        // depth target is recreated (a resize), never per frame (T-806).
        BindGroupHandle BlitDepthGroup()
        {
            if (blitDepthBindGroup.IsValid && blitDepthBoundFor.Equals(depthViewHandle))
            {
                return blitDepthBindGroup;
            }

            if (blitDepthBindGroup.IsValid)
            {
                backend.Destroy(blitDepthBindGroup);
            }

            blitDepthBindGroup = backend.CreateBindGroup(
                blitDepthLayout,
                [BindGroupEntryDesc.Texture(0, depthViewHandle), BindGroupEntryDesc.Sampler(1, blitDepthSamplerHandle)]);
            blitDepthBoundFor = depthViewHandle;
            blitDepthRebuilds++;
            return blitDepthBindGroup;
        }

        void CreateReferenceTargets()
        {
            if (refTargetsCreated)
            {
                backend.Destroy(refCullBindGroup);
                backend.Destroy(refTransmittanceBindGroup);
                backend.Destroy(refAccumulateBindGroup);
                backend.Destroy(refCompositeBindGroup);
                backend.Destroy(refDepthBoundsView);
                backend.Destroy(refDepthBoundsTexture);
                backend.Destroy(refDepthBoundsCulledView);
                backend.Destroy(refDepthBoundsCulledTexture);
                backend.Destroy(refTransmittance0View);
                backend.Destroy(refTransmittance0Texture);
                backend.Destroy(refTransmittance1View);
                backend.Destroy(refTransmittance1Texture);
                backend.Destroy(refAccumulateView);
                backend.Destroy(refAccumulateTexture);
                if (cloudDepthTargetsCreated)
                {
                    backend.Destroy(cloudDepthViewHandle);
                    backend.Destroy(cloudDepthTexture);
                    cloudDepthTargetsCreated = false;
                }

                itemOit.Dispose();

            }

            refDepthBoundsTexture = CreateOitTarget(backend, frameWidth, frameHeight, oitTargetFormat);
            refDepthBoundsView = backend.CreateTextureView(refDepthBoundsTexture);
            refDepthBoundsCulledTexture = CreateOitTarget(backend, frameWidth, frameHeight, oitTargetFormat);
            refDepthBoundsCulledView = backend.CreateTextureView(refDepthBoundsCulledTexture);
            refTransmittance0Texture = CreateOitTarget(backend, frameWidth, frameHeight, oitTargetFormat);
            refTransmittance0View = backend.CreateTextureView(refTransmittance0Texture);
            refTransmittance1Texture = CreateOitTarget(backend, frameWidth, frameHeight, oitTargetFormat);
            refTransmittance1View = backend.CreateTextureView(refTransmittance1Texture);
            refAccumulateTexture = CreateOitTarget(backend, frameWidth, frameHeight, oitTargetFormat);
            refAccumulateView = backend.CreateTextureView(refAccumulateTexture);

            refCullBindGroup = backend.CreateBindGroup(refCullLayout, [BindGroupEntryDesc.Texture(0, refDepthBoundsView)]);
            refTransmittanceBindGroup = backend.CreateBindGroup(refTransmittanceLayout, [BindGroupEntryDesc.Texture(0, refDepthBoundsCulledView)]);
            refAccumulateBindGroup = backend.CreateBindGroup(
                refAccumulateLayout,
                [
                    BindGroupEntryDesc.Texture(0, refDepthBoundsCulledView),
                    BindGroupEntryDesc.Texture(1, refTransmittance0View),
                    BindGroupEntryDesc.Texture(2, refTransmittance1View)
                ]);
            refCompositeBindGroup = backend.CreateBindGroup(
                refCompositeLayout,
                [
                    BindGroupEntryDesc.Texture(0, refAccumulateView),
                    BindGroupEntryDesc.Texture(1, refTransmittance0View),
                    BindGroupEntryDesc.Texture(2, refTransmittance1View),
                    // The composite writes the closest translucent device depth from the original terrain depth bounds,
                    // matching the reference composite (which samples the un-culled target); clouds are excluded from
                    // this depth write, exactly as the reference excludes them.
                    BindGroupEntryDesc.Texture(3, refDepthBoundsView)
                ]);
            itemOit.Create(
                backend,
                frameWidth,
                frameHeight,
                oitTargetFormat,
                refTransmittanceLayout,
                refAccumulateLayout,
                refCompositeLayout);
            // Clouds always get an independent depth target so the cloud depth-bounds write never pollutes the main
            // depth the terrain transparency passes test against (otherwise glass/water behind a cloud are culled).
            if (cloudOit is not null)
            {
                cloudDepthTexture = CreateDepthTexture(backend, frameWidth, frameHeight);
                cloudDepthViewHandle = backend.CreateTextureView(cloudDepthTexture);
                cloudDepthTargetsCreated = true;
            }

            refTargetsCreated = true;
        }

        Span<float> cameraBlock = stackalloc float[16];
        var cameraBytes = new byte[VertexLayout.CameraUniformSize];

        var colour = request.ClearColour;
        var presented = 0;
        var performanceSummary = false;
        var cullStatistics = false;
        var disableCulling = false;
        var frameNumber = 0u;
        var abGroup = 0;
        var abToggleFrames = 0;
        var skipTerrain = false;
        var skipSky = false;
        var skipClear = false;
        var skipPresentCopy = false;
        var skipCelestial = false;
        var skipEntities = false;
        var skipInterface = false;
        var cpuTiming = false;
        var skipOit = false;
        var skipClouds = false;
        var skipTranslucent = false;
        // Cheap enough to accumulate every frame (a dozen counter reads), printed only when the host asks for it.
        var cpuTimings = new FrameCpuTimings();
        var itemOitFrames = 0;
        var frustumPlanes = new System.Numerics.Vector4[6];
        var gapSamples = new double[1 << 17];
        var gapCount = 0;

        // Rolling mean over the frames since the last statistics line, so a steady-state number can be read while
        // chunks are still arriving: the whole-run summary includes loading and says nothing about the settled frame.
        var windowFrameMs = 0d;
        var windowFrameCount = 0;
        var abAMs = 0d;
        var abACount = 0;
        var abBMs = 0d;
        var abBCount = 0;
        var worstFrameMs = 0d;
        var worstFrameIndex = 0;
        var framesOver33 = 0;
        var lastUploadedAt = 0L;
        var rebuildTotalMs = 0d;
        var rebuildCount = 0;
        var worstRebuildMs = 0d;
        var uploadBytes = 0L;
        var cameraTotalMs = 0d;
        var worstCameraMs = 0d;
        var frameClock = System.Diagnostics.Stopwatch.StartNew();
        var lastFrameAt = frameClock.Elapsed.TotalMilliseconds;
        var deadline = maxDuration is { } duration
            ? Environment.TickCount64 + (long)duration.TotalMilliseconds
            : long.MaxValue;

        Console.WriteLine($"window: swapchain configured ({format}, {frameWidth}x{frameHeight})");

        var appliedTextureVersion = textureSource?.Version ?? 0;
        var appliedGeometryEpoch = geometryEpoch?.Invoke() ?? 0;
        var appliedAtlasVersion = atlasSource?.Version ?? 0;
        var appliedEntitySkinVersion = entitySkinSource?.Version ?? 0;
        var appliedItemAtlasVersion = itemAtlasSource?.Version ?? 0;

        while (true)
        {
            // Per frame CPU attribution (diagnostic): every stage below closes with a Cpu call so the report can say where
            // the frame went without a profiler. Removing these calls plus FrameCpuTimings leaves the renderer unchanged.
            var cpuFrameStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            var cpuLast = cpuFrameStarted;

            void Cpu(int stage)
            {
                var now = System.Diagnostics.Stopwatch.GetTimestamp();
                cpuTimings.Add(stage, now - cpuLast);
                cpuLast = now;
            }

            if (geometryEpoch is not null && geometryEpoch() != appliedGeometryEpoch)
            {
                appliedGeometryEpoch = geometryEpoch();

                // The caller rebuilt every mesh for a new atlas layout, so nothing in the old storage is valid. It is
                // dropped here and the caller replays every mesh in the same frame, which is what keeps a resolution
                // change all or nothing (docs/decisions/0017-reload-invalidation-by-layout.md).
                arena?.Dispose();
                arena = new ChunkGeometryStore(backend, device);

                arenaCutout?.Dispose();
                arenaCutout = new ChunkGeometryStore(backend, device);

                arenaTranslucent?.Dispose();
                arenaTranslucent = new ChunkGeometryStore(backend, device);
                Console.WriteLine("ui: world geometry reset");
            }

            if (atlasSource is not null && atlasSource.Version != appliedAtlasVersion)
            {
                appliedAtlasVersion = atlasSource.Version;

                // The atlas drives every textured face, so replacing it means replacing its texture and the two bind
                // groups that point at it. Whether the geometry must also be rebuilt is the caller's call, because it
                // depends on whether the layout changed (docs/decisions/0017-reload-invalidation-by-layout.md).
                var refreshedAtlas = atlasSource.Atlas ?? TextureData.White;

                backend.Destroy(bindGroup);
                backend.Destroy(uiBindGroup);
                backend.Destroy(atlasView);
                backend.Destroy(atlasTexture);

                atlasTexture = backend.CreateTexture(new TextureDesc(
                    (uint)refreshedAtlas.Width,
                    (uint)refreshedAtlas.Height,
                    TextureFormat.Rgba8Unorm,
                    1,
                    TextureUsages.TextureBinding | TextureUsages.CopyDst));

                backend.WriteTexture(
                    atlasTexture,
                    refreshedAtlas.Rgba,
                    (uint)(refreshedAtlas.Width * 4),
                    (uint)refreshedAtlas.Width,
                    (uint)refreshedAtlas.Height,
                    0);

                atlasView = backend.CreateTextureView(atlasTexture);

                bindGroup = backend.CreateBindGroup(
                    bindGroupLayout,
                    [
                        BindGroupEntryDesc.Texture(0, atlasView),
                        BindGroupEntryDesc.Sampler(1, atlasSamplerHandle),
                        BindGroupEntryDesc.Uniform(VertexLayout.CameraBinding, cameraBuffer, 0, VertexLayout.CameraUniformSize),
                        BindGroupEntryDesc.Uniform(3, fogBuffer, 0, 32),
                        BindGroupEntryDesc.Uniform(Shaders.ShaderBindings.Lightmap, lightmapBuffer, 0, Shaders.ShaderBindings.LightmapBytes)
                    ]);

                uiBindGroup = CreateUiBindGroup(atlasView);

                Console.WriteLine($"ui: atlas re-uploaded (version {appliedAtlasVersion}, {refreshedAtlas.Width}x{refreshedAtlas.Height})");
            }

            if (entitySkinSource is not null && entitySkinSource.Version != appliedEntitySkinVersion)
            {
                appliedEntitySkinVersion = entitySkinSource.Version;

                // Player skins arrive after the world starts, so the entity atlas can grow while the window is open.
                // Its geometry is rebuilt every frame from the current sprite rects, which makes replacing the GPU
                // texture and its bind group sufficient.
                if (entitySkinSource.Atlas is { } refreshedSkin)
                {
                    entitySkin.Upload(backend, bindGroupLayout, refreshedSkin, cameraBuffer, fogBuffer, lightmapBuffer, atlasSamplerHandle);
                }
                else
                {
                    entitySkin.Clear();
                }
            }

            if (itemAtlasSource is not null && itemAtlasSource.Version != appliedItemAtlasVersion)
            {
                appliedItemAtlasVersion = itemAtlasSource.Version;
                if (itemAtlasSource.Atlas is { } refreshedItems)
                {
                    itemAtlasGpu.Upload(backend, bindGroupLayout, refreshedItems, cameraBuffer, fogBuffer, lightmapBuffer, atlasSamplerHandle);
                }
                else
                {
                    itemAtlasGpu.Clear();
                }
            }

            if (textureSource is not null && textureSource.Version != appliedTextureVersion)
            {
                appliedTextureVersion = textureSource.Version;

                // A reload replaced the images, so the old GPU copies are stale; drop them and upload the new ones.
                backend.Destroy(uiTextBindGroup);
                backend.Destroy(glyphView);
                backend.Destroy(glyphSheet);

                var refreshedGlyph = textureSource.Glyph;

                // Without a glyph atlas text cannot be drawn, so a 1x1 transparent sheet stands in rather than a stale one.
                refreshedGlyph ??= new TextureData(1, 1, new byte[4]);

                glyphSheet = backend.CreateTexture(new TextureDesc(
                    (uint)refreshedGlyph.Width,
                    (uint)refreshedGlyph.Height,
                    TextureFormat.Rgba8Unorm,
                    1,
                    TextureUsages.TextureBinding | TextureUsages.CopyDst));

                backend.WriteTexture(
                    glyphSheet,
                    refreshedGlyph.Rgba,
                    (uint)(refreshedGlyph.Width * 4),
                    (uint)refreshedGlyph.Width,
                    (uint)refreshedGlyph.Height,
                    0);

                glyphView = backend.CreateTextureView(glyphSheet);
                uiTextBindGroup = CreateUiBindGroup(glyphView);

                foreach (var staleBindGroup in spriteBindGroups)
                {
                    backend.Destroy(staleBindGroup);
                }

                foreach (var staleView in spriteViews)
                {
                    backend.Destroy(staleView);
                }

                foreach (var staleSheet in spriteSheets)
                {
                    backend.Destroy(staleSheet);
                }

                spriteBindGroups.Clear();
                spriteViews.Clear();
                spriteSheets.Clear();

                foreach (var spriteData in textureSource.Sprites)
                {
                    var refreshedSheet = backend.CreateTexture(new TextureDesc(
                        (uint)spriteData.Width,
                        (uint)spriteData.Height,
                        TextureFormat.Rgba8Unorm,
                        1,
                        TextureUsages.TextureBinding | TextureUsages.CopyDst));

                    backend.WriteTexture(
                        refreshedSheet,
                        spriteData.Rgba,
                        (uint)(spriteData.Width * 4),
                        (uint)spriteData.Width,
                        (uint)spriteData.Height,
                        0);

                    var refreshedView = backend.CreateTextureView(refreshedSheet);

                    spriteBindGroups.Add(CreateUiBindGroup(refreshedView));

                    spriteSheets.Add(refreshedSheet);
                    spriteViews.Add(refreshedView);
                }

                Console.WriteLine(
                    $"ui: textures re-uploaded (version {appliedTextureVersion}, {spriteBindGroups.Count} sprites, " +
                    $"glyph {refreshedGlyph.Width}x{refreshedGlyph.Height})");
            }

            if (!window.Pump() || Environment.TickCount64 > deadline)
            {
                break;
            }

            var nextWidth = (uint)window.FramebufferWidth;
            var nextHeight = (uint)window.FramebufferHeight;
            // A closing or minimised window reports a zero-sized framebuffer; reconfiguring the swapchain with
            // it fails validation inside wgpu, so the loop stops instead of pushing an invalid size through
            // (docs/specs/network/play-movement.md 7.26).
            if (window.IsClosing || nextWidth == 0 || nextHeight == 0)
            {
                break;
            }

            if (nextWidth != frameWidth || nextHeight != frameHeight)
            {
                Console.WriteLine($"window: resize {frameWidth}x{frameHeight} -> {nextWidth}x{nextHeight}, "
                    + $"gui scale {window.Gui.Scale} -> canvas {window.Gui.Width}x{window.Gui.Height}");
                frameWidth = nextWidth;
                frameHeight = nextHeight;

                // Everything sized to the old framebuffer is rebuilt: surface and depth. The geometry must be
                // taken from the live request, not the one captured at startup: after a resize the draw call
                // uses the current index count, and a vertex buffer from an older mesh would make those
                // indices point past it, drawing stray triangles that look like overlapping texture.
                surface.Configure(device, format, TextureUsage.RenderAttachment, frameWidth, frameHeight, presentMode, WGPUCompositeAlphaMode.Auto);
                if (blitDepthBindGroup.IsValid)
                {
                    // The group samples the depth view, so it must go first.
                    backend.Destroy(blitDepthBindGroup);
                    blitDepthBindGroup = default;
                    blitDepthBoundFor = default;
                }

                backend.Destroy(depthViewHandle);
                backend.Destroy(depthTexture);
                depthTexture = CreateDepthTexture(backend, frameWidth, frameHeight);
                depthViewHandle = backend.CreateTextureView(depthTexture);

                if (refTargetsCreated)
                {
                    CreateReferenceTargets();
                }

                // The geometry is world-space and the camera travels in a uniform, so a resize needs no
                // re-upload at all.
            }

            // Rewriting the persistent buffers costs one upload; nothing is allocated or freed in the
            // steady state, so streaming new terrain does not hitch.
            var dynamicIndexCount = 0u;
            uiIndexCount = 0u;

            if (uiGeometry is not null && uiGeometry(new UiFrame(window.Input, window.Gui.Width, window.Gui.Height)) is { Ranges.Length: > 0 } ui)
            {
                var neededVertex = (ulong)ui.Vertices.Length * sizeof(float);
                var neededIndex = (ulong)ui.Indices.Length * sizeof(uint);

                if (neededVertex > uiVertexCapacity)
                {
                    backend.Destroy(uiVertex);
                    uiVertexCapacity = Math.Max(neededVertex * 2, MinimumBufferBytes);
                    uiVertex = backend.CreateBuffer(new BufferDesc(uiVertexCapacity, BufferUsages.Vertex | BufferUsages.CopyDst));
                }

                if (neededIndex > uiIndexCapacity)
                {
                    backend.Destroy(uiIndex);
                    uiIndexCapacity = Math.Max(neededIndex * 2, MinimumBufferBytes);
                    uiIndex = backend.CreateBuffer(new BufferDesc(uiIndexCapacity, BufferUsages.Index | BufferUsages.CopyDst));
                }

                backend.WriteBuffer(uiVertex, System.Runtime.InteropServices.MemoryMarshal.AsBytes(ui.Vertices), 0);
                backend.WriteBuffer(uiIndex, System.Runtime.InteropServices.MemoryMarshal.AsBytes(ui.Indices), 0);

                // The layout is expressed in interface pixels, and the canvas is the framebuffer divided by the
                // interface scale (docs/modules/ui.md 9.1). One factor converts both the projection and the
                // scissor rectangles, so a resize or a scale change moves the whole interface coherently.
                var canvas = window.Gui;
                // The factor is the whole scale, not framebuffer divided by canvas width: the canvas is rounded
                // up, so that division is fractional and would place the bottom of the interface a little above the
                // bottom of the screen. With the whole factor the interface lands on device pixels exactly, and the
                // strip left over by rounding stays outside the framebuffer, which is what the reference does.
                uiScale = Math.Max(1, canvas.Scale);
                var matrix = new float[UiProjection.FloatsPerMatrix];
                UiProjection.WriteOrthographic(canvas.Width, canvas.Height, matrix);

                var transformBytes = new byte[VertexLayout.CameraUniformSize];
                for (var element = 0; element < UiProjection.FloatsPerMatrix; element++)
                {
                    BitConverter.TryWriteBytes(transformBytes.AsSpan(element * sizeof(float)), matrix[element]);
                }

                backend.WriteBuffer(uiTransformBuffer, transformBytes, 0);
                uiIndexCount = (uint)ui.Indices.Length;
                uiRanges = ui.Ranges.ToArray();
            }

            if (dynamicGeometry is not null && dynamicGeometry() is { IndexCount: > 0 } dynamic)
            {
                var neededVertex = (ulong)dynamic.VertexFloats * sizeof(float);
                var neededIndex = (ulong)dynamic.IndexCount * sizeof(uint);

                if (neededVertex > dynamicVertexCapacity)
                {
                    backend.Destroy(dynamicVertex);
                    dynamicVertexCapacity = Math.Max(neededVertex * 2, MinimumBufferBytes);
                    dynamicVertex = backend.CreateBuffer(new BufferDesc(dynamicVertexCapacity, BufferUsages.Vertex | BufferUsages.CopyDst));
                }

                if (neededIndex > dynamicIndexCapacity)
                {
                    backend.Destroy(dynamicIndex);
                    dynamicIndexCapacity = Math.Max(neededIndex * 2, MinimumBufferBytes);
                    dynamicIndex = backend.CreateBuffer(new BufferDesc(dynamicIndexCapacity, BufferUsages.Index | BufferUsages.CopyDst));
                }

                backend.WriteBuffer(
                    dynamicVertex,
                    System.Runtime.InteropServices.MemoryMarshal.AsBytes(dynamic.Vertices.AsSpan(0, dynamic.VertexFloats)),
                    0);
                backend.WriteBuffer(
                    dynamicIndex,
                    System.Runtime.InteropServices.MemoryMarshal.AsBytes(dynamic.Indices.AsSpan(0, dynamic.IndexCount)),
                    0);
                dynamicIndexCount = (uint)dynamic.IndexCount;
            }

            entityLayerDrawn = 0;
            if (entityLayers is not null && entityLayers() is { Count: > 0 } layers)
            {
                var used = layers.Count;
                EnsureEntityLayerCapacity(used);
                for (var layerIndex = 0; layerIndex < used; layerIndex++)
                {
                    var layer = layers[layerIndex];
                    var geometry = layer.Geometry;
                    entityLayerIndexCount[layerIndex] = 0;
                    entityLayerKind[layerIndex] = layer.Kind;
                    entityLayerUsesItemAtlas[layerIndex] = layer.UsesItemAtlas;
                    if (geometry.IndexCount <= 0)
                    {
                        continue;
                    }

                    var neededVertex = (ulong)geometry.VertexFloats * sizeof(float);
                    var neededIndex = (ulong)geometry.IndexCount * sizeof(uint);

                    if (neededVertex > entityLayerVertexCapacity[layerIndex])
                    {
                        backend.Destroy(entityLayerVertex[layerIndex]);
                        entityLayerVertexCapacity[layerIndex] = Math.Max(neededVertex * 2, MinimumBufferBytes);
                        entityLayerVertex[layerIndex] = backend.CreateBuffer(
                            new BufferDesc(entityLayerVertexCapacity[layerIndex], BufferUsages.Vertex | BufferUsages.CopyDst));
                    }

                    if (neededIndex > entityLayerIndexCapacity[layerIndex])
                    {
                        backend.Destroy(entityLayerIndex[layerIndex]);
                        entityLayerIndexCapacity[layerIndex] = Math.Max(neededIndex * 2, MinimumBufferBytes);
                        entityLayerIndex[layerIndex] = backend.CreateBuffer(
                            new BufferDesc(entityLayerIndexCapacity[layerIndex], BufferUsages.Index | BufferUsages.CopyDst));
                    }

                    backend.WriteBuffer(
                        entityLayerVertex[layerIndex],
                        System.Runtime.InteropServices.MemoryMarshal.AsBytes(geometry.Vertices.AsSpan(0, geometry.VertexFloats)),
                        0);
                    backend.WriteBuffer(
                        entityLayerIndex[layerIndex],
                        System.Runtime.InteropServices.MemoryMarshal.AsBytes(geometry.Indices.AsSpan(0, geometry.IndexCount)),
                        0);
                    entityLayerIndexCount[layerIndex] = (uint)geometry.IndexCount;
                }

                entityLayerDrawn = used;
            }

            var updatesThisFrame = 0;

            if (arena is not null)
            {
                var updates = geometryUpdates!();
                updatesThisFrame = updates?.Count ?? 0;

                if (updates is { Count: > 0 })
                {
                    foreach (var (key, mesh) in updates)
                    {
                        arena.Apply(key, mesh);

                        // The per chunk layers travel with the opaque geometry; a chunk with none just skips its arena.
                        if (mesh.CutoutVertices is { Length: > 0 } cutoutVertices && arenaCutout is not null)
                        {
                            arenaCutout.Apply(key, new GeometryData(cutoutVertices, mesh.CutoutIndices ?? []));
                        }

                        if (mesh.TranslucentVertices is { Length: > 0 } translucentVertices && arenaTranslucent is not null)
                        {
                            arenaTranslucent.Apply(key, new GeometryData(translucentVertices, mesh.TranslucentIndices ?? []));
                        }
                    }
                }
            }

            if (geometryRemovals?.Invoke() is { Count: > 0 } removals)
            {
                foreach (var key in removals)
                {
                    arena?.Remove(key);
                    arenaCutout?.Remove(key);
                    arenaTranslucent?.Remove(key);
                }
            }

            var rebuildStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            var rebuiltFrame = rebuildRequest?.Invoke();

            if (rebuiltFrame is not null && rebuiltFrame.Vertices.Length > 0)
            {
                var rebuilt = rebuiltFrame;
                currentRequest = rebuilt;

                var neededVertex = (ulong)rebuilt.Vertices.Length * sizeof(float);
                var neededIndex = (ulong)rebuilt.Indices.Length * sizeof(uint);

                if (neededVertex > vertexCapacity)
                {
                    retiredVertex.Add(vertexBuffer);
                    vertexCapacity = Math.Max(neededVertex * 2, MinimumBufferBytes);
                    vertexBuffer = backend.CreateBuffer(new BufferDesc(vertexCapacity, BufferUsages.Vertex | BufferUsages.CopyDst));
                }

                if (neededIndex > indexCapacity)
                {
                    retiredIndex.Add(indexBuffer);
                    indexCapacity = Math.Max(neededIndex * 2, MinimumBufferBytes);
                    indexBuffer = backend.CreateBuffer(new BufferDesc(indexCapacity, BufferUsages.Index | BufferUsages.CopyDst));
                }

                backend.WriteBuffer(vertexBuffer, System.Runtime.InteropServices.MemoryMarshal.AsBytes(rebuilt.Vertices.AsSpan()), 0);
                backend.WriteBuffer(indexBuffer, System.Runtime.InteropServices.MemoryMarshal.AsBytes(rebuilt.Indices.AsSpan()), 0);

                uploadBytes += (long)rebuilt.Vertices.Length * sizeof(float);
                uploadBytes += (long)rebuilt.Indices.Length * sizeof(uint);
            }

            if (rebuildRequest is not null)
            {
                var rebuildMs = (System.Diagnostics.Stopwatch.GetTimestamp() - rebuildStarted) * 1000d
                    / System.Diagnostics.Stopwatch.Frequency;
                rebuildTotalMs += rebuildMs;
                worstRebuildMs = Math.Max(worstRebuildMs, rebuildMs);
                rebuildCount++;
            }

            // Frame pacing: the worst gap is what a dropped frame feels like, and it is the number that
            // regressed when meshing moved between threads.
            Cpu(FrameCpuTimings.UpdatesStage);
            var nowMs = frameClock.Elapsed.TotalMilliseconds;
            performanceSummary = currentRequest.Diagnostics?.PerformanceSummary == true;
            cullStatistics = currentRequest.Diagnostics?.CullStatistics == true;
            disableCulling = currentRequest.Diagnostics?.DisableCulling == true;
            // An interleaved A/B flips the same switches every N frames instead, so both groups live in one process.
            abToggleFrames = currentRequest.Diagnostics?.AbToggleFrames ?? 0;
            abGroup = abToggleFrames > 0 ? (int)((frameNumber / (uint)abToggleFrames) & 1) : 0;
            // Group B skips exactly what the host asked for, so one run attributes one item.
            var wantsSkipTerrain = currentRequest.Diagnostics?.SkipTerrain == true;
            var wantsSkipEntities = currentRequest.Diagnostics?.SkipEntities == true;
            var wantsSkipInterface = currentRequest.Diagnostics?.SkipInterface == true;
            var skipGroup = abToggleFrames > 0 && abGroup == 1;
            skipTerrain = wantsSkipTerrain && (abToggleFrames == 0 || skipGroup);
            skipSky = currentRequest.Diagnostics?.SkipSky == true && (abToggleFrames == 0 || skipGroup);
            skipClear = currentRequest.Diagnostics?.SkipClear == true && (abToggleFrames == 0 || skipGroup);
            skipPresentCopy = currentRequest.Diagnostics?.SkipPresentCopy == true && (abToggleFrames == 0 || skipGroup);
            skipCelestial = currentRequest.Diagnostics?.SkipCelestial == true && (abToggleFrames == 0 || skipGroup);
            skipEntities = wantsSkipEntities && (abToggleFrames == 0 || skipGroup);
            skipInterface = wantsSkipInterface && (abToggleFrames == 0 || skipGroup);
            cpuTiming = currentRequest.Diagnostics?.FrameCpuTiming == true;
            skipOit = currentRequest.Diagnostics?.SkipOit == true && (abToggleFrames == 0 || skipGroup);
            skipClouds = currentRequest.Diagnostics?.SkipClouds == true && (abToggleFrames == 0 || skipGroup);
            skipTranslucent = currentRequest.Diagnostics?.SkipTranslucent == true;
            var gapMs = nowMs - lastFrameAt;

            if (gapCount < gapSamples.Length)
            {
                gapSamples[gapCount++] = gapMs;
            }

            windowFrameMs += gapMs;
            windowFrameCount++;

            // Interleaved A/B: both groups accumulate inside the same window, so drift cancels.
            if (abGroup == 1)
            {
                abBMs += gapMs;
                abBCount++;
            }
            else
            {
                abAMs += gapMs;
                abACount++;
            }
            var uploadedNow = arena?.UploadedBytes ?? 0;
            var uploadedDelta = uploadedNow - lastUploadedAt;
            lastUploadedAt = uploadedNow;

            // Which frame was worst matters as much as how bad it was: a spike in the first few frames is startup
            // work, a spike in the middle is something that happens while playing.
            if (gapMs > worstFrameMs)
            {
                worstFrameMs = gapMs;
                worstFrameIndex = presented;
            }

            if (gapMs > 33.3)
            {
                framesOver33++;

                // A slow frame is either collection or upload; the counters tell them apart without a profiler.
                Console.WriteLine(
                    $"frame #{presented} gap {gapMs:F1} ms, updates {updatesThisFrame}, uploaded +{uploadedDelta / 1024} KiB, gc {GC.CollectionCount(0)}/{GC.CollectionCount(1)}/{GC.CollectionCount(2)}");
            }

            lastFrameAt = nowMs;

            var acquired = surface.GetCurrentTexture();
            Cpu(FrameCpuTimings.AcquireStage);

            // Brew's NeedsReconfigure wraps around on the uint enum (SuccessOptimal reports as
            // needing a reconfigure), so the statuses are compared explicitly instead.
            if (acquired.Status is WGPUSurfaceGetCurrentTextureStatus.Outdated or WGPUSurfaceGetCurrentTextureStatus.Lost)
            {
                // The surface output must be released before the surface is reconfigured.
                acquired.Texture.Dispose();
                surface.Configure(device, format, TextureUsage.RenderAttachment, frameWidth, frameHeight, presentMode, WGPUCompositeAlphaMode.Auto);
                continue;
            }

            if (!acquired.IsUsable)
            {
                Console.WriteLine($"window: acquire status {acquired.Status}");
                acquired.Texture.Dispose();
                break;
            }

            using var surfaceView = acquired.Texture.CreateView();
            // Every world and interface pass targets the frame target; RecordPresent copies it onto the surface.
            var frameView = presentation.Ensure(frameWidth, frameHeight);
            // The camera is sampled once per frame, before the encoder, because the cull pass needs the view projection
            // and a render pass cannot be interrupted to record compute.
            var cameraStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            var (eye, target) = cameraPerFrame is null
                ? (request.CameraPosition, request.CameraTarget)
                : cameraPerFrame(window);
            Cpu(FrameCpuTimings.CameraStage);

            // Sun/moon/stars are built relative to the initial camera; keep them centred as the player moves.
            var celestialDelta = eye - request.CameraPosition;

            // Sample the cloud draw once: the OIT stages need its vertices before the main transparency passes, and the
            // non-OIT path reuses the same draw later.
            var cloudDrawFrame = skipClouds ? null : cloudsPerFrame?.Invoke();
            if (cloudOit is not null && cloudDrawFrame is { } cloudForOit)
            {
                cloudOit.Update(cloudForOit.Vertices, cloudForOit.Indices, cloudForOit.Model, cloudForOit.Tint);
            }

            Cpu(FrameCpuTimings.CloudsStage);

            var encoder = backend.CreateCommandEncoder();
            var frameColour = clearColourPerFrame?.Invoke() ?? colour;
            var frameAttachment = skipClear
                ? ColorAttachmentDesc.Keep(frameView)
                : ColorAttachmentDesc.Clear(frameView, frameColour);
            var depthAttachment = skipClear
                ? DepthAttachmentDesc.Keep(depthViewHandle)
                : DepthAttachmentDesc.Clear(depthViewHandle, 0f);
            var pass = backend.BeginRenderPass(encoder, [frameAttachment], depthAttachment);

            // Camera/light/fog uniforms are frame setup, not pass ordering. The scene pass sequence itself comes from
            // WorldRenderPipeline so the windowed path and the offscreen path share one execution order.
            backend.SetPipeline(pass, pipeline);
            // Rewriting 64 bytes is the whole cost of moving the camera; the staging buffers are reused.
            var cameraMs = (System.Diagnostics.Stopwatch.GetTimestamp() - cameraStarted) * 1000d
                / System.Diagnostics.Stopwatch.Frequency;
            cameraTotalMs += cameraMs;
            worstCameraMs = Math.Max(worstCameraMs, cameraMs);

            CameraUniform.Write(
                cameraBlock,
                eye,
                target,
                request.FieldOfViewDegrees,
                (int)frameWidth,
                (int)frameHeight);

            for (var index = 0; index < 16; index++)
            {
                BitConverter.TryWriteBytes(cameraBytes.AsSpan(index * sizeof(float)), cameraBlock[index]);
            }

            backend.WriteBuffer(cameraBuffer, cameraBytes, 0);

            // Per-frame lightmap parameters: day/night changes only this block, never the mesh.
            var frameLightmapBytes = new byte[Shaders.ShaderBindings.LightmapBytes];
            var frameLightmap = lightmapPerFrame?.Invoke() ?? currentRequest.Lightmap;
            var glintTime = currentRequest.GlintTime != 0f
                ? currentRequest.GlintTime
                : (float)(System.Diagnostics.Stopwatch.GetTimestamp() / (double)System.Diagnostics.Stopwatch.Frequency);
            LightmapUniformCodec.Write(frameLightmap with { Padding = glintTime }, frameLightmapBytes);
            backend.WriteBuffer(lightmapBuffer, frameLightmapBytes, 0);

            // Per-frame fog colour (time/weather driven); the range stays as configured.
            var frameFogColour = fogPerFrame?.Invoke() ?? currentRequest.FogColour;
            var frameFogValues = new float[8];
            frameFogValues[0] = frameFogColour.X;
            frameFogValues[1] = frameFogColour.Y;
            frameFogValues[2] = frameFogColour.Z;
            frameFogValues[3] = frameFogColour.W;
            frameFogValues[4] = currentRequest.Diagnostics?.FogStart ?? 64f;
            frameFogValues[5] = currentRequest.Diagnostics?.FogEnd ?? 160f;
            var frameFogBytes = new byte[Shaders.ShaderBindings.FogBytes];
            System.Runtime.InteropServices.MemoryMarshal.AsBytes(frameFogValues.AsSpan()).CopyTo(frameFogBytes);
            backend.WriteBuffer(fogBuffer, frameFogBytes, 0);

            backend.SetBindGroup(pass, 0, bindGroup);

            var drawsWorld = arena is { ChunkCount: > 0 } || arenaCutout is { ChunkCount: > 0 };
            if (drawsWorld && !disableCulling)
            {
                var viewProjection = CameraMath.BuildViewProjection(
                    eye,
                    target,
                    request.FieldOfViewDegrees,
                    (int)frameWidth,
                    (int)frameHeight);
                FrustumPlanes.Extract(viewProjection, frustumPlanes);
            }

            // The reference draws every section from the same per section buffers; it has no separate static copy of the
            // world. Drawing the preloaded combined mesh next to the arena kept the first few chunks on a second path with
            // its own colour, so the static copy is used only until the arena holds anything.
            var useStaticWorld = arena is null || arena.ChunkCount == 0;
            Cpu(FrameCpuTimings.SceneOpenStage);

            void DrawScenePass(WorldRenderPassKind kind)
            {
                switch (kind)
                {
                    case WorldRenderPassKind.Sky:
                    {
                        if (skipSky)
                        {
                            break;
                        }

                        // Sky first, over the cleared target; world geometry then draws on top of it. When the caller
                        // supplies a flat SKY_COLOR through the clear colour, the gradient sky is skipped.
                        if (!currentRequest.SkipGradientSky)
                        {
                            backend.SetPipeline(pass, skyPipeline);
                            backend.Draw(pass, 3, 1, 0, 0);
                        }

                        Cpu(FrameCpuTimings.SceneSkyStage);

                        if (skipCelestial)
                        {
                            Cpu(FrameCpuTimings.SceneCelestialStage);
                            break;
                        }

                        // Sun/moon (textured) then sunrise/stars (position-colour), still before terrain so world
                        // geometry covers them.
                        if (celestialPerFrame?.Invoke() is { } celestialUpdate)
                        {
                            celestial?.SetDraws(celestialUpdate.Draws);
                            celestialColour?.SetDraws(celestialUpdate.Colors);
                            skyDiscColour?.SetDraws(celestialUpdate.SkyDiscs);
                        }

                        Cpu(FrameCpuTimings.CelestialBuildStage);

                        // Apply the camera delta AFTER any per-frame draw rebuild: a freshly built draw uses the base
                        // (initial) camera model, so translating first would be overwritten and the bodies would stay
                        // anchored in the world.
                        celestial?.UpdateCamera(celestialDelta);
                        celestialColour?.UpdateCamera(celestialDelta);
                        skyDiscColour?.UpdateCamera(celestialDelta);

                        Cpu(FrameCpuTimings.CelestialCameraStage);

                        if (skyDiscVanillaResources is not null && skyDiscPerFrame?.Invoke() is { } skyDiscState)
                        {
                            skyDiscVanillaResources.Update(skyDiscState.Sky, skyDiscState.Fog, skyDiscState.SkyFogEnd, skyDiscState.Eye);

                            Cpu(FrameCpuTimings.CelestialDiscStage);
                            skyDiscVanillaResources.Draw(backend, pass);
                        }

                        skyDiscColour?.Draw(backend, pass);
                        celestial?.Draw(backend, pass);
                        celestialColour?.Draw(backend, pass);

                        Cpu(FrameCpuTimings.SceneCelestialStage);
                        break;
                    }

                    case WorldRenderPassKind.TerrainOpaque:
                    {
                        if (skipTerrain)
                        {
                            break;
                        }

                        backend.SetPipeline(pass, pipeline);
                        backend.SetBindGroup(pass, 0, bindGroup);
                        if (arena is { ChunkCount: > 0 })
                        {
                            if (disableCulling)
                            {
                                arena.Draw(pass);
                            }
                            else
                            {
                                arena.Draw(pass, frustumPlanes);
                            }

                            Cpu(FrameCpuTimings.SceneTerrainStage);
                        }

                        // Clouds are drawn after the transparent terrain (below), matching the reference order.
                        break;
                    }

                    case WorldRenderPassKind.TerrainCutout:
                    {
                        if (skipTerrain || arenaCutout is not { ChunkCount: > 0 })
                        {
                            break;
                        }

                        backend.SetPipeline(pass, cutoutPipeline);
                        backend.SetBindGroup(pass, 0, bindGroup);
                        if (disableCulling)
                        {
                            arenaCutout.Draw(pass);
                        }
                        else
                        {
                            arenaCutout.Draw(pass, frustumPlanes);
                        }
                        break;
                    }

                    case WorldRenderPassKind.Entities:
                    {
                        Cpu(FrameCpuTimings.SceneEntitiesStage);
                        if (!skipEntities && dynamicIndexCount > 0)
                        {
                            backend.SetPipeline(pass, entityPipeline);
                            backend.SetVertexBuffer(pass, 0, dynamicVertex, 0, dynamicVertexCapacity);
                            backend.SetIndexBuffer(pass, dynamicIndex, IndexFormat.Uint32, 0, (ulong)dynamicIndexCount * sizeof(uint));
                            if (entitySkin.Created)
                            {
                                backend.SetBindGroup(pass, 0, entitySkin.BindGroup);
                            }

                            backend.DrawIndexed(pass, dynamicIndexCount, 1, 0, 0, 1);
                            if (entitySkin.Created)
                            {
                                backend.SetBindGroup(pass, 0, bindGroup);
                            }
                        }

                        // Entity layers (docs/decisions/0021): cutout layers share the opaque depth (writes on);
                        // translucent layers blend with depth writes off, which the pipeline state already encodes.
                        for (var layerIndex = 0; layerIndex < entityLayerDrawn; layerIndex++)
                        {
                            var layerIndexCount = entityLayerIndexCount[layerIndex];
                            if (layerIndexCount == 0)
                            {
                                continue;
                            }

                            if (entityLayerKind[layerIndex] == EntityGeometryLayerKind.ItemTranslucent)
                            {
                                // Composed by the reference OIT stages below, exactly as RenderTypes.ITEM_TRANSLUCENT
                                // binds OIT_ITEM in the reference.
                                continue;
                            }

                            backend.SetPipeline(pass, entityLayerKind[layerIndex] switch
                            {
                                EntityGeometryLayerKind.ItemCutout => itemCutoutPipeline,
                                EntityGeometryLayerKind.Translucent => entityPipeline,
                                EntityGeometryLayerKind.Emissive => entityEmissivePipeline,
                                EntityGeometryLayerKind.Glint => entityGlintPipeline,
                                EntityGeometryLayerKind.Elytra => entityElytraPipeline,
                                _ => entityCutoutPipeline
                            });
                            backend.SetVertexBuffer(pass, 0, entityLayerVertex[layerIndex], 0, entityLayerVertexCapacity[layerIndex]);
                            backend.SetIndexBuffer(
                                pass,
                                entityLayerIndex[layerIndex],
                                IndexFormat.Uint32,
                                0,
                                (ulong)layerIndexCount * sizeof(uint));
                            if (entityLayerUsesItemAtlas[layerIndex] && itemAtlasGpu.Created)
                            {
                                backend.SetBindGroup(pass, 0, itemAtlasGpu.BindGroup);
                            }
                            else if (entitySkin.Created)
                            {
                                backend.SetBindGroup(pass, 0, entitySkin.BindGroup);
                            }

                            backend.DrawIndexed(pass, layerIndexCount, 1, 0, 0, 1);
                        }

                        if (entityLayerDrawn > 0 && (entitySkin.Created || itemAtlasGpu.Created))
                        {
                            backend.SetBindGroup(pass, 0, bindGroup);
                        }
                        else if (useStaticWorld)
                        {
                            backend.SetVertexBuffer(pass, 0, vertexBuffer);
                            backend.SetIndexBuffer(pass, indexBuffer, IndexFormat.Uint32);
                            backend.DrawIndexed(pass, (uint)currentRequest.Indices.Length, 1, 0, 0, 1);
                        }

                        // The static fallback keeps its original post-entity order. Translucent terrain is not drawn
                        // here: it writes the order independent targets in the next pass (docs/specs/render/pipeline.md 15).
                        if (useStaticWorld && currentRequest.CutoutVertices is { Length: > 0 } windowCutoutVertices &&
                            currentRequest.CutoutIndices is { Length: > 0 } windowCutoutIndices)
                        {
                            var windowCutoutVertexBuffer = backend.CreateBufferInit(
                                new BufferDesc((ulong)windowCutoutVertices.Length * sizeof(float), BufferUsages.Vertex),
                                System.Runtime.InteropServices.MemoryMarshal.AsBytes(windowCutoutVertices.AsSpan()));
                            var windowCutoutIndexBuffer = backend.CreateBufferInit(
                                new BufferDesc((ulong)windowCutoutIndices.Length * sizeof(uint), BufferUsages.Index),
                                System.Runtime.InteropServices.MemoryMarshal.AsBytes(windowCutoutIndices.AsSpan()));

                            backend.SetPipeline(pass, cutoutPipeline);
                            backend.SetBindGroup(pass, 0, bindGroup);
                            backend.SetVertexBuffer(pass, 0, windowCutoutVertexBuffer);
                            backend.SetIndexBuffer(pass, windowCutoutIndexBuffer, IndexFormat.Uint32);
                            backend.DrawIndexed(pass, (uint)windowCutoutIndices.Length, 1, 0, 0, 1);
                            backend.Destroy(windowCutoutIndexBuffer);
                            backend.Destroy(windowCutoutVertexBuffer);
                        }
                        break;
                    }
                }
            }

            WorldRenderPipeline.RecordScenePasses(DrawScenePass);

            // The main pass ends here: sky, opaque terrain and the cutout layer (which shares the opaque depth) have all
            // been drawn. Translucent terrain and the interface each get their own pass so the order independent resolve
            // can run between them.
            // The scene pass declares locals the rest of the frame uses, so it cannot sit inside a try block; the
            // body between begin and end has no return and no throw, which is what keeps the single end safe.
            backend.EndRenderPass(pass);
            Cpu(FrameCpuTimings.SceneStage);

            if (cloudDepthTargetsCreated)
            {
                // Blit the opaque main depth into the cloud depth attachment (reference BLIT_DEPTH_DURING_DEPTH_BOUNDS).
                // The first pass recorded through the seam: it needs no helper that still takes a native pass, so it
                // proves the whole path - encoder adoption, attachments, bindings and the single end - on its own.
                var blitPass = backend.BeginRenderPass(
                    encoder,
                    [],
                    DepthAttachmentDesc.Keep(cloudDepthViewHandle));
                try
                {
                    backend.SetPipeline(blitPass, blitDepthPipeline);
                    backend.SetBindGroup(blitPass, 0, BlitDepthGroup());
                    backend.Draw(blitPass, 3, 1, 0, 0);
                }
                finally
                {
                    backend.EndRenderPass(blitPass);
                }
            }

            // Reference transparency (docs/specs/render/oit-reference.md): depth bounds, a culling pass, wavelet
            // transmittance, accumulation and a composite that writes depth. The three stages redraw the same
            // translucent geometry, so no sort is needed at all.
            var needsOit = arenaTranslucent?.ChunkCount > 0
                || currentRequest.TranslucentIndices is { Length: > 0 }
                || cloudOit is not null;
            if (needsOit && !refTargetsCreated)
            {
                CreateReferenceTargets();
            }

            if (refTargetsCreated && needsOit && !skipOit)
            {
                var hasRefStatic = useStaticWorld
                    && currentRequest.TranslucentVertices is { Length: > 0 }
                    && currentRequest.TranslucentIndices is { Length: > 0 };
                BufferHandle refStaticVertexBuffer = default;
                BufferHandle refStaticIndexBuffer = default;
                var refStaticIndexCount = 0u;

                if (hasRefStatic)
                {
                    var staticVertices = currentRequest.TranslucentVertices!;
                    var staticIndices = currentRequest.TranslucentIndices!;
                    refStaticVertexBuffer = backend.CreateBufferInit(
                        new BufferDesc((ulong)staticVertices.Length * sizeof(float), BufferUsages.Vertex),
                        System.Runtime.InteropServices.MemoryMarshal.AsBytes(staticVertices.AsSpan()));
                    refStaticIndexBuffer = backend.CreateBufferInit(
                        new BufferDesc((ulong)staticIndices.Length * sizeof(uint), BufferUsages.Index),
                        System.Runtime.InteropServices.MemoryMarshal.AsBytes(staticIndices.AsSpan()));
                    refStaticIndexCount = (uint)staticIndices.Length;
                }

                void DrawReferenceGeometry(RenderPassHandle p)
                {
                    if (skipTranslucent)
                    {
                        // Measurement only (MC_SKIP_TRANSLUCENT): the layers keep uploading, only the draw is dropped.
                        return;
                    }

                    if (arenaTranslucent is not null && arenaTranslucent.ChunkCount > 0)
                    {
                        // Order independent by construction (docs/specs/render/oit-reference.md 6: additive accumulation
                        // and transmittance are commutative), so the paged arena draws page major and binds once per page.
                        arenaTranslucent.Draw(p);
                    }

                    if (hasRefStatic && refStaticIndexCount > 0)
                    {
                        backend.SetVertexBuffer(p, 0, refStaticVertexBuffer);
                        backend.SetIndexBuffer(p, refStaticIndexBuffer, IndexFormat.Uint32);
                        backend.DrawIndexed(p, refStaticIndexCount, 1, 0, 0, 0);
                    }
                }

                // Translucent item geometry joins the same reference OIT stages (the reference binds OIT_ITEM to
                // RenderTypes.ITEM_TRANSLUCENT). The OIT pipelines already declare the entity vertex layout and the shared
                // pipeline layout, so only the atlas bind group differs from the terrain draw.
                void DrawItemOitGeometry(RenderPassHandle p)
                {
                    var atlasBound = false;
                    for (var itemLayer = 0; itemLayer < entityLayerDrawn; itemLayer++)
                    {
                        if (entityLayerKind[itemLayer] != EntityGeometryLayerKind.ItemTranslucent ||
                            entityLayerIndexCount[itemLayer] == 0)
                        {
                            continue;
                        }

                        if (!atlasBound)
                        {
                            backend.SetBindGroup(p, 0, itemAtlasGpu.BindGroup);
                            atlasBound = true;
                        }

                        backend.SetVertexBuffer(p, 0, entityLayerVertex[itemLayer], 0, entityLayerVertexCapacity[itemLayer]);
                        backend.SetIndexBuffer(
                            p,
                            entityLayerIndex[itemLayer],
                            IndexFormat.Uint32,
                            0,
                            (ulong)entityLayerIndexCount[itemLayer] * sizeof(uint));
                        backend.DrawIndexed(p, entityLayerIndexCount[itemLayer], 1, 0, 0, 1);
                    }
                }

                // Terrain transparency (glass/water) tests depth against the opaque main depth only; cloud depth writes
                // go to a separate target below so they cannot cull translucent terrain behind a cloud.
                var depthLoadStore = DepthAttachmentDesc.Keep(depthViewHandle);

                // Cloud OIT stages use the independent cloud depth target (blitted from the opaque depth above), so the
                // cloud depth-bounds write culls only the cloud's own interior faces without hiding terrain behind it.
                var cloudOitDepth = cloudDepthTargetsCreated
                    ? DepthAttachmentDesc.Keep(cloudDepthViewHandle)
                    : depthLoadStore;

                // The item transparency set has its own four full resolution targets; with no translucent item geometry every
                // one of its passes would only clear and then discard, so the whole set is skipped instead (T-799).
                var itemOitGeometryPresent = false;
                for (var itemLayer = 0; itemLayer < entityLayerDrawn; itemLayer++)
                {
                    if (entityLayerKind[itemLayer] == EntityGeometryLayerKind.ItemTranslucent &&
                        entityLayerIndexCount[itemLayer] > 0)
                    {
                        itemOitGeometryPresent = true;
                        break;
                    }
                }

                ReferenceOitPasses.Record(new ReferenceOitPasses.Parameters
                {
                    Backend = backend,
                    Encoder = encoder,
                    Frame = frameView,
                    Depth = depthLoadStore,
                    CloudDepth = cloudOitDepth,
                    Bounds = refDepthBoundsView,
                    BoundsCulled = refDepthBoundsCulledView,
                    Transmittance0 = refTransmittance0View,
                    Transmittance1 = refTransmittance1View,
                    Accumulate = refAccumulateView,
                    BoundsPipeline = refDepthBoundsPipeline,
                    CullPipeline = refCullPipeline,
                    TransmittancePipeline = refTransmittancePipeline,
                    AccumulatePipeline = refAccumulatePipeline,
                    CompositePipeline = refCompositePipeline,
                    TerrainAtlas = bindGroup,
                    Cull = refCullBindGroup,
                    TransmittanceGroup = refTransmittanceBindGroup,
                    AccumulateGroup = refAccumulateBindGroup,
                    CompositeGroup = refCompositeBindGroup,
                    Clouds = skipClouds ? null : cloudOit,
                    CloudBoundsPipeline = cloudDepthBoundsPipeline,
                    CloudTransmittancePipeline = cloudTransmittancePipeline,
                    CloudAccumulatePipeline = cloudAccumulatePipeline,
                    Items = itemOit,
                    ItemAtlas = itemAtlasGpu.BindGroup,
                    DrawTerrain = DrawReferenceGeometry,
                    DrawItems = DrawItemOitGeometry,
                    MaxStages = currentRequest.Diagnostics?.OitStages ?? 0,
                    ItemsHasGeometry = itemOitGeometryPresent
                });

                if (itemOitGeometryPresent)
                {
                    itemOitFrames++;
                }

                if (hasRefStatic)
                {
                    backend.Destroy(refStaticVertexBuffer);
                    backend.Destroy(refStaticIndexBuffer);
                }
            }

            Cpu(FrameCpuTimings.OitStage);
            // (The weighted-blended OIT path that used to live here was shelved for its visual quality and is no longer
            // compiled; the reference OIT above is the only transparency path.)

            // Text, clouds and the interface share one pass: same colour target, same depth attachment, same drawing
            // order. What costs is the pass object itself (encoder validation and driver setup), not the draws inside it
            // (T-804).
            var uiPass = backend.BeginRenderPass(
                encoder,
                [ColorAttachmentDesc.Keep(frameView)],
                DepthAttachmentDesc.Keep(depthViewHandle));
            try
            {
            if (worldText is not null)
            {
                worldText.SetDraws(worldTextPerFrame?.Invoke() ?? []);
                worldText.Draw(backend, uiPass);

                Cpu(FrameCpuTimings.TextStage);
            }
            // Clouds come after the opaque and transparent terrain, with depth testing so terrain and the cloud layer
            // occlude each other correctly (the reference draws them in executeClassicTransparency).
            if (cloudColour is not null && cloudOit is null && !skipClouds)
            {
                if (cloudDrawFrame is { } cloudDraw)
                {
                    cloudColour.SetDraws([cloudDraw]);
                }

                cloudColour.Draw(backend, uiPass);
            }

            Cpu(FrameCpuTimings.CloudsStage);

            // Pass 4: the interface draws last, over the resolved frame, in its own pass so it never participates in the
            // world depth test or the transparency resolve.
            if (!skipInterface)
            {

            if (uiIndexCount > 0 && uiRanges.Length > 0)
            {
                backend.SetVertexBuffer(uiPass, 0, uiVertex, 0, uiVertexCapacity);
                backend.SetIndexBuffer(uiPass, uiIndex, IndexFormat.Uint32, 0, (ulong)uiIndexCount * sizeof(uint));

                var boundMaterial = (UiMaterial?)null;

                // A sprite range also depends on which texture it binds, so the index is part of what decides a rebind.
                var boundSprite = -2;

                // One draw per scissor range, which is what keeps a screen of many widgets down to a few draws.
                // The pipeline and bind group only change when the material does, so panels then text cost two
                // bindings for the whole screen rather than one per range.
                foreach (var range in uiRanges)
                {
                    if (boundMaterial != range.Material || boundSprite != range.SpriteIndex)
                    {
                        boundMaterial = range.Material;
                        boundSprite = range.SpriteIndex;

                        if (range.Material == UiMaterial.Text)
                        {
                            backend.SetPipeline(uiPass, uiTextPipeline);
                            backend.SetBindGroup(uiPass, 0, uiTextBindGroup);
                        }
                        else if (range.Material == UiMaterial.Sprite &&
                                 range.SpriteIndex >= 0 &&
                                 range.SpriteIndex < spriteBindGroups.Count)
                        {
                            backend.SetPipeline(uiPass, uiSpritePipeline);
                            backend.SetBindGroup(uiPass, 0, spriteBindGroups[range.SpriteIndex]);
                        }
                        else
                        {
                            backend.SetPipeline(uiPass, uiPipeline);
                            backend.SetBindGroup(uiPass, 0, uiBindGroup);
                        }
                    }

                    var left = Math.Max(0f, range.MinX * uiScale);
                    var top = Math.Max(0f, range.MinY * uiScale);
                    var right = Math.Min(frameWidth, range.MaxX * uiScale);
                    var bottom = Math.Min(frameHeight, range.MaxY * uiScale);

                    if (right <= left || bottom <= top)
                    {
                        continue;
                    }

                    backend.SetScissorRect(
                        uiPass,
                        (uint)left,
                        (uint)top,
                        (uint)(right - left),
                        (uint)(bottom - top));

                    backend.DrawIndexed(uiPass, (uint)range.IndexCount, 1, (uint)range.FirstIndex, 0, 1);
                }
            }

            }
            }
            finally
            {
                backend.EndRenderPass(uiPass);
            }

            Cpu(FrameCpuTimings.UiStage);

            if (!skipPresentCopy)
            {
                presentation.RecordPresent(backend, encoder, wgpu.AdoptTextureView(surfaceView), DepthAttachmentDesc.Keep(depthViewHandle));
            }

            Cpu(FrameCpuTimings.PresentRecordStage);

            backend.Submit(encoder);

            // Screenshot (reference Screenshot.grab): only when the host asks, and only after the frame was submitted, so
            // the frame target still holds the presented image (docs/specs/render/screenshot.md 2.2).
            if (screenshotRequest?.Invoke(window) is { Length: > 0 } screenshotPath &&
                FrameCapture.Capture(backend, instance, presentation.TargetHandle, frameWidth, frameHeight, BackendConvert.FromWgpu(format)) is { } screenshotPixels)
            {
                screenshotCaptured?.Invoke(screenshotPath, screenshotPixels, frameWidth, frameHeight);
            }

            Cpu(FrameCpuTimings.SubmitStage);

            surface.Present();

            // The acquired texture must be released each frame; leaving it alive makes the next acquire fail
            // when the swapchain reuses its semaphore.
            acquired.Texture.Dispose();

            // Split from the host callback: this is the swapchain wait itself, which is what T-803 asks about.
            Cpu(FrameCpuTimings.SwapStage);

            onFrame?.Invoke(presented, window);
            frameNumber++;

            Cpu(FrameCpuTimings.OnFrameStage);
            cpuTimings.Add(FrameCpuTimings.FrameStage, System.Diagnostics.Stopwatch.GetTimestamp() - cpuFrameStarted);
            cpuTimings.EndFrame();

            // Same cadence and same shape as before the attribution existed: the first line is frame zero.
            if ((presented % 60) == 0)
            {
                if (cullStatistics)
                {
                    // The mean over this window is the settled frame cost; the chunk counts say what was in it.
                    var windowMeanMs = windowFrameCount == 0 ? 0d : windowFrameMs / windowFrameCount;
                    Console.WriteLine(
                        $"cull: chunks={arena?.ChunkCount ?? 0} culled={arena?.CulledLastFrame ?? 0} "
                        + $"cutout={arenaCutout?.ChunkCount ?? 0} translucent={arenaTranslucent?.ChunkCount ?? 0} "
                        + $"drawn={arena?.DrawnLastDraw ?? 0} tri={arena?.TrianglesLastDraw ?? 0} "
                        + (abToggleFrames > 0
                            ? $"abA={(abACount == 0 ? 0d : abAMs / abACount):F3}/{abACount} abB={(abBCount == 0 ? 0d : abBMs / abBCount):F3}/{abBCount} "
                            : string.Empty)
                        + $"frames={windowFrameCount} meanMs={windowMeanMs:F3} (reference oit)");
                    windowFrameMs = 0d;
                    windowFrameCount = 0;
                    abAMs = 0d;
                    abACount = 0;
                    abBMs = 0d;
                    abBCount = 0;
                }

                if (cpuTiming)
                {
                    // Which passes the host asked to skip travels with the numbers, so an A/B row is self describing.
                    var skipped = (skipOit ? "oit" : string.Empty) + (skipOit && (skipClouds || skipTranslucent) ? "+" : string.Empty)
                        + (skipClouds ? "clouds" : string.Empty)
                        + (skipClouds && skipTranslucent ? "+" : string.Empty)
                        + (skipTranslucent ? "translucent" : string.Empty);
                    cpuTimings.WriteLine($"skip={(skipped.Length == 0 ? "none" : skipped)}");
                }
            }

            presented++;

            if (frames > 0 && presented >= frames)
            {
                break;
            }
        }

        if (retiredVertex.Count > 0 || retiredIndex.Count > 0)
        {
            // Retired buffers may still be referenced by the last submissions; wait before freeing.
            device.Queue.OnSubmittedWorkDoneBlocking(instance);
        }

        foreach (var buffer in retiredVertex)
        {
            backend.Destroy(buffer);
        }

        foreach (var buffer in retiredIndex)
        {
            backend.Destroy(buffer);
        }

        Console.WriteLine(
            $"window: {presented} frames in {frameClock.Elapsed.TotalSeconds:F1}s, worst frame {worstFrameMs:F1} ms at #{worstFrameIndex}, {framesOver33} frames over 33 ms");

        // Percentiles are what a player feels, so they are printed on request: the median says what a normal frame costs
        // and the ninety ninth says what the occasional hitch costs.
        if (performanceSummary && gapCount > 0)
        {
            var sorted = gapSamples.AsSpan(0, gapCount).ToArray();
            Array.Sort(sorted);

            static double At(double[] samples, double fraction) =>
                samples[Math.Clamp((int)Math.Ceiling(fraction * samples.Length) - 1, 0, samples.Length - 1)];

            var mean = 0d;

            for (var index = 0; index < sorted.Length; index++)
            {
                mean += sorted[index];
            }

            mean /= sorted.Length;

            Console.WriteLine(
                $"perf: frames={sorted.Length} mean={mean:F2} p50={At(sorted, 0.50):F2} p95={At(sorted, 0.95):F2} "
                + $"p99={At(sorted, 0.99):F2} max={sorted[^1]:F2} over33={framesOver33} "
                + $"rebuildAvgMs={(rebuildCount == 0 ? 0 : rebuildTotalMs / rebuildCount):F1}");
        }
        // Page bookkeeping is read before the stores free their buffers, so the binding summary can describe the layout.
        var solidPages = arena?.PageCount ?? 0;
        var solidMemoryBytes = arena?.MemoryBytes ?? 0;
        var solidLiveBytes = arena?.LiveBytes ?? 0;
        if (arena is not null)
        {
            Console.WriteLine(
                $"window: arena {arena.ChunkCount} chunks, presized={arena.Presized}, "
                + $"memory {arena.MemoryBytes / (1024d * 1024d):F1} MB, "
                + $"uploaded {arena.UploadedBytes / (1024d * 1024d):F1} MB, "
                + $"{arena.FullRebuilds} full rebuilds");
            arena.Dispose();
        }
        if (arenaCutout is not null)
        {
            Console.WriteLine($"window: cutout {arenaCutout.ChunkCount} chunks, memory {arenaCutout.MemoryBytes / (1024d * 1024d):F1} MB");
        }
        if (arenaTranslucent is not null)
        {
            Console.WriteLine($"window: translucent {arenaTranslucent.ChunkCount} chunks, memory {arenaTranslucent.MemoryBytes / (1024d * 1024d):F1} MB");
        }
        var totalDrawCalls = (arena?.DrawCallsTotal ?? 0)
            + (arenaCutout?.DrawCallsTotal ?? 0)
            + (arenaTranslucent?.DrawCallsTotal ?? 0);
        Console.WriteLine($"window: draws/frame {(totalDrawCalls / (double)Math.Max(presented, 1)):F0} (solid/cutout/translucent stores)");
        Console.WriteLine($"window: item oit set ran on {itemOitFrames}/{presented} frames (skipped when no translucent items)");
        Console.WriteLine($"window: cloud depth blit bind group rebuilt {blitDepthRebuilds} times (once per depth target)");
        Console.WriteLine(
            $"window: celestial camera updates applied {celestial?.AppliedCameraUpdates ?? 0}/"
            + $"{celestialColour?.AppliedCameraUpdates ?? 0}/{skyDiscColour?.AppliedCameraUpdates ?? 0}, skipped "
            + $"{celestial?.SkippedCameraUpdates ?? 0}/{celestialColour?.SkippedCameraUpdates ?? 0}/"
            + $"{skyDiscColour?.SkippedCameraUpdates ?? 0}");

        // Bindings are the cost the paged arena removes (ADR-0029): they follow the page count, not the chunk count.
        var totalBinds = (arena?.BindCallsTotal ?? 0)
            + (arenaCutout?.BindCallsTotal ?? 0)
            + (arenaTranslucent?.BindCallsTotal ?? 0);
        var totalPages = solidPages + (arenaCutout?.PageCount ?? 0) + (arenaTranslucent?.PageCount ?? 0);
        var liveBytes = solidLiveBytes + (arenaCutout?.LiveBytes ?? 0) + (arenaTranslucent?.LiveBytes ?? 0);
        var pageBytes = solidMemoryBytes + (arenaCutout?.MemoryBytes ?? 0) + (arenaTranslucent?.MemoryBytes ?? 0);
        Console.WriteLine(
            $"window: binds/frame {(totalBinds / (double)Math.Max(presented, 1)):F1} (vertex+index), "
            + $"pages {solidPages}/{arenaCutout?.PageCount ?? 0}/{arenaTranslucent?.PageCount ?? 0} (solid/cutout/translucent), "
            + $"live {liveBytes / (1024d * 1024d):F1} MB of {pageBytes / (1024d * 1024d):F1} MB in {totalPages} pages");

        Console.WriteLine(
            $"window: rebuild {rebuildCount} frames, avg {(rebuildCount == 0 ? 0 : rebuildTotalMs / rebuildCount):F1} ms, "
            + $"worst {worstRebuildMs:F1} ms, uploaded {uploadBytes / (1024d * 1024d):F1} MB; "
            + $"camera avg {(presented == 0 ? 0 : cameraTotalMs / presented):F1} ms, worst {worstCameraMs:F1} ms");

        if (cpuTiming)
        {
            cpuTimings.WriteSummary();
        }

        backend.Destroy(dynamicVertex);
        backend.Destroy(dynamicIndex);
        for (var layerIndex = 0; layerIndex < entityLayerVertex.Count; layerIndex++)
        {
            backend.Destroy(entityLayerVertex[layerIndex]);
            backend.Destroy(entityLayerIndex[layerIndex]);
        }

        backend.Destroy(uiVertex);
        backend.Destroy(uiIndex);
        backend.Destroy(uiTransformBuffer);

        backend.Destroy(vertexBuffer);
        if (blitDepthBindGroup.IsValid)
        {
            backend.Destroy(blitDepthBindGroup);
        }

        backend.Destroy(depthViewHandle);
        backend.Destroy(depthTexture);
        // The reference OIT targets only exist once a frame has needed transparency, so their handles are guarded: a
        // handle that was never created has nothing to release, and Destroy rejects a default handle.
        if (refTargetsCreated)
        {
            backend.Destroy(refCullBindGroup);
            backend.Destroy(refTransmittanceBindGroup);
            backend.Destroy(refAccumulateBindGroup);
            backend.Destroy(refCompositeBindGroup);
            backend.Destroy(refDepthBoundsView);
            backend.Destroy(refDepthBoundsTexture);
            backend.Destroy(refDepthBoundsCulledView);
            backend.Destroy(refDepthBoundsCulledTexture);
            backend.Destroy(refTransmittance0View);
            backend.Destroy(refTransmittance0Texture);
            backend.Destroy(refTransmittance1View);
            backend.Destroy(refTransmittance1Texture);
            backend.Destroy(refAccumulateView);
            backend.Destroy(refAccumulateTexture);
            if (cloudDepthTargetsCreated)
            {
                backend.Destroy(cloudDepthViewHandle);
                backend.Destroy(cloudDepthTexture);
            }
        }

        backend.Destroy(indexBuffer);

        // The frame pipelines and pipeline layouts are seam handles, so the frame loop's exit releases them by hand: a
        // handle is pure identity and cannot carry a Dispose (render-backend-seam.md 9).
        backend.Destroy(uiSpritePipeline);
        backend.Destroy(uiTextPipeline);
        backend.Destroy(uiPipeline);
        backend.Destroy(cloudCompositePipeline);
        backend.Destroy(refCompositePipeline);
        backend.Destroy(refCullPipeline);
        backend.Destroy(refAccumulatePipeline);
        backend.Destroy(refTransmittancePipeline);
        backend.Destroy(refDepthBoundsPipeline);
        if (cloudAccumulatePipeline.IsValid)
        {
            backend.Destroy(cloudAccumulatePipeline);
        }

        if (cloudTransmittancePipeline.IsValid)
        {
            backend.Destroy(cloudTransmittancePipeline);
        }

        if (cloudDepthBoundsPipeline.IsValid)
        {
            backend.Destroy(cloudDepthBoundsPipeline);
        }

        backend.Destroy(blitDepthPipeline);
        backend.Destroy(skyPipeline);
        backend.Destroy(entityGlintPipeline);
        backend.Destroy(entityEmissivePipeline);
        backend.Destroy(entityElytraPipeline);
        backend.Destroy(itemCutoutPipeline);
        backend.Destroy(entityCutoutPipeline);
        backend.Destroy(entityPipeline);
        backend.Destroy(cutoutPipeline);
        backend.Destroy(pipeline);
        backend.Destroy(blitDepthLayout);
        backend.Destroy(blitDepthPipelineLayout);
        backend.Destroy(refCompositePipelineLayout);
        backend.Destroy(refCullPipelineLayout);
        backend.Destroy(refAccumulatePipelineLayout);
        backend.Destroy(refTransmittancePipelineLayout);
        backend.Destroy(pipelineLayout);
        backend.Destroy(refCompositeLayout);
        backend.Destroy(refCullLayout);
        backend.Destroy(refAccumulateLayout);
        backend.Destroy(refTransmittanceLayout);
        backend.Destroy(bindGroup);

        return format;
    }

    /// <summary>
    /// Uploads clip-space vertices built for the current framebuffer aspect ratio. The staging
    /// array is pooled, so a resize (or a frame with a moving camera) does not allocate megabytes.
    /// </summary>
    /// <summary>Smallest geometry buffer, so tiny meshes do not force a reallocation on the next change.</summary>
    private const ulong MinimumBufferBytes = 1 << 20;

    private static TextureHandle CreateDepthTexture(IRenderBackend backend, uint width, uint height) =>
        backend.CreateTexture(new TextureDesc(
            width,
            height,
            TextureFormat.Depth32Float,
            1,
            TextureUsages.RenderAttachment | TextureUsages.CopySrc | TextureUsages.CopyDst | TextureUsages.TextureBinding));

    /// <summary>Creates one order independent transparency target: renderable and sampleable, at frame size.</summary>
    private static TextureHandle CreateOitTarget(IRenderBackend backend, uint width, uint height, TextureFormat format) =>
        backend.CreateTexture(new TextureDesc(
            width,
            height,
            format,
            1,
            TextureUsages.RenderAttachment | TextureUsages.TextureBinding));

    private static SurfaceSource ToSurfaceSource(NativeSurfaceSource source) => source.Tag switch
    {
        NativeSurfaceKind.Windows => SurfaceSource.WindowsHwnd(source.Handle1, source.Handle0),
        NativeSurfaceKind.X11 => SurfaceSource.XlibWindow(source.Handle0, (ulong)source.Handle1),
        NativeSurfaceKind.Wayland => SurfaceSource.WaylandSurface(source.Handle0, source.Handle1),
        NativeSurfaceKind.MetalLayer => SurfaceSource.MetalLayer(source.Handle0),
        _ => throw new PlatformNotSupportedException($"no surface source for {source.Tag}")
    };

    /// <summary>Layout for a bind group of <paramref name="textureCount"/> sampled textures, fetched with texelFetch.</summary>
    /// <remarks>The targets are Rgba32Float by default, which is sampleable but not filterable, so the entries declare an
    /// unfilterable float binding.</remarks>
    private static BindGroupLayoutHandle CreateOitTextureLayout(WgpuRenderBackend backend, int textureCount)
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
    private static PipelineHandle CreateDepthBoundsPipeline(WgpuRenderBackend backend, PipelineLayoutHandle layout, string shaderName = "oit_reference", bool positionColor = false, WGPUTextureFormat format = WGPUTextureFormat.RGBA16Float, bool depthWrite = false)
    {
        var source = System.Text.Encoding.UTF8.GetBytes(Shaders.ShaderLibrary.Load(shaderName));
        return backend.CreateRenderPipeline(new PipelineDesc(
            source, "vs_main", source, "fs_depth_bounds", layout,
            [new ColorTargetDesc(BackendConvert.FromWgpu(format), new BlendStateDesc(
                new BlendComponentDesc(BlendOperation.Max, BlendFactor.One, BlendFactor.One),
                new BlendComponentDesc(BlendOperation.Max, BlendFactor.One, BlendFactor.One)))],
            new DepthStateDesc(TextureFormat.Depth32Float, CompareFunction.GreaterEqual, depthWrite),
            CullMode.Back, FrontFace.Ccw,
            (ulong)VertexLayout.Stride, VertexStepMode.Vertex,
            LayoutAttributes.From(VertexLayout.Attributes())));
    }

    /// <summary>Reference transparency stage 2: wavelet-transmittance coefficients, additively blended into two targets.</summary>
    private static PipelineHandle CreateTransmittancePipeline(WgpuRenderBackend backend, PipelineLayoutHandle layout, string shaderName = "oit_reference", bool positionColor = false, WGPUTextureFormat format = WGPUTextureFormat.RGBA16Float, bool depthWrite = false)
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
            (ulong)VertexLayout.Stride, VertexStepMode.Vertex,
            LayoutAttributes.From(VertexLayout.Attributes())));
    }

    /// <summary>Reference transparency stage 3: colour weighted by the transmittance in front of each fragment.</summary>
    private static PipelineHandle CreateAccumulatePipeline(WgpuRenderBackend backend, PipelineLayoutHandle layout, string shaderName = "oit_reference", bool positionColor = false, WGPUTextureFormat format = WGPUTextureFormat.RGBA16Float, bool depthWrite = false)
    {
        var source = System.Text.Encoding.UTF8.GetBytes(Shaders.ShaderLibrary.Load(shaderName));
        return backend.CreateRenderPipeline(new PipelineDesc(
            source, "vs_main", source, "fs_accumulate", layout,
            [new ColorTargetDesc(BackendConvert.FromWgpu(format), new BlendStateDesc(
                new BlendComponentDesc(BlendOperation.Add, BlendFactor.One, BlendFactor.One),
                new BlendComponentDesc(BlendOperation.Add, BlendFactor.One, BlendFactor.One)))],
            new DepthStateDesc(TextureFormat.Depth32Float, CompareFunction.GreaterEqual, depthWrite),
            CullMode.Back, FrontFace.Ccw,
            (ulong)VertexLayout.Stride, VertexStepMode.Vertex,
            LayoutAttributes.From(VertexLayout.Attributes())));
    }

    /// <summary>Reference transparency: clip the far depth bound to the nearest opaque surface.</summary>
    private static PipelineHandle CreateCullPipeline(WgpuRenderBackend backend, PipelineLayoutHandle layout, WGPUTextureFormat format = WGPUTextureFormat.RGBA16Float)
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
    private static PipelineHandle CreateBlitDepthPipeline(WgpuRenderBackend backend, PipelineLayoutHandle layout)
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

    private static PipelineHandle CreateCompositePipeline(WgpuRenderBackend backend, WGPUTextureFormat format, PipelineLayoutHandle layout, WGPUCompareFunction depthCompare = WGPUCompareFunction.Always)
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
    private static PipelineHandle CreateSkyPipeline(WgpuRenderBackend backend, WGPUTextureFormat format)
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
    private static PipelineHandle CreateCutoutPipeline(WgpuRenderBackend backend, WGPUTextureFormat format, PipelineLayoutHandle layout)
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
    private static PipelineHandle CreateEntityPipeline(WgpuRenderBackend backend, WGPUTextureFormat format, PipelineLayoutHandle layout)
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
    private static PipelineHandle CreateItemCutoutPipeline(WgpuRenderBackend backend, WGPUTextureFormat format, PipelineLayoutHandle layout)
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
    private static PipelineHandle CreateEntityCutoutPipeline(WgpuRenderBackend backend, WGPUTextureFormat format, PipelineLayoutHandle layout)
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
    private static PipelineHandle CreateEntityElytraPipeline(WgpuRenderBackend backend, WGPUTextureFormat format, PipelineLayoutHandle layout)
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
    private static PipelineHandle CreateEntityEmissivePipeline(WgpuRenderBackend backend, WGPUTextureFormat format, PipelineLayoutHandle layout)
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
    private static PipelineHandle CreateEntityGlintPipeline(WgpuRenderBackend backend, WGPUTextureFormat format, PipelineLayoutHandle layout)
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

    private static PipelineHandle CreatePipeline(WgpuRenderBackend backend, WGPUTextureFormat format, PipelineLayoutHandle layout)
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
