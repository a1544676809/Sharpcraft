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
        int expectedChunks = 0,
        Func<DynamicGeometry?>? dynamicGeometry = null,
        Func<UiFrame, UiGeometry?>? uiGeometry = null,
        TextureData? glyphTexture = null,
        IReadOnlyList<TextureData>? spriteTextures = null,
        UiTextureSource? textureSource = null,
        AtlasTextureSource? atlasSource = null,
        Func<int>? geometryEpoch = null,
    FrameDiagnostics? diagnostics = null,
    Func<LightmapUniform>? lightmapPerFrame = null,
    Func<Vector4>? fogPerFrame = null,
    Func<Vector4>? clearColourPerFrame = null,
    Func<CelestialColorDraw?>? cloudsPerFrame = null,
    Func<(CelestialQuadDraw[] Draws, CelestialColorDraw[] Colors)?>? celestialPerFrame = null,
    bool cloudOitEnabled = false)
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
        var adapter = instance.RequestAdapterBlocking(surface);
        // Ask for the limits this adapter actually offers rather than the defaults, whose maxBufferSize is 256 MiB. A full
        // amplified view needs more than that for one geometry buffer, and the adapter's own figure is the honest ceiling.
        var deviceLimits = adapter.GetLimits();
        var deviceDescriptor = new DeviceDescriptor { RequiredLimits = deviceLimits };
        var device = adapter.RequestDeviceBlocking(in deviceDescriptor);

        Console.WriteLine($"window: surface ready on {adapter.GetInfo().Description}");
        // A non-sRGB surface blends in gamma space, which is what the reference does
        // (docs/decisions/0013-gamma-space-ui-blending.md). Using the preferred sRGB format made every translucent
        // overlay lighter than the reference.
        var format = WGPUTextureFormat.BGRA8Unorm;

        // Atlas resources: the fragment shader samples it, so the pipeline layout and the bind group
        // have to exist before anything is drawn (docs/modules/render.md 6.1).
        var atlasData = request.Atlas ?? TextureData.White;

        var atlasMips = atlasData.Mips ?? [];
        var atlasTexture = device.CreateTexture(new TextureDescriptor
        {
            Size = new WGPUExtent3D
            {
                width = (uint)atlasData.Width,
                height = (uint)atlasData.Height,
                depthOrArrayLayers = 1
            },
            MipLevelCount = 1 + (uint)atlasMips.Length,
            SampleCount = 1,
            Dimension = WGPUTextureDimension._2D,
            Format = WGPUTextureFormat.RGBA8Unorm,
            Usage = TextureUsage.TextureBinding | TextureUsage.CopyDst
        });

        device.Queue.WriteTexture(
            atlasTexture,
            atlasData.Rgba,
            (uint)(atlasData.Width * 4),
            (uint)atlasData.Height,
            new WGPUExtent3D
            {
                width = (uint)atlasData.Width,
                height = (uint)atlasData.Height,
                depthOrArrayLayers = 1
            },
            0,
            new WGPUOrigin3D { x = 0, y = 0, z = 0 });

        var mipWidth = atlasData.Width;
        var mipHeight = atlasData.Height;

        for (var level = 0; level < atlasMips.Length; level++)
        {
            mipWidth = Math.Max(1, mipWidth / 2);
            mipHeight = Math.Max(1, mipHeight / 2);

            device.Queue.WriteTexture(
                atlasTexture,
                atlasMips[level],
                (uint)(mipWidth * 4),
                (uint)mipHeight,
                new WGPUExtent3D { width = (uint)mipWidth, height = (uint)mipHeight, depthOrArrayLayers = 1 },
                (uint)(level + 1),
                new WGPUOrigin3D { x = 0, y = 0, z = 0 });
        }


        var atlasView = atlasTexture.CreateView();

        using var atlasSampler = device.CreateSampler(new SamplerDescriptor
        {
            AddressModeU = WGPUAddressMode.ClampToEdge,
            AddressModeV = WGPUAddressMode.ClampToEdge,
            AddressModeW = WGPUAddressMode.ClampToEdge,
            MagFilter = WGPUFilterMode.Nearest,
            MinFilter = WGPUFilterMode.Nearest,
            MipmapFilter = WGPUMipmapFilterMode.Nearest,
            LodMinClamp = 0f,
            LodMaxClamp = 1f,
            MaxAnisotropy = 1
        });

        Shaders.ShaderPipelines.EnsureValid();

        using var bindGroupLayout = device.CreateBindGroupLayout(
        [
            BindGroupLayoutEntry.Texture(
                0,
                ShaderStage.Fragment,
                WGPUTextureSampleType.Float,
                WGPUTextureViewDimension._2D,
                false),
            BindGroupLayoutEntry.Sampler(1, ShaderStage.Fragment, WGPUSamplerBindingType.Filtering),
            BindGroupLayoutEntry.Buffer(
                VertexLayout.CameraBinding,
                ShaderStage.Vertex,
                WGPUBufferBindingType.Uniform,
                false,
                VertexLayout.CameraUniformSize),
            BindGroupLayoutEntry.Buffer(
                3,
                ShaderStage.Vertex | ShaderStage.Fragment,
                WGPUBufferBindingType.Uniform,
                false,
                32),
            BindGroupLayoutEntry.Buffer(
                Shaders.ShaderBindings.Lightmap,
                ShaderStage.Fragment,
                WGPUBufferBindingType.Uniform,
                false,
                Shaders.ShaderBindings.LightmapBytes)
        ]);

        using var pipelineLayout = device.CreatePipelineLayout([bindGroupLayout]);

        using var cameraBuffer = device.CreateBufferInit(
            new BufferInitDescriptor { Usage = BufferUsage.Uniform | BufferUsage.CopyDst },
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
        using var fogBuffer = device.CreateBufferInit(
            new BufferInitDescriptor { Usage = BufferUsage.Uniform | BufferUsage.CopyDst },
            fogBytes.AsSpan());
        device.Queue.WriteBuffer(fogBuffer, 0, fogBytes.AsSpan());

        // Lightmap parameters (sky/block factors and colours), rewritten per frame so day/night needs no re-mesh.
        var lightmapBytes = new byte[Shaders.ShaderBindings.LightmapBytes];
        LightmapUniformCodec.Write(LightmapUniform.Day, lightmapBytes);
        using var lightmapBuffer = device.CreateBufferInit(
            new BufferInitDescriptor { Usage = BufferUsage.Uniform | BufferUsage.CopyDst },
            lightmapBytes.AsSpan());
        device.Queue.WriteBuffer(lightmapBuffer, 0, lightmapBytes.AsSpan());

        var bindGroup = device.CreateBindGroup(
            bindGroupLayout,
            [
                BindGroupEntry.TextureView(0, atlasView),
                BindGroupEntry.Sampler(1, atlasSampler),
                BindGroupEntry.Buffer(VertexLayout.CameraBinding, cameraBuffer, 0, VertexLayout.CameraUniformSize),
                BindGroupEntry.Buffer(3, fogBuffer, 0, 32),
                BindGroupEntry.Buffer(Shaders.ShaderBindings.Lightmap, lightmapBuffer, 0, Shaders.ShaderBindings.LightmapBytes)
            ]);

        using var pipeline = CreatePipeline(device, format, pipelineLayout);
        using var cutoutPipeline = CreateCutoutPipeline(device, format, pipelineLayout);
        using var skyPipeline = CreateSkyPipeline(device, format);
        // Celestial bodies and stars: the same backdrop draws the offscreen path uses, so the window and the exported
        // frame agree (docs/modules/clouds.md 6).
        using var celestial = request.CelestialDraws is { Length: > 0 } && request.CelestialTextures is { Length: > 0 }
            ? new OffscreenRenderer.CelestialResources(device, request, cameraBuffer, VertexLayout.CameraUniformSize, format)
            : null;
        using var celestialColour = request.CelestialColorDraws is { Length: > 0 } || celestialPerFrame is not null
            ? new OffscreenRenderer.CelestialColorResources(device, request, cameraBuffer, VertexLayout.CameraUniformSize, format)
            : null;
        // Clouds change every frame (scroll/camera), so their position-colour draw set is updated per frame.
        using var cloudColour = cloudsPerFrame is null
            ? null
            : new OffscreenRenderer.CelestialColorResources(device, request with { CelestialColorDraws = [] }, cameraBuffer, VertexLayout.CameraUniformSize, format, WGPUCompareFunction.LessEqual);
        using var cloudOit = cloudOitEnabled && cloudColour is not null
            ? new CloudOitResources(device, bindGroupLayout, cameraBuffer, VertexLayout.CameraUniformSize)
            : null;
        using var uiPipeline = UiRenderer.CreatePipeline(device, format, pipelineLayout);
        using var uiTextPipeline = UiRenderer.CreateTextPipeline(device, format, pipelineLayout);

        // Transparency is the reference depth-bounds + wavelet-transmittance OIT (docs/specs/render/oit-reference.md).
        // The earlier weighted-blended OIT looked markedly worse in play and is shelved, not compiled; git history keeps
        // it if it is ever wanted again.
        using var refTransmittanceLayout = CreateOitTextureLayout(device, 1);
        using var refAccumulateLayout = CreateOitTextureLayout(device, 3);
        using var refCullLayout = CreateOitTextureLayout(device, 1);
        using var refCompositeLayout = CreateOitTextureLayout(device, 4);
        using var refTransmittancePipelineLayout = device.CreatePipelineLayout([bindGroupLayout, refTransmittanceLayout]);
        using var refAccumulatePipelineLayout = device.CreatePipelineLayout([bindGroupLayout, refAccumulateLayout]);
        using var refCullPipelineLayout = device.CreatePipelineLayout([refCullLayout]);
        using var refCompositePipelineLayout = device.CreatePipelineLayout([refCompositeLayout]);

        using var refDepthBoundsPipeline = CreateDepthBoundsPipeline(device, pipelineLayout);
        using var refTransmittancePipeline = CreateTransmittancePipeline(device, refTransmittancePipelineLayout);
        using var refAccumulatePipeline = CreateAccumulatePipeline(device, refAccumulatePipelineLayout);
        using var refCullPipeline = CreateCullPipeline(device, refCullPipelineLayout);
        using var refCompositePipeline = CreateCompositePipeline(device, format, refCompositePipelineLayout);

        // The interface layer needs its own transform: one uniform buffer cannot hold both the world camera and
        // the orthographic projection while a single pass reads from it.
        var uiTransformBuffer = CreateGeometryBuffer(device, VertexLayout.CameraUniformSize, BufferUsage.Uniform | BufferUsage.CopyDst);

        // Glyphs live in their own sheet rather than the block atlas: the two have different sampling needs and
        // mixing them would make text blurry at small sizes.
        var glyphData = glyphTexture ?? TextureData.White;
        var glyphSheet = device.CreateTexture(new TextureDescriptor
        {
            Size = new WGPUExtent3D
            {
                width = (uint)glyphData.Width,
                height = (uint)glyphData.Height,
                depthOrArrayLayers = 1
            },
            MipLevelCount = 1,
            SampleCount = 1,
            Dimension = WGPUTextureDimension._2D,
            Format = WGPUTextureFormat.RGBA8Unorm,
            Usage = TextureUsage.TextureBinding | TextureUsage.CopyDst
        });

        device.Queue.WriteTexture(
            glyphSheet,
            glyphData.Rgba,
            (uint)(glyphData.Width * 4),
            (uint)glyphData.Height,
            new WGPUExtent3D
            {
                width = (uint)glyphData.Width,
                height = (uint)glyphData.Height,
                depthOrArrayLayers = 1
            },
            0,
            new WGPUOrigin3D { x = 0, y = 0, z = 0 });

        var glyphView = glyphSheet.CreateView();
        var uiBindGroup = device.CreateBindGroup(
            bindGroupLayout,
            [
                BindGroupEntry.TextureView(0, atlasView),
                BindGroupEntry.Sampler(1, atlasSampler),
                BindGroupEntry.Buffer(VertexLayout.CameraBinding, uiTransformBuffer, 0, VertexLayout.CameraUniformSize),
                // The layout declares the fog buffer for the world shader; the interface shader ignores it, but a bind
                // group must still supply every binding the layout declares.
                BindGroupEntry.Buffer(3, fogBuffer, 0, 32),
                BindGroupEntry.Buffer(Shaders.ShaderBindings.Lightmap, lightmapBuffer, 0, Shaders.ShaderBindings.LightmapBytes)
            ]);

        using var uiSpritePipeline = UiRenderer.CreateSpritePipeline(device, format, pipelineLayout);

        // GUI sprites (tooltip background and frame) each need their own texture and binding, because one draw call can
        // only bind one texture (docs/decisions/0014-gui-sprite-atlas-and-nine-slice.md).
        var spriteDataList = spriteTextures ?? [];
        var spriteSheets = new List<Texture>(spriteDataList.Count);
        var spriteViews = new List<TextureView>(spriteDataList.Count);
        var spriteBindGroups = new List<BindGroup>(spriteDataList.Count);

        foreach (var spriteData in spriteDataList)
        {
            var sheet = device.CreateTexture(new TextureDescriptor
            {
                Size = new WGPUExtent3D
                {
                    width = (uint)spriteData.Width,
                    height = (uint)spriteData.Height,
                    depthOrArrayLayers = 1
                },
                MipLevelCount = 1,
                SampleCount = 1,
                Dimension = WGPUTextureDimension._2D,
                Format = WGPUTextureFormat.RGBA8Unorm,
                Usage = TextureUsage.TextureBinding | TextureUsage.CopyDst
            });

            device.Queue.WriteTexture(
                sheet,
                spriteData.Rgba,
                (uint)(spriteData.Width * 4),
                (uint)spriteData.Height,
                new WGPUExtent3D
                {
                    width = (uint)spriteData.Width,
                    height = (uint)spriteData.Height,
                    depthOrArrayLayers = 1
                },
                0,
                new WGPUOrigin3D { x = 0, y = 0, z = 0 });

            var view = sheet.CreateView();

            spriteBindGroups.Add(device.CreateBindGroup(
                bindGroupLayout,
                [
                    BindGroupEntry.TextureView(0, view),
                    BindGroupEntry.Sampler(1, atlasSampler),
                    BindGroupEntry.Buffer(VertexLayout.CameraBinding, uiTransformBuffer, 0, VertexLayout.CameraUniformSize),
                    BindGroupEntry.Buffer(3, fogBuffer, 0, 32),
                BindGroupEntry.Buffer(Shaders.ShaderBindings.Lightmap, lightmapBuffer, 0, Shaders.ShaderBindings.LightmapBytes)
                ]));

            spriteSheets.Add(sheet);
            spriteViews.Add(view);
        }

        var uiTextBindGroup = device.CreateBindGroup(
            bindGroupLayout,
            [
                BindGroupEntry.TextureView(0, glyphView),
                BindGroupEntry.Sampler(1, atlasSampler),
                BindGroupEntry.Buffer(VertexLayout.CameraBinding, uiTransformBuffer, 0, VertexLayout.CameraUniformSize),
                BindGroupEntry.Buffer(3, fogBuffer, 0, 32),
                BindGroupEntry.Buffer(Shaders.ShaderBindings.Lightmap, lightmapBuffer, 0, Shaders.ShaderBindings.LightmapBytes)
            ]);

        // The source starts at the textures just uploaded, so the first frame does not re-upload them.
        textureSource?.Update(glyphTexture, spriteTextures ?? []);
        atlasSource?.Update(request.Atlas);

        // A DPI-scaled display reports a logical size smaller than the framebuffer; the swapchain
        // must be configured in physical pixels or every acquire reports "outdated".
        var frameWidth = (uint)window.FramebufferWidth;
        var frameHeight = (uint)window.FramebufferHeight;
        Console.WriteLine($"window: framebuffer {frameWidth}x{frameHeight}, logical {window.Width}x{window.Height}, "
                + $"gui scale {window.Gui.Scale} -> canvas {window.Gui.Width}x{window.Gui.Height}");

        surface.Configure(device, format, TextureUsage.RenderAttachment, frameWidth, frameHeight);

        var currentRequest = request;

        // One store per layer: every chunk keeps its own vertex/index buffer, drawn with CPU frustum culling. Memory
        // follows the resident meshes, so view distances 12-32 need no single-buffer ceiling and never drop far chunks
        // (ADR-0020).
        var arena = geometryUpdates is null ? null : new ChunkGeometryStore(device);
        var arenaCutout = geometryUpdates is null ? null : new ChunkGeometryStore(device);
        var arenaTranslucent = geometryUpdates is null ? null : new ChunkGeometryStore(device);

        // Geometry that is rebuilt every frame, such as placeholder entity boxes. It gets its own small pair of
        // buffers so a per frame rewrite never touches the world arena.
        var dynamicVertex = CreateGeometryBuffer(device, MinimumBufferBytes, BufferUsage.Vertex | BufferUsage.CopyDst);
        var dynamicIndex = CreateGeometryBuffer(device, MinimumBufferBytes, BufferUsage.Index | BufferUsage.CopyDst);

        var uiVertex = CreateGeometryBuffer(device, MinimumBufferBytes, BufferUsage.Vertex | BufferUsage.CopyDst);
        var uiIndex = CreateGeometryBuffer(device, MinimumBufferBytes, BufferUsage.Index | BufferUsage.CopyDst);
        var uiVertexCapacity = MinimumBufferBytes;
        var uiIndexCapacity = MinimumBufferBytes;
        var uiIndexCount = 0u;
        var uiRanges = Array.Empty<UiGeometry.Range>();
        var uiScale = 1f;
        var dynamicVertexCapacity = MinimumBufferBytes;
        var dynamicIndexCapacity = MinimumBufferBytes;

        // The helper is defined further down; the buffers above are created eagerly so no nullable state leaks.


        // Geometry lives in persistent buffers that are rewritten in place, so a rebuild costs one upload
        // instead of two allocations plus a GPU sync. They only grow, and the grown-out buffers are released
        // when the window closes (a rebuild that needs more room is rare)
        // (docs/specs/network/play-movement.md 7.23).
        var retiredVertex = new List<WgpuBuffer>();
        var retiredIndex = new List<WgpuBuffer>();

        var vertexCapacity = Math.Max((ulong)currentRequest.Vertices.Length * sizeof(float), MinimumBufferBytes);
        var indexCapacity = Math.Max((ulong)currentRequest.Indices.Length * sizeof(uint), MinimumBufferBytes);

        var vertexBuffer = CreateGeometryBuffer(device, vertexCapacity, BufferUsage.Vertex | BufferUsage.CopyDst);
        var indexBuffer = CreateGeometryBuffer(device, indexCapacity, BufferUsage.Index | BufferUsage.CopyDst);
        device.Queue.WriteBuffer(vertexBuffer, 0, currentRequest.Vertices.AsSpan());
        device.Queue.WriteBuffer(indexBuffer, 0, currentRequest.Indices.AsSpan());
        var depthTexture = CreateDepthTexture(device, frameWidth, frameHeight);
        var depthView = depthTexture.CreateView();

        // Reference transparency targets and bind groups. Brew handles are structs, so they are held as non-nullable
        // defaults and rebuilt by CreateReferenceTargets on resize.
        Texture refDepthBoundsTexture = default;
        TextureView refDepthBoundsView = default;
        Texture refDepthBoundsCulledTexture = default;
        TextureView refDepthBoundsCulledView = default;
        Texture refTransmittance0Texture = default;
        TextureView refTransmittance0View = default;
        Texture refTransmittance1Texture = default;
        TextureView refTransmittance1View = default;
        Texture refAccumulateTexture = default;
        TextureView refAccumulateView = default;
        BindGroup refCullBindGroup = default;
        BindGroup refTransmittanceBindGroup = default;
        BindGroup refAccumulateBindGroup = default;
        BindGroup refCompositeBindGroup = default;
        var refTargetsCreated = false;

        void CreateReferenceTargets()
        {
            if (refTargetsCreated)
            {
                refCullBindGroup.Dispose();
                refTransmittanceBindGroup.Dispose();
                refAccumulateBindGroup.Dispose();
                refCompositeBindGroup.Dispose();
                refDepthBoundsView.Dispose();
                refDepthBoundsTexture.Dispose();
                refDepthBoundsCulledView.Dispose();
                refDepthBoundsCulledTexture.Dispose();
                refTransmittance0View.Dispose();
                refTransmittance0Texture.Dispose();
                refTransmittance1View.Dispose();
                refTransmittance1Texture.Dispose();
                refAccumulateView.Dispose();
                refAccumulateTexture.Dispose();
            }

            refDepthBoundsTexture = CreateOitTarget(device, frameWidth, frameHeight, WGPUTextureFormat.RGBA16Float);
            refDepthBoundsView = refDepthBoundsTexture.CreateView();
            refDepthBoundsCulledTexture = CreateOitTarget(device, frameWidth, frameHeight, WGPUTextureFormat.RGBA16Float);
            refDepthBoundsCulledView = refDepthBoundsCulledTexture.CreateView();
            refTransmittance0Texture = CreateOitTarget(device, frameWidth, frameHeight, WGPUTextureFormat.RGBA16Float);
            refTransmittance0View = refTransmittance0Texture.CreateView();
            refTransmittance1Texture = CreateOitTarget(device, frameWidth, frameHeight, WGPUTextureFormat.RGBA16Float);
            refTransmittance1View = refTransmittance1Texture.CreateView();
            refAccumulateTexture = CreateOitTarget(device, frameWidth, frameHeight, WGPUTextureFormat.RGBA16Float);
            refAccumulateView = refAccumulateTexture.CreateView();

            refCullBindGroup = device.CreateBindGroup(refCullLayout, [BindGroupEntry.TextureView(0, refDepthBoundsView)]);
            refTransmittanceBindGroup = device.CreateBindGroup(refTransmittanceLayout, [BindGroupEntry.TextureView(0, refDepthBoundsCulledView)]);
            refAccumulateBindGroup = device.CreateBindGroup(
                refAccumulateLayout,
                [
                    BindGroupEntry.TextureView(0, refDepthBoundsCulledView),
                    BindGroupEntry.TextureView(1, refTransmittance0View),
                    BindGroupEntry.TextureView(2, refTransmittance1View)
                ]);
            refCompositeBindGroup = device.CreateBindGroup(
                refCompositeLayout,
                [
                    BindGroupEntry.TextureView(0, refAccumulateView),
                    BindGroupEntry.TextureView(1, refTransmittance0View),
                    BindGroupEntry.TextureView(2, refTransmittance1View),
                    BindGroupEntry.TextureView(3, refDepthBoundsCulledView)
                ]);
            refTargetsCreated = true;
        }

        CreateReferenceTargets();

        Span<float> cameraBlock = stackalloc float[16];
        var cameraBytes = new byte[VertexLayout.CameraUniformSize];

        var colour = request.ClearColour;
        var presented = 0;
        var performanceSummary = false;
        var cullStatistics = false;
        var disableCulling = false;
        var frustumPlanes = new System.Numerics.Vector4[6];
        var gapSamples = new double[1 << 17];
        var gapCount = 0;
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

        while (true)
        {
            if (geometryEpoch is not null && geometryEpoch() != appliedGeometryEpoch)
            {
                appliedGeometryEpoch = geometryEpoch();

                // The caller rebuilt every mesh for a new atlas layout, so nothing in the old storage is valid. It is
                // dropped here and the caller replays every mesh in the same frame, which is what keeps a resolution
                // change all or nothing (docs/decisions/0017-reload-invalidation-by-layout.md).
                arena?.Dispose();
                arena = new ChunkGeometryStore(device);

                arenaCutout?.Dispose();
                arenaCutout = new ChunkGeometryStore(device);

                arenaTranslucent?.Dispose();
                arenaTranslucent = new ChunkGeometryStore(device);
                Console.WriteLine("ui: world geometry reset");
            }

            if (atlasSource is not null && atlasSource.Version != appliedAtlasVersion)
            {
                appliedAtlasVersion = atlasSource.Version;

                // The atlas drives every textured face, so replacing it means replacing its texture and the two bind
                // groups that point at it. Whether the geometry must also be rebuilt is the caller's call, because it
                // depends on whether the layout changed (docs/decisions/0017-reload-invalidation-by-layout.md).
                var refreshedAtlas = atlasSource.Atlas ?? TextureData.White;

                bindGroup.Dispose();
                uiBindGroup.Dispose();
                atlasView.Dispose();
                atlasTexture.Dispose();

                atlasTexture = device.CreateTexture(new TextureDescriptor
                {
                    Size = new WGPUExtent3D
                    {
                        width = (uint)refreshedAtlas.Width,
                        height = (uint)refreshedAtlas.Height,
                        depthOrArrayLayers = 1
                    },
                    MipLevelCount = 1,
                    SampleCount = 1,
                    Dimension = WGPUTextureDimension._2D,
                    Format = WGPUTextureFormat.RGBA8Unorm,
                    Usage = TextureUsage.TextureBinding | TextureUsage.CopyDst
                });

                device.Queue.WriteTexture(
                    atlasTexture,
                    refreshedAtlas.Rgba,
                    (uint)(refreshedAtlas.Width * 4),
                    (uint)refreshedAtlas.Height,
                    new WGPUExtent3D
                    {
                        width = (uint)refreshedAtlas.Width,
                        height = (uint)refreshedAtlas.Height,
                        depthOrArrayLayers = 1
                    },
                    0,
                    new WGPUOrigin3D { x = 0, y = 0, z = 0 });

                atlasView = atlasTexture.CreateView();

                bindGroup = device.CreateBindGroup(
                    bindGroupLayout,
                    [
                        BindGroupEntry.TextureView(0, atlasView),
                        BindGroupEntry.Sampler(1, atlasSampler),
                        BindGroupEntry.Buffer(VertexLayout.CameraBinding, cameraBuffer, 0, VertexLayout.CameraUniformSize),
                        BindGroupEntry.Buffer(3, fogBuffer, 0, 32),
                BindGroupEntry.Buffer(Shaders.ShaderBindings.Lightmap, lightmapBuffer, 0, Shaders.ShaderBindings.LightmapBytes)
                    ]);

                uiBindGroup = device.CreateBindGroup(
                    bindGroupLayout,
                    [
                        BindGroupEntry.TextureView(0, atlasView),
                        BindGroupEntry.Sampler(1, atlasSampler),
                        BindGroupEntry.Buffer(VertexLayout.CameraBinding, uiTransformBuffer, 0, VertexLayout.CameraUniformSize),
                        BindGroupEntry.Buffer(3, fogBuffer, 0, 32),
                BindGroupEntry.Buffer(Shaders.ShaderBindings.Lightmap, lightmapBuffer, 0, Shaders.ShaderBindings.LightmapBytes)
                    ]);

                Console.WriteLine($"ui: atlas re-uploaded (version {appliedAtlasVersion}, {refreshedAtlas.Width}x{refreshedAtlas.Height})");
            }

            if (textureSource is not null && textureSource.Version != appliedTextureVersion)
            {
                appliedTextureVersion = textureSource.Version;

                // A reload replaced the images, so the old GPU copies are stale; drop them and upload the new ones.
                uiTextBindGroup.Dispose();
                glyphView.Dispose();
                glyphSheet.Dispose();

                var refreshedGlyph = textureSource.Glyph;

                // Without a glyph atlas text cannot be drawn, so a 1x1 transparent sheet stands in rather than a stale one.
                refreshedGlyph ??= new TextureData(1, 1, new byte[4]);

                glyphSheet = device.CreateTexture(new TextureDescriptor
                {
                    Size = new WGPUExtent3D { width = (uint)refreshedGlyph.Width, height = (uint)refreshedGlyph.Height, depthOrArrayLayers = 1 },
                    MipLevelCount = 1,
                    SampleCount = 1,
                    Dimension = WGPUTextureDimension._2D,
                    Format = WGPUTextureFormat.RGBA8Unorm,
                    Usage = TextureUsage.TextureBinding | TextureUsage.CopyDst
                });

                device.Queue.WriteTexture(
                    glyphSheet,
                    refreshedGlyph.Rgba,
                    (uint)(refreshedGlyph.Width * 4),
                    (uint)refreshedGlyph.Height,
                    new WGPUExtent3D { width = (uint)refreshedGlyph.Width, height = (uint)refreshedGlyph.Height, depthOrArrayLayers = 1 },
                    0,
                    new WGPUOrigin3D { x = 0, y = 0, z = 0 });

                glyphView = glyphSheet.CreateView();
                uiTextBindGroup = device.CreateBindGroup(
                    bindGroupLayout,
                    [
                        BindGroupEntry.TextureView(0, glyphView),
                        BindGroupEntry.Sampler(1, atlasSampler),
                        BindGroupEntry.Buffer(VertexLayout.CameraBinding, uiTransformBuffer, 0, VertexLayout.CameraUniformSize),
                        BindGroupEntry.Buffer(3, fogBuffer, 0, 32),
                BindGroupEntry.Buffer(Shaders.ShaderBindings.Lightmap, lightmapBuffer, 0, Shaders.ShaderBindings.LightmapBytes)
                    ]);

                foreach (var staleBindGroup in spriteBindGroups)
                {
                    staleBindGroup.Dispose();
                }

                foreach (var staleView in spriteViews)
                {
                    staleView.Dispose();
                }

                foreach (var staleSheet in spriteSheets)
                {
                    staleSheet.Dispose();
                }

                spriteBindGroups.Clear();
                spriteViews.Clear();
                spriteSheets.Clear();

                foreach (var spriteData in textureSource.Sprites)
                {
                    var refreshedSheet = device.CreateTexture(new TextureDescriptor
                    {
                        Size = new WGPUExtent3D { width = (uint)spriteData.Width, height = (uint)spriteData.Height, depthOrArrayLayers = 1 },
                        MipLevelCount = 1,
                        SampleCount = 1,
                        Dimension = WGPUTextureDimension._2D,
                        Format = WGPUTextureFormat.RGBA8Unorm,
                        Usage = TextureUsage.TextureBinding | TextureUsage.CopyDst
                    });

                    device.Queue.WriteTexture(
                        refreshedSheet,
                        spriteData.Rgba,
                        (uint)(spriteData.Width * 4),
                        (uint)spriteData.Height,
                        new WGPUExtent3D { width = (uint)spriteData.Width, height = (uint)spriteData.Height, depthOrArrayLayers = 1 },
                        0,
                        new WGPUOrigin3D { x = 0, y = 0, z = 0 });

                    var refreshedView = refreshedSheet.CreateView();

                    spriteBindGroups.Add(device.CreateBindGroup(
                        bindGroupLayout,
                        [
                            BindGroupEntry.TextureView(0, refreshedView),
                            BindGroupEntry.Sampler(1, atlasSampler),
                            BindGroupEntry.Buffer(VertexLayout.CameraBinding, uiTransformBuffer, 0, VertexLayout.CameraUniformSize),
                            BindGroupEntry.Buffer(3, fogBuffer, 0, 32),
                BindGroupEntry.Buffer(Shaders.ShaderBindings.Lightmap, lightmapBuffer, 0, Shaders.ShaderBindings.LightmapBytes)
                        ]));

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
                surface.Configure(device, format, TextureUsage.RenderAttachment, frameWidth, frameHeight);
                depthView.Dispose();
                depthTexture.Dispose();
                depthTexture = CreateDepthTexture(device, frameWidth, frameHeight);
                depthView = depthTexture.CreateView();

                CreateReferenceTargets();

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
                    uiVertex.Dispose();
                    uiVertexCapacity = Math.Max(neededVertex * 2, MinimumBufferBytes);
                    uiVertex = CreateGeometryBuffer(device, uiVertexCapacity, BufferUsage.Vertex | BufferUsage.CopyDst);
                }

                if (neededIndex > uiIndexCapacity)
                {
                    uiIndex.Dispose();
                    uiIndexCapacity = Math.Max(neededIndex * 2, MinimumBufferBytes);
                    uiIndex = CreateGeometryBuffer(device, uiIndexCapacity, BufferUsage.Index | BufferUsage.CopyDst);
                }

                device.Queue.WriteBuffer(uiVertex, 0, ui.Vertices);
                device.Queue.WriteBuffer(uiIndex, 0, ui.Indices);

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

                device.Queue.WriteBuffer(uiTransformBuffer, 0, transformBytes);
                uiIndexCount = (uint)ui.Indices.Length;
                uiRanges = ui.Ranges.ToArray();
            }

            if (dynamicGeometry is not null && dynamicGeometry() is { IndexCount: > 0 } dynamic)
            {
                var neededVertex = (ulong)dynamic.VertexFloats * sizeof(float);
                var neededIndex = (ulong)dynamic.IndexCount * sizeof(uint);

                if (neededVertex > dynamicVertexCapacity)
                {
                    dynamicVertex.Dispose();
                    dynamicVertexCapacity = Math.Max(neededVertex * 2, MinimumBufferBytes);
                    dynamicVertex = CreateGeometryBuffer(device, dynamicVertexCapacity, BufferUsage.Vertex | BufferUsage.CopyDst);
                }

                if (neededIndex > dynamicIndexCapacity)
                {
                    dynamicIndex.Dispose();
                    dynamicIndexCapacity = Math.Max(neededIndex * 2, MinimumBufferBytes);
                    dynamicIndex = CreateGeometryBuffer(device, dynamicIndexCapacity, BufferUsage.Index | BufferUsage.CopyDst);
                }

                device.Queue.WriteBuffer(dynamicVertex, 0, dynamic.Vertices.AsSpan(0, dynamic.VertexFloats));
                device.Queue.WriteBuffer(dynamicIndex, 0, dynamic.Indices.AsSpan(0, dynamic.IndexCount));
                dynamicIndexCount = (uint)dynamic.IndexCount;
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
                    vertexBuffer = CreateGeometryBuffer(device, vertexCapacity, BufferUsage.Vertex | BufferUsage.CopyDst);
                }

                if (neededIndex > indexCapacity)
                {
                    retiredIndex.Add(indexBuffer);
                    indexCapacity = Math.Max(neededIndex * 2, MinimumBufferBytes);
                    indexBuffer = CreateGeometryBuffer(device, indexCapacity, BufferUsage.Index | BufferUsage.CopyDst);
                }

                device.Queue.WriteBuffer(vertexBuffer, 0, rebuilt.Vertices.AsSpan());
                device.Queue.WriteBuffer(indexBuffer, 0, rebuilt.Indices.AsSpan());

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
            var nowMs = frameClock.Elapsed.TotalMilliseconds;
            performanceSummary = currentRequest.Diagnostics?.PerformanceSummary == true;
            cullStatistics = currentRequest.Diagnostics?.CullStatistics == true;
            disableCulling = currentRequest.Diagnostics?.DisableCulling == true;
            var gapMs = nowMs - lastFrameAt;

            if (gapCount < gapSamples.Length)
            {
                gapSamples[gapCount++] = gapMs;
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

            // Brew's NeedsReconfigure wraps around on the uint enum (SuccessOptimal reports as
            // needing a reconfigure), so the statuses are compared explicitly instead.
            if (acquired.Status is WGPUSurfaceGetCurrentTextureStatus.Outdated or WGPUSurfaceGetCurrentTextureStatus.Lost)
            {
                // The surface output must be released before the surface is reconfigured.
                acquired.Texture.Dispose();
                surface.Configure(device, format, TextureUsage.RenderAttachment, frameWidth, frameHeight);
                continue;
            }

            if (!acquired.IsUsable)
            {
                Console.WriteLine($"window: acquire status {acquired.Status}");
                acquired.Texture.Dispose();
                break;
            }

            using var view = acquired.Texture.CreateView();
            // The camera is sampled once per frame, before the encoder, because the cull pass needs the view projection
            // and a render pass cannot be interrupted to record compute.
            var cameraStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            var (eye, target) = cameraPerFrame is null
                ? (request.CameraPosition, request.CameraTarget)
                : cameraPerFrame(window);

            // Sun/moon/stars are built relative to the initial camera; keep them centred as the player moves.
            var celestialDelta = eye - request.CameraPosition;

            // Sample the cloud draw once: the OIT stages need its vertices before the main transparency passes, and the
            // non-OIT path reuses the same draw later.
            var cloudDrawFrame = cloudsPerFrame?.Invoke();
            if (cloudOit is not null && cloudDrawFrame is { } cloudForOit)
            {
                cloudOit.Update(cloudForOit.Vertices, cloudForOit.Indices, cloudForOit.Model, cloudForOit.Tint);
            }

            var encoder = device.CreateCommandEncoder();
            var frameColour = clearColourPerFrame?.Invoke() ?? colour;
            var pass = encoder.BeginRenderPass(
            [
                new ColorAttachment
                {
                    View = view,
                    LoadOp = WGPULoadOp.Clear,
                    StoreOp = WGPUStoreOp.Store,
                    ClearValue = new WGPUColor { r = frameColour.X, g = frameColour.Y, b = frameColour.Z, a = frameColour.W }
                }
            ],
            DepthStencilAttachment.DepthClear(depthView));

            // Sky first, over the cleared target; world geometry then draws on top of it. When the caller supplies a flat
            // SKY_COLOR through the clear colour, the gradient sky is skipped.
            if (!currentRequest.SkipGradientSky)
            {
                pass.SetPipeline(skyPipeline);
                pass.Draw(3);
            }

            // Sun/moon (textured) then sunrise/stars (position-colour), still before terrain so world geometry covers them.
            // Sun/moon/star/sunrise geometry depends on the world clock, so rebuild the small draw set each frame while
            // keeping the loaded textures/pipelines.
            if (celestialPerFrame?.Invoke() is { } celestialUpdate)
            {
                celestial?.SetDraws(celestialUpdate.Draws);
                celestialColour?.SetDraws(celestialUpdate.Colors);
            }

            // Apply the camera delta AFTER any per-frame draw rebuild: a freshly built draw uses the base (initial) camera
            // model, so translating first would be overwritten and the bodies would stay anchored in the world.
            celestial?.UpdateCamera(celestialDelta);
            celestialColour?.UpdateCamera(celestialDelta);

            celestial?.Draw(pass);
            celestialColour?.Draw(pass);

            // Clouds are drawn after the transparent terrain (below), with depth testing, matching the reference order.

            pass.SetPipeline(pipeline);
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

            device.Queue.WriteBuffer(cameraBuffer, 0, cameraBytes.AsSpan());

            // Per-frame lightmap parameters: day/night changes only this block, never the mesh.
            var frameLightmapBytes = new byte[Shaders.ShaderBindings.LightmapBytes];
            LightmapUniformCodec.Write(lightmapPerFrame?.Invoke() ?? currentRequest.Lightmap, frameLightmapBytes);
            device.Queue.WriteBuffer(lightmapBuffer, 0, frameLightmapBytes.AsSpan());

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
            device.Queue.WriteBuffer(fogBuffer, 0, frameFogBytes.AsSpan());

            pass.SetBindGroup(0, bindGroup);

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
            }

            if (arenaCutout is { ChunkCount: > 0 })
            {
                pass.SetPipeline(cutoutPipeline);
                pass.SetBindGroup(0, bindGroup);

                if (disableCulling)
                {
                    arenaCutout.Draw(pass);
                }
                else
                {
                    arenaCutout.Draw(pass, frustumPlanes);
                }
            }

            // Translucent terrain is not drawn here: it writes the order independent targets in the next pass, so that the
            // blended result does not depend on triangle order (docs/specs/render/pipeline.md 15).

            // The reference draws every section from the same per section buffers; it has no separate static copy of the
            // world. Drawing the preloaded combined mesh next to the arena kept the first few chunks on a second path with
            // its own colour, so the static copy is used only until the arena holds anything.
            var useStaticWorld = arena is null || arena.ChunkCount == 0;

            if (dynamicIndexCount > 0)
            {
                pass.SetVertexBuffer(0, dynamicVertex, 0, dynamicVertexCapacity);
                pass.SetIndexBuffer(dynamicIndex, WGPUIndexFormat.Uint32, 0, (ulong)dynamicIndexCount * sizeof(uint));
                pass.DrawIndexed(dynamicIndexCount, 1, 0, 0, 1);
            }
            else if (useStaticWorld)
            {
                pass.SetVertexBuffer(0, vertexBuffer);
                pass.SetIndexBuffer(indexBuffer, WGPUIndexFormat.Uint32);
                pass.DrawIndexed((uint)currentRequest.Indices.Length);
            }

            if (useStaticWorld && currentRequest.CutoutVertices is { Length: > 0 } windowCutoutVertices &&
                currentRequest.CutoutIndices is { Length: > 0 } windowCutoutIndices)
            {
                using var windowCutoutVertexBuffer = device.CreateBufferInit(
                    new BufferInitDescriptor { Usage = BufferUsage.Vertex },
                    windowCutoutVertices.AsSpan());
                using var windowCutoutIndexBuffer = device.CreateBufferInit(
                    new BufferInitDescriptor { Usage = BufferUsage.Index },
                    windowCutoutIndices.AsSpan());

                pass.SetPipeline(cutoutPipeline);
                pass.SetBindGroup(0, bindGroup);
                pass.SetVertexBuffer(0, windowCutoutVertexBuffer);
                pass.SetIndexBuffer(windowCutoutIndexBuffer, WGPUIndexFormat.Uint32);
                pass.DrawIndexed((uint)windowCutoutIndices.Length);
            }

            // The main pass ends here: sky, opaque terrain and the cutout layer (which shares the opaque depth) have all
            // been drawn. Translucent terrain and the interface each get their own pass so the order independent resolve
            // can run between them.
            pass.End();

            // Reference transparency (docs/specs/render/oit-reference.md): depth bounds, a culling pass, wavelet
            // transmittance, accumulation and a composite that writes depth. The three stages redraw the same
            // translucent geometry, so no sort is needed at all.
                var hasRefStatic = useStaticWorld
                    && currentRequest.TranslucentVertices is { Length: > 0 }
                    && currentRequest.TranslucentIndices is { Length: > 0 };
                WgpuBuffer refStaticVertexBuffer = default;
                WgpuBuffer refStaticIndexBuffer = default;
                var refStaticIndexCount = 0u;

                if (hasRefStatic)
                {
                    refStaticVertexBuffer = device.CreateBufferInit(
                        new BufferInitDescriptor { Usage = BufferUsage.Vertex },
                        currentRequest.TranslucentVertices!.AsSpan());
                    refStaticIndexBuffer = device.CreateBufferInit(
                        new BufferInitDescriptor { Usage = BufferUsage.Index },
                        currentRequest.TranslucentIndices!.AsSpan());
                    refStaticIndexCount = (uint)currentRequest.TranslucentIndices!.Length;
                }

                void DrawReferenceGeometry(Brew.Wgpu.RenderPassEncoder p)
                {
                    if (arenaTranslucent is not null && arenaTranslucent.ChunkCount > 0)
                    {
                        arenaTranslucent.DrawFarthestFirst(p, eye);
                    }

                    if (hasRefStatic && refStaticIndexCount > 0)
                    {
                        p.SetVertexBuffer(0, refStaticVertexBuffer);
                        p.SetIndexBuffer(refStaticIndexBuffer, WGPUIndexFormat.Uint32);
                        p.DrawIndexed(refStaticIndexCount);
                    }
                }

                var depthLoadStore = new DepthStencilAttachment
                {
                    View = depthView,
                    DepthLoadOp = WGPULoadOp.Load,
                    DepthStoreOp = WGPUStoreOp.Store
                };

                // Stage 1: depth bounds. Cleared very negative so MAX blending keeps the closest and furthest.
                var boundsPass = encoder.BeginRenderPass(
                [
                    new ColorAttachment
                    {
                        View = refDepthBoundsView,
                        LoadOp = WGPULoadOp.Clear,
                        StoreOp = WGPUStoreOp.Store,
                        ClearValue = new WGPUColor { r = -65504f, g = -65504f, b = -65504f, a = -65504f }
                    }
                ],
                depthLoadStore);
                boundsPass.SetPipeline(refDepthBoundsPipeline);
                boundsPass.SetBindGroup(0, bindGroup);
                DrawReferenceGeometry(boundsPass);
                if (cloudOit is not null)
                {
                    boundsPass.SetPipeline(refDepthBoundsPipeline);
                    boundsPass.SetBindGroup(0, cloudOit.BindGroup);
                    cloudOit.DrawGeometry(boundsPass);
                }
                boundsPass.End();

                // Stage 1b: clip the furthest bound to the nearest opaque surface.
                var cullPass = encoder.BeginRenderPass(
                [
                    new ColorAttachment
                    {
                        View = refDepthBoundsCulledView,
                        LoadOp = WGPULoadOp.Clear,
                        StoreOp = WGPUStoreOp.Store,
                        ClearValue = new WGPUColor { r = 0f, g = 0f, b = 0f, a = 0f }
                    }
                ],
                depthLoadStore);
                cullPass.SetPipeline(refCullPipeline);
                cullPass.SetBindGroup(0, refCullBindGroup);
                cullPass.Draw(3);
                cullPass.End();

                // Stage 2: wavelet transmittance, additively blended into two RGBA16F targets.
                var transmittancePass = encoder.BeginRenderPass(
                [
                    new ColorAttachment
                    {
                        View = refTransmittance0View,
                        LoadOp = WGPULoadOp.Clear,
                        StoreOp = WGPUStoreOp.Store,
                        ClearValue = new WGPUColor { r = 0f, g = 0f, b = 0f, a = 0f }
                    },
                    new ColorAttachment
                    {
                        View = refTransmittance1View,
                        LoadOp = WGPULoadOp.Clear,
                        StoreOp = WGPUStoreOp.Store,
                        ClearValue = new WGPUColor { r = 0f, g = 0f, b = 0f, a = 0f }
                    }
                ],
                depthLoadStore);
                transmittancePass.SetPipeline(refTransmittancePipeline);
                transmittancePass.SetBindGroup(0, bindGroup);
                transmittancePass.SetBindGroup(1, refTransmittanceBindGroup);
                DrawReferenceGeometry(transmittancePass);
                if (cloudOit is not null)
                {
                    transmittancePass.SetPipeline(refTransmittancePipeline);
                    transmittancePass.SetBindGroup(0, cloudOit.BindGroup);
                    transmittancePass.SetBindGroup(1, refTransmittanceBindGroup);
                    cloudOit.DrawGeometry(transmittancePass);
                }
                transmittancePass.End();

                // Stage 3: accumulate colour weighted by the transmittance in front of each fragment.
                var accumulatePass = encoder.BeginRenderPass(
                [
                    new ColorAttachment
                    {
                        View = refAccumulateView,
                        LoadOp = WGPULoadOp.Clear,
                        StoreOp = WGPUStoreOp.Store,
                        ClearValue = new WGPUColor { r = 0f, g = 0f, b = 0f, a = 0f }
                    }
                ],
                depthLoadStore);
                accumulatePass.SetPipeline(refAccumulatePipeline);
                accumulatePass.SetBindGroup(0, bindGroup);
                accumulatePass.SetBindGroup(1, refAccumulateBindGroup);
                DrawReferenceGeometry(accumulatePass);
                if (cloudOit is not null)
                {
                    accumulatePass.SetPipeline(refAccumulatePipeline);
                    accumulatePass.SetBindGroup(0, cloudOit.BindGroup);
                    accumulatePass.SetBindGroup(1, refAccumulateBindGroup);
                    cloudOit.DrawGeometry(accumulatePass);
                }
                accumulatePass.End();

                // Composite over the frame, writing the nearest translucent depth.
                var compositePass = encoder.BeginRenderPass(
                [
                    new ColorAttachment { View = view, LoadOp = WGPULoadOp.Load, StoreOp = WGPUStoreOp.Store }
                ],
                new DepthStencilAttachment { View = depthView, DepthLoadOp = WGPULoadOp.Load, DepthStoreOp = WGPUStoreOp.Store });
                compositePass.SetPipeline(refCompositePipeline);
                compositePass.SetBindGroup(0, refCompositeBindGroup);
                compositePass.Draw(3);
                compositePass.End();

                if (hasRefStatic)
                {
                    refStaticVertexBuffer.Dispose();
                    refStaticIndexBuffer.Dispose();
                }

                if (cullStatistics && (presented % 60) == 0)
                {
                    Console.WriteLine(
                        $"cull: chunks={arena?.ChunkCount ?? 0} culled={arena?.CulledLastFrame ?? 0} "
                        + $"cutout={arenaCutout?.ChunkCount ?? 0} translucent={arenaTranslucent?.ChunkCount ?? 0} (reference oit)");
                }
            // (The weighted-blended OIT path that used to live here was shelved for its visual quality and is no longer
            // compiled; the reference OIT above is the only transparency path.)

            // Clouds come after the opaque and transparent terrain, with depth testing so terrain and the cloud layer
            // occlude each other correctly (the reference draws them in executeClassicTransparency).
            if (cloudColour is not null && cloudOit is null)
            {
                var cloudPass = encoder.BeginRenderPass(
                [
                    new ColorAttachment { View = view, LoadOp = WGPULoadOp.Load, StoreOp = WGPUStoreOp.Store }
                ],
                new DepthStencilAttachment { View = depthView, DepthLoadOp = WGPULoadOp.Load, DepthStoreOp = WGPUStoreOp.Store });

                if (cloudDrawFrame is { } cloudDraw)
                {
                    cloudColour.SetDraws([cloudDraw]);
                }

                cloudColour.Draw(cloudPass);
                cloudPass.End();
            }

            // Pass 4: the interface draws last, over the resolved frame, in its own pass so it never participates in the
            // world depth test or the transparency resolve.
            var uiPass = encoder.BeginRenderPass(
            [
                new ColorAttachment
                {
                    View = view,
                    LoadOp = WGPULoadOp.Load,
                    StoreOp = WGPUStoreOp.Store
                }
            ],
            new DepthStencilAttachment
            {
                View = depthView,
                DepthLoadOp = WGPULoadOp.Load,
                DepthStoreOp = WGPUStoreOp.Store
            });

            if (uiIndexCount > 0 && uiRanges.Length > 0)
            {
                uiPass.SetVertexBuffer(0, uiVertex, 0, uiVertexCapacity);
                uiPass.SetIndexBuffer(uiIndex, WGPUIndexFormat.Uint32, 0, (ulong)uiIndexCount * sizeof(uint));

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
                            uiPass.SetPipeline(uiTextPipeline);
                            uiPass.SetBindGroup(0, uiTextBindGroup);
                        }
                        else if (range.Material == UiMaterial.Sprite &&
                                 range.SpriteIndex >= 0 &&
                                 range.SpriteIndex < spriteBindGroups.Count)
                        {
                            uiPass.SetPipeline(uiSpritePipeline);
                            uiPass.SetBindGroup(0, spriteBindGroups[range.SpriteIndex]);
                        }
                        else
                        {
                            uiPass.SetPipeline(uiPipeline);
                            uiPass.SetBindGroup(0, uiBindGroup);
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

                    uiPass.SetScissorRect(
                        (uint)left,
                        (uint)top,
                        (uint)(right - left),
                        (uint)(bottom - top));

                    uiPass.DrawIndexed((uint)range.IndexCount, 1, (uint)range.FirstIndex, 0, 1);
                }
            }

            uiPass.End();

            var commands = encoder.Finish();
            device.Queue.Submit(commands);
            surface.Present();
            commands.Dispose();
            encoder.Dispose();

            // The acquired texture must be released each frame; leaving it alive makes the next acquire fail
            // when the swapchain reuses its semaphore.
            acquired.Texture.Dispose();

            onFrame?.Invoke(presented, window);
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
            buffer.Dispose();
        }

        foreach (var buffer in retiredIndex)
        {
            buffer.Dispose();
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
        if (arena is not null)
        {
            Console.WriteLine(
                $"window: arena {arena.ChunkCount} chunks, presized={arena.Presized}, "
                + $"uploaded {arena.UploadedBytes / (1024d * 1024d):F1} MB, "
                + $"{arena.FullRebuilds} full rebuilds");
            arena.Dispose();
        }

        Console.WriteLine(
            $"window: rebuild {rebuildCount} frames, avg {(rebuildCount == 0 ? 0 : rebuildTotalMs / rebuildCount):F1} ms, "
            + $"worst {worstRebuildMs:F1} ms, uploaded {uploadBytes / (1024d * 1024d):F1} MB; "
            + $"camera avg {(presented == 0 ? 0 : cameraTotalMs / presented):F1} ms, worst {worstCameraMs:F1} ms");

        dynamicVertex.Dispose();
        dynamicIndex.Dispose();
        uiVertex.Dispose();
        uiIndex.Dispose();
        uiTransformBuffer.Dispose();

        vertexBuffer.Dispose();
        depthView.Dispose();
        depthTexture.Dispose();
        refCullBindGroup.Dispose();
        refTransmittanceBindGroup.Dispose();
        refAccumulateBindGroup.Dispose();
        refCompositeBindGroup.Dispose();
        refDepthBoundsView.Dispose();
        refDepthBoundsTexture.Dispose();
        refDepthBoundsCulledView.Dispose();
        refDepthBoundsCulledTexture.Dispose();
        refTransmittance0View.Dispose();
        refTransmittance0Texture.Dispose();
        refTransmittance1View.Dispose();
        refTransmittance1Texture.Dispose();
        refAccumulateView.Dispose();
        refAccumulateTexture.Dispose();
        indexBuffer.Dispose();

        return format;
    }

    /// <summary>
    /// Uploads clip-space vertices built for the current framebuffer aspect ratio. The staging
    /// array is pooled, so a resize (or a frame with a moving camera) does not allocate megabytes.
    /// </summary>
    /// <summary>Smallest geometry buffer, so tiny meshes do not force a reallocation on the next change.</summary>
    private const ulong MinimumBufferBytes = 1 << 20;

    private static WgpuBuffer CreateGeometryBuffer(Device device, ulong sizeBytes, BufferUsage usage) =>
        device.CreateBuffer(new BufferDescriptor { Size = sizeBytes, Usage = usage });

    private static Texture CreateDepthTexture(Device device, uint width, uint height) =>
        device.CreateTexture(new TextureDescriptor
        {
            Size = new WGPUExtent3D { width = width, height = height, depthOrArrayLayers = 1 },
            MipLevelCount = 1,
            SampleCount = 1,
            Dimension = WGPUTextureDimension._2D,
            Format = WGPUTextureFormat.Depth32Float,
            Usage = TextureUsage.RenderAttachment
        });

    /// <summary>Creates one order independent transparency target: renderable and sampleable, at frame size.</summary>
    private static Texture CreateOitTarget(Device device, uint width, uint height, WGPUTextureFormat format) =>
        device.CreateTexture(new TextureDescriptor
        {
            Size = new WGPUExtent3D { width = width, height = height, depthOrArrayLayers = 1 },
            MipLevelCount = 1,
            SampleCount = 1,
            Dimension = WGPUTextureDimension._2D,
            Format = format,
            Usage = TextureUsage.RenderAttachment | TextureUsage.TextureBinding
        });

    private static SurfaceSource ToSurfaceSource(NativeSurfaceSource source) => source.Tag switch
    {
        NativeSurfaceKind.Windows => SurfaceSource.WindowsHwnd(source.Handle1, source.Handle0),
        NativeSurfaceKind.X11 => SurfaceSource.XlibWindow(source.Handle0, (ulong)source.Handle1),
        NativeSurfaceKind.Wayland => SurfaceSource.WaylandSurface(source.Handle0, source.Handle1),
        NativeSurfaceKind.MetalLayer => SurfaceSource.MetalLayer(source.Handle0),
        _ => throw new PlatformNotSupportedException($"no surface source for {source.Tag}")
    };

    /// <summary>Layout for a bind group of <paramref name="textureCount"/> sampled textures, fetched with texelFetch.</summary>
    private static BindGroupLayout CreateOitTextureLayout(Device device, int textureCount)
    {
        var entries = new BindGroupLayoutEntry[textureCount];

        for (var index = 0; index < textureCount; index++)
        {
            entries[index] = BindGroupLayoutEntry.Texture(
                (uint)index,
                ShaderStage.Fragment,
                WGPUTextureSampleType.UnfilterableFloat,
                WGPUTextureViewDimension._2D,
                false);
        }

        return device.CreateBindGroupLayout(entries);
    }

    /// <summary>
    /// Reference transparency stage 1: record the per-pixel depth range of the translucent layer. RGBA32F with a MAX
    /// blend, so the closest and furthest bounds survive in different channels.
    /// </summary>
    private static RenderPipeline CreateDepthBoundsPipeline(Device device, PipelineLayout layout)
    {
        using var shader = device.CreateShaderModule(new ShaderModuleDescriptor
        {
            Source = ShaderSource.FromWgsl(System.Text.Encoding.UTF8.GetBytes(Shaders.ShaderLibrary.Load("oit_reference")))
        });

        return device.CreateRenderPipeline(
            shader,
            "vs_main"u8,
            shader,
            "fs_depth_bounds"u8,
            [
                new ColorTargetState
                {
                    Format = WGPUTextureFormat.RGBA16Float,
                    WriteMask = ColorWriteMask.All,
                    Blend = new WGPUBlendState
                    {
                        color = new WGPUBlendComponent { operation = WGPUBlendOperation.Max, srcFactor = WGPUBlendFactor.One, dstFactor = WGPUBlendFactor.One },
                        alpha = new WGPUBlendComponent { operation = WGPUBlendOperation.Max, srcFactor = WGPUBlendFactor.One, dstFactor = WGPUBlendFactor.One }
                    }
                }
            ],
            layout: layout,
            primitive: PrimitiveState.Default with { CullMode = WGPUCullMode.Back, FrontFace = WGPUFrontFace.CCW },
            multisample: MultisampleState.Default,
            depthStencil: DepthStencilState.Depth(WGPUTextureFormat.Depth32Float, WGPUCompareFunction.LessEqual, write: false),
            vertexStride: (ulong)VertexLayout.Stride,
            vertexStepMode: WGPUVertexStepMode.Vertex,
            vertexAttributes: VertexLayout.Attributes());
    }

    /// <summary>Reference transparency stage 2: wavelet-transmittance coefficients, additively blended into two targets.</summary>
    private static RenderPipeline CreateTransmittancePipeline(Device device, PipelineLayout layout)
    {
        using var shader = device.CreateShaderModule(new ShaderModuleDescriptor
        {
            Source = ShaderSource.FromWgsl(System.Text.Encoding.UTF8.GetBytes(Shaders.ShaderLibrary.Load("oit_reference")))
        });

        static ColorTargetState Additive(WGPUTextureFormat format) => new()
        {
            Format = format,
            WriteMask = ColorWriteMask.All,
            Blend = new WGPUBlendState
            {
                color = new WGPUBlendComponent { operation = WGPUBlendOperation.Add, srcFactor = WGPUBlendFactor.One, dstFactor = WGPUBlendFactor.One },
                alpha = new WGPUBlendComponent { operation = WGPUBlendOperation.Add, srcFactor = WGPUBlendFactor.One, dstFactor = WGPUBlendFactor.One }
            }
        };

        return device.CreateRenderPipeline(
            shader,
            "vs_main"u8,
            shader,
            "fs_transmittance"u8,
            [Additive(WGPUTextureFormat.RGBA16Float), Additive(WGPUTextureFormat.RGBA16Float)],
            layout: layout,
            primitive: PrimitiveState.Default with { CullMode = WGPUCullMode.Back, FrontFace = WGPUFrontFace.CCW },
            multisample: MultisampleState.Default,
            depthStencil: DepthStencilState.Depth(WGPUTextureFormat.Depth32Float, WGPUCompareFunction.LessEqual, write: false),
            vertexStride: (ulong)VertexLayout.Stride,
            vertexStepMode: WGPUVertexStepMode.Vertex,
            vertexAttributes: VertexLayout.Attributes());
    }

    /// <summary>Reference transparency stage 3: colour weighted by the transmittance in front of each fragment.</summary>
    private static RenderPipeline CreateAccumulatePipeline(Device device, PipelineLayout layout)
    {
        using var shader = device.CreateShaderModule(new ShaderModuleDescriptor
        {
            Source = ShaderSource.FromWgsl(System.Text.Encoding.UTF8.GetBytes(Shaders.ShaderLibrary.Load("oit_reference")))
        });

        return device.CreateRenderPipeline(
            shader,
            "vs_main"u8,
            shader,
            "fs_accumulate"u8,
            [
                new ColorTargetState
                {
                    Format = WGPUTextureFormat.RGBA16Float,
                    WriteMask = ColorWriteMask.All,
                    Blend = new WGPUBlendState
                    {
                        color = new WGPUBlendComponent { operation = WGPUBlendOperation.Add, srcFactor = WGPUBlendFactor.One, dstFactor = WGPUBlendFactor.One },
                        alpha = new WGPUBlendComponent { operation = WGPUBlendOperation.Add, srcFactor = WGPUBlendFactor.One, dstFactor = WGPUBlendFactor.One }
                    }
                }
            ],
            layout: layout,
            primitive: PrimitiveState.Default with { CullMode = WGPUCullMode.Back, FrontFace = WGPUFrontFace.CCW },
            multisample: MultisampleState.Default,
            depthStencil: DepthStencilState.Depth(WGPUTextureFormat.Depth32Float, WGPUCompareFunction.LessEqual, write: false),
            vertexStride: (ulong)VertexLayout.Stride,
            vertexStepMode: WGPUVertexStepMode.Vertex,
            vertexAttributes: VertexLayout.Attributes());
    }

    /// <summary>Reference transparency: clip the far depth bound to the nearest opaque surface.</summary>
    private static RenderPipeline CreateCullPipeline(Device device, PipelineLayout layout)
    {
        using var shader = device.CreateShaderModule(new ShaderModuleDescriptor
        {
            Source = ShaderSource.FromWgsl(System.Text.Encoding.UTF8.GetBytes(Shaders.ShaderLibrary.Load("oit_depth_bounds_cull")))
        });

        return device.CreateRenderPipeline(
            shader,
            "vs_fullscreen"u8,
            shader,
            "fs_cull"u8,
            [new ColorTargetState(WGPUTextureFormat.RGBA16Float)],
            layout: layout,
            primitive: PrimitiveState.Default with { CullMode = WGPUCullMode.None },
            multisample: MultisampleState.Default,
            depthStencil: DepthStencilState.Depth(WGPUTextureFormat.Depth32Float, WGPUCompareFunction.Always, write: false),
            vertexStride: 0,
            vertexStepMode: WGPUVertexStepMode.Vertex,
            vertexAttributes: []);
    }

    /// <summary>Reference transparency composite: premultiplied over the frame, writing the nearest translucent depth.</summary>
    private static RenderPipeline CreateCompositePipeline(Device device, WGPUTextureFormat format, PipelineLayout layout)
    {
        using var shader = device.CreateShaderModule(new ShaderModuleDescriptor
        {
            Source = ShaderSource.FromWgsl(System.Text.Encoding.UTF8.GetBytes(Shaders.ShaderLibrary.Load("oit_composite")))
        });

        return device.CreateRenderPipeline(
            shader,
            "vs_fullscreen"u8,
            shader,
            "fs_composite"u8,
            [
                new ColorTargetState
                {
                    Format = format,
                    WriteMask = ColorWriteMask.All,
                    Blend = new WGPUBlendState
                    {
                        color = new WGPUBlendComponent { operation = WGPUBlendOperation.Add, srcFactor = WGPUBlendFactor.One, dstFactor = WGPUBlendFactor.OneMinusSrcAlpha },
                        alpha = new WGPUBlendComponent { operation = WGPUBlendOperation.Add, srcFactor = WGPUBlendFactor.One, dstFactor = WGPUBlendFactor.OneMinusSrcAlpha }
                    }
                }
            ],
            layout: layout,
            primitive: PrimitiveState.Default with { CullMode = WGPUCullMode.None },
            multisample: MultisampleState.Default,
            depthStencil: DepthStencilState.Depth(WGPUTextureFormat.Depth32Float, WGPUCompareFunction.Always, write: true),
            vertexStride: 0,
            vertexStepMode: WGPUVertexStepMode.Vertex,
            vertexAttributes: []);
    }

    /// <summary>Builds the sky pipeline: a full-screen triangle with no vertex buffer and no depth writes.</summary>
    /// <param name="device">Device.</param>
    /// <param name="format">Colour target format.</param>
    private static RenderPipeline CreateSkyPipeline(Device device, WGPUTextureFormat format)
    {
        using var shader = device.CreateShaderModule(new ShaderModuleDescriptor
        {
            Source = ShaderSource.FromWgsl(System.Text.Encoding.UTF8.GetBytes(OffscreenRenderer.SkyShaderSourceText))
        });

        return device.CreateRenderPipeline(
            shader,
            "vs_sky"u8,
            shader,
            "fs_sky"u8,
            [new ColorTargetState(format)],
            primitive: PrimitiveState.Default with { CullMode = WGPUCullMode.None },
            multisample: MultisampleState.Default,
            depthStencil: DepthStencilState.Depth(WGPUTextureFormat.Depth32Float, WGPUCompareFunction.Always, write: false),
            vertexStride: 0,
            vertexStepMode: WGPUVertexStepMode.Vertex,
            vertexAttributes: []);
    }

    /// <summary>
    /// The cutout pipeline: alpha testing instead of blending, depth writes on.
    /// </summary>
    private static RenderPipeline CreateCutoutPipeline(Device device, WGPUTextureFormat format, PipelineLayout layout)
    {
        using var shader = device.CreateShaderModule(new ShaderModuleDescriptor
        {
            Source = ShaderSource.FromWgsl(System.Text.Encoding.UTF8.GetBytes(OffscreenRenderer.ShaderSourceText))
        });

        return device.CreateRenderPipeline(
            shader,
            "vs_main"u8,
            shader,
            "fs_cutout"u8,
            [new ColorTargetState(format)],
            layout: layout,
            primitive: PrimitiveState.Default with { CullMode = WGPUCullMode.Back, FrontFace = WGPUFrontFace.CCW },
            multisample: MultisampleState.Default,
            depthStencil: DepthStencilState.Depth(WGPUTextureFormat.Depth32Float),
            vertexStride: (ulong)VertexLayout.Stride,
            vertexStepMode: WGPUVertexStepMode.Vertex,
            vertexAttributes: VertexLayout.Attributes());
    }

    private static RenderPipeline CreatePipeline(Device device, WGPUTextureFormat format, PipelineLayout layout)
    {
        using var shader = device.CreateShaderModule(new ShaderModuleDescriptor
        {
            Source = ShaderSource.FromWgsl(System.Text.Encoding.UTF8.GetBytes(OffscreenRenderer.ShaderSourceText))
        });

        return device.CreateRenderPipeline(
            shader,
            "vs_main"u8,
            shader,
            "fs_main"u8,
            [new ColorTargetState(format)],
            layout: layout,
            primitive: PrimitiveState.Default with { CullMode = WGPUCullMode.Back, FrontFace = WGPUFrontFace.CCW },
            multisample: MultisampleState.Default,
            depthStencil: DepthStencilState.Depth(WGPUTextureFormat.Depth32Float),
            vertexStride: (ulong)VertexLayout.Stride,
            vertexStepMode: WGPUVertexStepMode.Vertex,
            vertexAttributes: VertexLayout.Attributes());
    }
}
