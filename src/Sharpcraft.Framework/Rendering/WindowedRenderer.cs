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
    Func<(float Start, float End)>? fogRangePerFrame = null,
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
    Action<string, byte[], uint, uint>? screenshotCaptured = null,
    Func<long, ulong>? visibleSections = null)
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
                with { MinBindingSize = Shaders.ShaderBindings.LightmapBytes },
            new BindGroupLayoutEntryDesc(VertexLayout.SpriteBinding, ShaderStages.Vertex, BindingKind.ReadOnlyStorageBuffer)
        ]);

        var pipelineLayout = backend.CreatePipelineLayout([bindGroupLayout]);

        var spriteRects = new SpriteRectTable(backend);
        spriteRects.Rebuild(request.SpriteRects);

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
        var bindGroup = TerrainBindGroup.Create(backend, bindGroupLayout, atlasView, atlasSamplerHandle, cameraBuffer, fogBuffer, lightmapBuffer, spriteRects);

        using var entitySkin = new EntitySkinTextureGpu();
        if (entitySkinSource?.Atlas is { } skinData)
        {
            entitySkin.Upload(backend, bindGroupLayout, skinData, cameraBuffer, fogBuffer, lightmapBuffer, atlasSamplerHandle, spriteRects);
        }

        using var itemAtlasGpu = new EntitySkinTextureGpu();
        if (itemAtlasSource?.Atlas is { } itemData)
            itemAtlasGpu.Upload(backend, bindGroupLayout, itemData, cameraBuffer, fogBuffer, lightmapBuffer, atlasSamplerHandle, spriteRects);
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
        using var presentation = FramePresentation.Create(backend, BackendConvert.FromWgpu(format), samplers.GetClampToEdge(FilterMode.Nearest));


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
                BindGroupEntryDesc.Uniform(Shaders.ShaderBindings.Lightmap, lightmapBuffer, 0, Shaders.ShaderBindings.LightmapBytes),
                BindGroupEntryDesc.ReadOnlyStorage(VertexLayout.SpriteBinding, spriteRects.Buffer, 0, spriteRects.Bytes)
            ]);

        var uiBindGroup = CreateUiBindGroup(atlasView);


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
        // FrameResources is the single assembly point for the pipelines, buffers, recorders, OIT targets and the
        // executor's parameter object (T-843 2B-2a). The loop keeps local aliases so the per-frame code is unchanged.
        var uiScale = 1f;
        var entityLayerDrawn = 0;
        var frameResources = new FrameResources(new FrameResources.Options
        {
            Backend = backend,
            Request = request,
            Format = format,
            PipelineLayout = pipelineLayout,
            BindGroupLayout = bindGroupLayout,
            Samplers = samplers,
            CameraBuffer = cameraBuffer,
            FogBuffer = fogBuffer,
            LightmapBuffer = lightmapBuffer,
            BindGroup = bindGroup,
            EntitySkin = entitySkin,
            ItemAtlasGpu = itemAtlasGpu,
            SpriteBindGroups = spriteBindGroups,
            WorldText = worldText,
            Celestial = celestial,
            CelestialColour = celestialColour,
            SkyDiscColour = skyDiscColour,
            SkyDiscVanilla = skyDiscVanillaResources,
            Arena = arena,
            ArenaCutout = arenaCutout,
            ArenaTranslucent = arenaTranslucent,
            Width = frameWidth,
            Height = frameHeight,
            SupportsFloat32Blendable = supportsFloat32Blendable,
            OitLowPrecision = oitLowPrecision,
            CloudOitEnabled = cloudOitEnabled,
            CloudsPerFrame = cloudsPerFrame,
            ClearColourPerFrame = clearColourPerFrame,
            LightmapPerFrame = lightmapPerFrame,
            FogPerFrame = fogPerFrame,
            FogRangePerFrame = fogRangePerFrame,
            WorldTextPerFrame = worldTextPerFrame,
            VisibleSections = visibleSections,
            CelestialPerFrame = celestialPerFrame,
            SkyDiscPerFrame = skyDiscPerFrame
        });
        var deferredRelease = frameResources.DeferredRelease;
        var uiBuffers = frameResources.UiBuffers;
        var dynamicBuffers = frameResources.DynamicBuffers;
        var uiRanges = frameResources.UiRanges;
        var layerPool = frameResources.LayerPool;
        var retiredVertex = frameResources.RetiredVertex;
        var retiredIndex = frameResources.RetiredIndex;
        var staticWorld = frameResources.StaticWorld;
        var geometryUploader = frameResources.GeometryUploader;
        var depthViewHandle = frameResources.DepthView;
        var oitTargets = frameResources.OitTargets;
        var uniforms = frameResources.Uniforms;
        var cloudColour = frameResources.CloudColour;
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
        var cpuTimings = frameResources.Cpu;

        var itemOitFrames = 0;
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
        var pixelCompare = new PixelFrameCompare();
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

        var executorResources = frameResources.ExecutorResources;

        while (true)
        {
            // Per frame CPU attribution (diagnostic): every stage below closes with a Cpu call so the report can say where
            // the frame went without a profiler. Removing these calls plus FrameCpuTimings leaves the renderer unchanged.
            uniforms.Advance(); deferredRelease.BeginFrame();
            var cpuFrameStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            cpuTimings.BeginFrameMarks(cpuFrameStarted);

            void Cpu(int stage) => cpuTimings.Mark(stage);

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

                // The atlas renumbers its sprites when it is rebuilt, so the table has to be replaced with it or the
                // indices already burned into the cached meshes would point at the wrong sprites (T-832).
                spriteRects.Rebuild(atlasSource.SpriteRects);

                bindGroup = TerrainBindGroup.Create(backend, bindGroupLayout, atlasView, atlasSamplerHandle, cameraBuffer, fogBuffer, lightmapBuffer, spriteRects);

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
                    entitySkin.Upload(backend, bindGroupLayout, refreshedSkin, cameraBuffer, fogBuffer, lightmapBuffer, atlasSamplerHandle, spriteRects);
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
                    itemAtlasGpu.Upload(backend, bindGroupLayout, refreshedItems, cameraBuffer, fogBuffer, lightmapBuffer, atlasSamplerHandle, spriteRects);
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

            // Everything before the pump is waiting: the previous frame is still being presented and nothing here can
            // shorten it. Splitting it out is what says whether the largest stage is work or a stall (T-833).
            Cpu(FrameCpuTimings.FrameGapStage);

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
                // FrameResources rebuilds the OIT targets and the depth texture for the new size; the blit bind group
                // is dropped first, before the depth view it samples is replaced.
                frameResources.Resize(frameWidth, frameHeight);
                depthViewHandle = frameResources.DepthView;

                // The geometry is world-space and the camera travels in a uniform, so a resize needs no
                // re-upload at all.
            }

            // Split of the frame preparation, so the diagnostic can name where the budget goes (T-817). This mark closes
            // the diagnostics report, the window pump and the loop head.
            Cpu(FrameCpuTimings.PrepPumpStage);

            // Rewriting the persistent buffers costs one upload; nothing is allocated or freed in the
            // steady state, so streaming new terrain does not hitch.
        var dynamicIndexCount = 0u;
            uiBuffers.SetIndexCount(0);

            if (uiGeometry is not null && uiGeometry(new UiFrame(window.Input, window.Gui.Width, window.Gui.Height)) is { Ranges.Length: > 0 } ui)
            {
                var neededVertex = (ulong)ui.Vertices.Length * sizeof(float);
                var neededIndex = (ulong)ui.Indices.Length * sizeof(uint);

            uiBuffers.EnsureVertexCapacity(neededVertex);

            uiBuffers.EnsureIndexCapacity(neededIndex);

            backend.WriteBuffer(uiBuffers.Vertex, System.Runtime.InteropServices.MemoryMarshal.AsBytes(ui.Vertices), 0);
            backend.WriteBuffer(uiBuffers.Index, System.Runtime.InteropServices.MemoryMarshal.AsBytes(ui.Indices), 0);

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
            uiBuffers.SetIndexCount((uint)ui.Indices.Length);
                uiRanges.Clear(); uiRanges.AddRange(ui.Ranges);
            }

            Cpu(FrameCpuTimings.PrepUiStage);

            // A/B: skip only the buffer writes on the toggled group, so both halves draw the same geometry
            // and the measured difference is the upload alone (T-828).
            var skipUpload = currentRequest.Diagnostics?.SkipGeometryUploadOnAbGroup == true && abGroup == 1;
            var uploadStart = System.Diagnostics.Stopwatch.GetTimestamp();
            var uploaded = geometryUploader.Upload(
                dynamicGeometry?.Invoke(),
                entityLayers?.Invoke(),
                geometryUpdates?.Invoke(),
                geometryRemovals?.Invoke(),
                skipUpload);
            dynamicIndexCount = uploaded.DynamicIndexCount;
            entityLayerDrawn = uploaded.LayersDrawn;
            var updatesThisFrame = uploaded.Updates;

            Cpu(FrameCpuTimings.PrepEntitiesStage);
            cpuTimings.AddAbSample(abGroup, System.Diagnostics.Stopwatch.GetTimestamp() - uploadStart);

            var rebuildStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            var rebuiltFrame = rebuildRequest?.Invoke();

            if (rebuiltFrame is not null && rebuiltFrame.Vertices.Length > 0)
            {
                var rebuilt = rebuiltFrame;
                currentRequest = rebuilt;

                uploadBytes += staticWorld.Upload(rebuilt);
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

            // The whole frame is recorded into the parameterised target; this shell only owns the surface, the
            // presentation blit and the submit (docs/specs/render/single-track-executor.md, step 1).
            var recordedFrame = FrameExecutor.Record(executorResources, new FrameExecutor.Frame
            {
                Target = new FrameTarget(frameView, depthViewHandle, frameWidth, frameHeight),
                Eye = eye,
                CameraTarget = target,
                CameraStarted = cameraStarted,
                CurrentRequest = currentRequest,
                BindGroup = bindGroup,
                UiBindGroup = uiBindGroup,
                UiTextBindGroup = uiTextBindGroup,
                Arena = arena,
                ArenaCutout = arenaCutout,
                ArenaTranslucent = arenaTranslucent,
                DynamicIndexCount = dynamicIndexCount,
                EntityLayerDrawn = entityLayerDrawn,
                UiScale = uiScale,
                SkipClear = skipClear,
                SkipSky = skipSky,
                SkipCelestial = skipCelestial,
                SkipTerrain = skipTerrain,
                SkipEntities = skipEntities,
                SkipOit = skipOit,
                SkipClouds = skipClouds,
                SkipTranslucent = skipTranslucent,
                SkipInterface = skipInterface,
                DisableCulling = disableCulling
            });
            var encoder = recordedFrame.Encoder;
            cameraTotalMs += recordedFrame.CameraMs;
            worstCameraMs = Math.Max(worstCameraMs, recordedFrame.CameraMs);
            if (recordedFrame.ItemOitRan)
            {
                itemOitFrames++;
            }

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

            // The submit bucket above also holds the (usually absent) screenshot branch; these two are the backend's own
            // split of the call, so finish-plus-queue should account for almost all of it (T-819).
            cpuTimings.Add(FrameCpuTimings.SubmitFinishStage, backend.SubmitFinishTicks);
            cpuTimings.Add(FrameCpuTimings.SubmitQueueStage, backend.SubmitQueueTicks);

            surface.Present();
            // The acquired texture must be released each frame, or the next acquire fails on the semaphore.
            acquired.Texture.Dispose();
            deferredRelease.Collect();

            // Split from the host callback: this is the swapchain wait itself, which is what T-803 asks about.
            Cpu(FrameCpuTimings.SwapStage);

            onFrame?.Invoke(presented, window);
            frameNumber++;

            Cpu(FrameCpuTimings.OnFrameStage);
            pixelCompare.Observe(backend, instance, presentation.TargetHandle, frameWidth, frameHeight, BackendConvert.FromWgpu(format), abGroup, currentRequest.Diagnostics);
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
                        + $"cutout={arenaCutout?.ChunkCount ?? 0}/drawn={arenaCutout?.DrawnLastDraw ?? 0} "
                        // Columns are shown as total plus drawn so a column count cannot be read as a draw count again (T-821).
                        + $"translucent={arenaTranslucent?.ChunkCount ?? 0} translucentDrawn={arenaTranslucent?.DrawnLastDraw ?? 0} translucentCulled={arenaTranslucent?.CulledLastFrame ?? 0} "
                        + $"drawn={arena?.DrawnLastDraw ?? 0} tri={arena?.TrianglesLastDraw ?? 0} triTranslucent={arenaTranslucent?.TrianglesLastDraw ?? 0} "
                        + (abToggleFrames > 0
                            ? $"abA={(abACount == 0 ? 0d : abAMs / abACount):F3}/{abACount} abB={(abBCount == 0 ? 0d : abBMs / abBCount):F3}/{abBCount} "
                            : string.Empty)
                        + $"bufs={backend.BuffersCreated}/{backend.BuffersDestroyed} bufMB={(backend.BufferBytesCreated / (1024d * 1024d)):F1} bufLiveMB={(backend.BufferBytesLive / (1024d * 1024d)):F1} tex={backend.TexturesCreated} texMB={(backend.TextureBytesCreated / (1024d * 1024d)):F1} frames={windowFrameCount} meanMs={windowMeanMs:F3} (reference oit)");
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

        if (frameResources.RetiredVertex.Count > 0 || frameResources.RetiredIndex.Count > 0)
        {
            // Retired buffers may still be referenced by the last submissions; wait before freeing.
            device.Queue.OnSubmittedWorkDoneBlocking(instance);
        }



        Console.WriteLine(
            $"window: {presented} frames in {frameClock.Elapsed.TotalSeconds:F1}s, worst frame {worstFrameMs:F1} ms at #{worstFrameIndex}, {framesOver33} frames over 33 ms");
        pixelCompare.WriteReport(frameWidth, frameHeight);
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
        Console.WriteLine($"window: cloud depth blit bind group rebuilt {frameResources.OitTargets.BlitDepthRebuilds} times (once per depth target)");
        var oitCounts = ReferenceOitPasses.LastCounts;
        Console.WriteLine(
            $"window: oit commands per frame bounds={DescribeCounts(oitCounts.Bounds)} "
            + $"trans={DescribeCounts(oitCounts.Transmittance)} accum={DescribeCounts(oitCounts.Accumulate)} "
            + $"cloudBounds={DescribeCounts(oitCounts.CloudBounds)} cloudTrans={DescribeCounts(oitCounts.CloudTransmittance)} "
            + $"cloudAccum={DescribeCounts(oitCounts.CloudAccumulate)} terrainDraws={oitCounts.TerrainDraws} "
            + $"cloudDraws={oitCounts.CloudDraws}");
        Console.WriteLine(
            $"window: celestial colour tint uploads written {celestialColour?.TintWrites ?? 0}, "
            + $"skipped {celestialColour?.TintSkips ?? 0}/{frameResources.CloudColour?.TintWrites ?? 0}+{frameResources.CloudColour?.TintSkips ?? 0} (colour/cloud)");
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

            frameResources.DynamicBuffers.Destroy();
        for (var layerIndex = 0; layerIndex < frameResources.LayerPool.Count; layerIndex++)
        {
            frameResources.DeferredRelease.Release(frameResources.LayerPool.Vertex(layerIndex));
            frameResources.DeferredRelease.Release(frameResources.LayerPool.Index(layerIndex));
        }

            frameResources.UiBuffers.Destroy();
        backend.Destroy(uiTransformBuffer);

            frameResources.StaticWorld.Destroy();
        // OIT targets, the blit bind group, the depth texture and every pipeline/layout are owned by FrameResources.


        // FrameResources owns the pipelines and layouts; the shared world layout and bind group are owned here.
        frameResources.Dispose();
        backend.Destroy(pipelineLayout);
        backend.Destroy(bindGroup);

        return format;
    }

    /// <summary>
    /// Uploads clip-space vertices built for the current framebuffer aspect ratio. The staging
    /// array is pooled, so a resize (or a frame with a moving camera) does not allocate megabytes.
    /// </summary>
    /// <summary>Smallest geometry buffer, so tiny meshes do not force a reallocation on the next change.</summary>
    private const ulong MinimumBufferBytes = 1 << 20;

    /// <summary>Formats one recorded command count as draws/binds/pipelines (T-820).</summary>
    /// <param name="counts">Counts to format.</param>
    /// <returns>Compact text.</returns>
    private static string DescribeCounts(CommandCounts counts) =>
        $"{counts.Draws}d/{counts.BindGroupSets}b/{counts.PipelineSets}p";

    private static SurfaceSource ToSurfaceSource(NativeSurfaceSource source) => source.Tag switch
    {
        NativeSurfaceKind.Windows => SurfaceSource.WindowsHwnd(source.Handle1, source.Handle0),
        NativeSurfaceKind.X11 => SurfaceSource.XlibWindow(source.Handle0, (ulong)source.Handle1),
        NativeSurfaceKind.Wayland => SurfaceSource.WaylandSurface(source.Handle0, source.Handle1),
        NativeSurfaceKind.MetalLayer => SurfaceSource.MetalLayer(source.Handle0),
        _ => throw new PlatformNotSupportedException($"no surface source for {source.Tag}")
    };

}
