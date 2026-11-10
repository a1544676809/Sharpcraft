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
        var adapter = instance.RequestAdapterBlocking(surface);
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

        // One sampler per address/filter state for the whole renderer (T-784 S1b): the reference keeps a SamplerCache so
        // a state always resolves to the same sampler instead of a fresh device object.
        using var samplers = new SamplerCache();
        samplers.Initialize(device);

        // The backend seam (T-786 W1). Only texture and view creation go through it so far; the bind group below still
        // builds on the device, which the escape hatch on WgpuRenderBackend exposes.
        var backend = new WgpuRenderBackend(device);
        var wgpu = backend;
        // The block atlas carries a mip chain, so it samples like the reference MipmappedTexture: linear minification
        // with an unbounded LOD clamp. Clamping to the sprite edge is kept because the atlas relies on padding.
        // Magnification stays nearest: with a linear magnifier every block face reads visibly soft even at close range,
        // because a 16 texel sprite is stretched over a whole face. Minification is linear and the LOD clamp is
        // unbounded, so the derived mip chain still resolves distant ground.
        var atlasSampler = samplers.GetMipmapped(AddressMode.ClampToEdge, FilterMode.Nearest);

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
        // The interface still creates its three pipelines from a native layout, so the handle is peeked once and shared
        // by those calls; UiRenderer is the next slice (docs/analysis/architecture/render-backend-seam.md 9.9).
        var uiPipelineLayout = wgpu.AsPipelineLayout(pipelineLayout);

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
            wgpu.AsBindGroupLayout(bindGroupLayout),
            [
                BindGroupEntry.TextureView(0, atlasView),
                BindGroupEntry.Sampler(1, atlasSampler),
                BindGroupEntry.Buffer(VertexLayout.CameraBinding, cameraBuffer, 0, VertexLayout.CameraUniformSize),
                BindGroupEntry.Buffer(3, fogBuffer, 0, 32),
                BindGroupEntry.Buffer(Shaders.ShaderBindings.Lightmap, lightmapBuffer, 0, Shaders.ShaderBindings.LightmapBytes)
            ]);

        using var entitySkin = new EntitySkinTextureGpu();
        if (entitySkinSource?.Atlas is { } skinData)
        {
            entitySkin.Upload(backend, bindGroupLayout, skinData, wgpu.AdoptBuffer(cameraBuffer), wgpu.AdoptBuffer(fogBuffer), wgpu.AdoptBuffer(lightmapBuffer), wgpu.AdoptSampler(atlasSampler));
        }

        using var itemAtlasGpu = new EntitySkinTextureGpu();
        if (itemAtlasSource?.Atlas is { } itemData)
            itemAtlasGpu.Upload(backend, bindGroupLayout, itemData, wgpu.AdoptBuffer(cameraBuffer), wgpu.AdoptBuffer(fogBuffer), wgpu.AdoptBuffer(lightmapBuffer), wgpu.AdoptSampler(atlasSampler));
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
            ? new OffscreenRenderer.CelestialResources(device, request, cameraBuffer, VertexLayout.CameraUniformSize, BackendConvert.FromWgpu(format))
            : null;
        using var celestialColour = request.CelestialColorDraws is { Length: > 0 } || celestialPerFrame is not null
            ? new OffscreenRenderer.CelestialColorResources(backend, device, request, cameraBuffer, VertexLayout.CameraUniformSize, BackendConvert.FromWgpu(format))
            : null;
        using var skyDiscColour = !skyDiscVanilla && request.SkyDiscDraws is { Length: > 0 }
            ? new OffscreenRenderer.CelestialColorResources(backend, device, request with { CelestialColorDraws = request.SkyDiscDraws }, cameraBuffer, VertexLayout.CameraUniformSize, BackendConvert.FromWgpu(format))
            : null;
        using var skyDiscVanillaResources = skyDiscVanilla && skyDiscPerFrame is not null
            ? new SkyDiscResources(backend, device, cameraBuffer, VertexLayout.CameraUniformSize, BackendConvert.FromWgpu(format))
            : null;
        // Clouds change every frame (scroll/camera), so their position-colour draw set is updated per frame.
        using var cloudColour = cloudsPerFrame is null
            ? null
            : new OffscreenRenderer.CelestialColorResources(backend, device, request with { CelestialColorDraws = [] }, cameraBuffer, VertexLayout.CameraUniformSize, BackendConvert.FromWgpu(format), WGPUCompareFunction.GreaterEqual);
        using var cloudOit = cloudOitEnabled && cloudColour is not null
            ? new CloudOitResources(device, cameraBuffer, VertexLayout.CameraUniformSize)
            : null;
        using var uiPipeline = UiRenderer.CreatePipeline(device, BackendConvert.FromWgpu(format), uiPipelineLayout);
        using var uiTextPipeline = UiRenderer.CreateTextPipeline(device, BackendConvert.FromWgpu(format), uiPipelineLayout);

        // Transparency is the reference depth-bounds + wavelet-transmittance OIT (docs/specs/render/oit-reference.md).
        // The earlier weighted-blended OIT looked markedly worse in play and is shelved, not compiled; git history keeps
        // it if it is ever wanted again.
        var oitTargetFormat = oitLowPrecision ? TextureFormat.Rgba8Unorm : (supportsFloat32Blendable ? TextureFormat.Rgba32Float : TextureFormat.Rgba16Float);
        using var refTransmittanceLayout = CreateOitTextureLayout(device, 1);
        using var refAccumulateLayout = CreateOitTextureLayout(device, 3);
        using var refCullLayout = CreateOitTextureLayout(device, 1);
        using var refCompositeLayout = CreateOitTextureLayout(device, 4);
        // The four texture layouts stay native for now: the seam can only declare a filterable float sample type, and
        // the OIT targets are Rgba32Float, whose sample type is unfilterable, so a seam-created layout would fail bind
        // group validation. Their pipeline layouts are still built through the seam, so each layout is adopted once.
        var refTransmittancePipelineLayout = backend.CreatePipelineLayout([bindGroupLayout, wgpu.AdoptBindGroupLayout(refTransmittanceLayout)]);
        var refAccumulatePipelineLayout = backend.CreatePipelineLayout([bindGroupLayout, wgpu.AdoptBindGroupLayout(refAccumulateLayout)]);
        var refCullPipelineLayout = backend.CreatePipelineLayout([wgpu.AdoptBindGroupLayout(refCullLayout)]);
        var refCompositePipelineLayout = backend.CreatePipelineLayout([wgpu.AdoptBindGroupLayout(refCompositeLayout)]);

        // The cloud layouts are created through the seam and released as soon as their pipelines exist: a pipeline
        // keeps what it needs from its layout, so nothing outlives this block. Destroying rather than waiting for the
        // method to end is what lets these stay handles instead of needing a Dispose on the handle itself.
        var cloudDbPipelineLayout = cloudOit is null ? default : backend.CreatePipelineLayout([wgpu.AdoptBindGroupLayout(cloudOit.Layout)]);
        using var cloudDepthBoundsPipeline = cloudOit is null ? default : CreateDepthBoundsPipeline(backend, cloudDbPipelineLayout, "cloud_oit", positionColor: true, format: BackendConvert.ToWgpu(oitTargetFormat), depthWrite: true);
        if (cloudDbPipelineLayout.IsValid)
        {
            backend.Destroy(cloudDbPipelineLayout);
        }

        var cloudTransPipelineLayout = cloudOit is null ? default : backend.CreatePipelineLayout([wgpu.AdoptBindGroupLayout(cloudOit.Layout), wgpu.AdoptBindGroupLayout(refTransmittanceLayout)]);
        using var cloudTransmittancePipeline = cloudOit is null ? default : CreateTransmittancePipeline(backend, cloudTransPipelineLayout, "cloud_oit", positionColor: true, format: BackendConvert.ToWgpu(oitTargetFormat));
        if (cloudTransPipelineLayout.IsValid)
        {
            backend.Destroy(cloudTransPipelineLayout);
        }

        var cloudAccumPipelineLayout = cloudOit is null ? default : backend.CreatePipelineLayout([wgpu.AdoptBindGroupLayout(cloudOit.Layout), wgpu.AdoptBindGroupLayout(refAccumulateLayout)]);
        using var cloudAccumulatePipeline = cloudOit is null ? default : CreateAccumulatePipeline(backend, cloudAccumPipelineLayout, "cloud_oit", positionColor: true, format: BackendConvert.ToWgpu(oitTargetFormat));
        if (cloudAccumPipelineLayout.IsValid)
        {
            backend.Destroy(cloudAccumPipelineLayout);
        }

        using var refDepthBoundsPipeline = CreateDepthBoundsPipeline(backend, pipelineLayout, format: BackendConvert.ToWgpu(oitTargetFormat));
        using var refTransmittancePipeline = CreateTransmittancePipeline(backend, refTransmittancePipelineLayout, format: BackendConvert.ToWgpu(oitTargetFormat));
        using var refAccumulatePipeline = CreateAccumulatePipeline(backend, refAccumulatePipelineLayout, format: BackendConvert.ToWgpu(oitTargetFormat));
        using var refCullPipeline = CreateCullPipeline(backend, refCullPipelineLayout, BackendConvert.ToWgpu(oitTargetFormat));
        using var refCompositePipeline = CreateCompositePipeline(backend, format, refCompositePipelineLayout);
        using var cloudCompositePipeline = CreateCompositePipeline(backend, format, refCompositePipelineLayout, WGPUCompareFunction.GreaterEqual);
        var blitDepthLayout = backend.CreateBindGroupLayout([
            BindGroupLayoutEntryDesc.Texture(0, ShaderStages.Fragment) with { SampleType = TextureSampleType.Depth, ViewDimension = TextureViewDimension.D2 },
            BindGroupLayoutEntryDesc.Sampler(1, ShaderStages.Fragment)
        ]);
        // The blit still builds its bind group on the device, so the handle is peeked once for that call.
        var blitDepthNativeLayout = wgpu.AsBindGroupLayout(blitDepthLayout);
        using var presentation = FramePresentation.Create(backend, device, BackendConvert.FromWgpu(format), samplers.GetClampToEdge(FilterMode.Nearest));
        var blitDepthPipelineLayout = backend.CreatePipelineLayout([blitDepthLayout]);
        var blitDepthPipeline = CreateBlitDepthPipeline(backend, blitDepthPipelineLayout);
        var blitDepthSampler = samplers.GetClampToEdge(FilterMode.Nearest);

        // The interface layer needs its own transform: one uniform buffer cannot hold both the world camera and
        // the orthographic projection while a single pass reads from it.
        var uiTransformBuffer = CreateGeometryBuffer(device, VertexLayout.CameraUniformSize, BufferUsage.Uniform | BufferUsage.CopyDst);

        // Glyphs live in their own sheet rather than the block atlas: the two have different sampling needs and
        // mixing them would make text blurry at small sizes.
        var glyphData = glyphTexture ?? TextureData.White;
        using var worldText = worldTextPerFrame is not null
            ? new WorldTextResources(backend, device, glyphData, cameraBuffer, VertexLayout.CameraUniformSize, BackendConvert.FromWgpu(format))
            : null;
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
            wgpu.AsBindGroupLayout(bindGroupLayout),
            [
                BindGroupEntry.TextureView(0, atlasView),
                BindGroupEntry.Sampler(1, atlasSampler),
                BindGroupEntry.Buffer(VertexLayout.CameraBinding, uiTransformBuffer, 0, VertexLayout.CameraUniformSize),
                // The layout declares the fog buffer for the world shader; the interface shader ignores it, but a bind
                // group must still supply every binding the layout declares.
                BindGroupEntry.Buffer(3, fogBuffer, 0, 32),
                BindGroupEntry.Buffer(Shaders.ShaderBindings.Lightmap, lightmapBuffer, 0, Shaders.ShaderBindings.LightmapBytes)
            ]);

        using var uiSpritePipeline = UiRenderer.CreateSpritePipeline(device, BackendConvert.FromWgpu(format), uiPipelineLayout);

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
                wgpu.AsBindGroupLayout(bindGroupLayout),
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
            wgpu.AsBindGroupLayout(bindGroupLayout),
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

        // Entity layers (docs/decisions/0021): persistent buffers grow with the layer plan instead of a fixed cap, so
        // an explicit per-layer definition can add a new pass without truncation.
        var entityLayerVertex = new List<WgpuBuffer>();
        var entityLayerIndex = new List<WgpuBuffer>();
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
                entityLayerVertex.Add(CreateGeometryBuffer(device, MinimumBufferBytes, BufferUsage.Vertex | BufferUsage.CopyDst));
                entityLayerIndex.Add(CreateGeometryBuffer(device, MinimumBufferBytes, BufferUsage.Index | BufferUsage.CopyDst));
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
        Texture cloudDepthTexture = default;
        TextureView cloudDepthView = default;
        var cloudDepthTargetsCreated = false;
        var refTargetsCreated = false;

        // Translucent item geometry composites through its own reference OIT set, as the reference does with
        // OitPipelineSet("item") bound to RenderTypes.ITEM_TRANSLUCENT (ItemOitSet).
        var itemOit = new ItemOitSet();

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
                if (cloudDepthTargetsCreated)
                {
                    cloudDepthView.Dispose();
                    cloudDepthTexture.Dispose();
                    cloudDepthTargetsCreated = false;
                }

                itemOit.Dispose();

            }

            refDepthBoundsTexture = CreateOitTarget(device, frameWidth, frameHeight, oitTargetFormat);
            refDepthBoundsView = refDepthBoundsTexture.CreateView();
            refDepthBoundsCulledTexture = CreateOitTarget(device, frameWidth, frameHeight, oitTargetFormat);
            refDepthBoundsCulledView = refDepthBoundsCulledTexture.CreateView();
            refTransmittance0Texture = CreateOitTarget(device, frameWidth, frameHeight, oitTargetFormat);
            refTransmittance0View = refTransmittance0Texture.CreateView();
            refTransmittance1Texture = CreateOitTarget(device, frameWidth, frameHeight, oitTargetFormat);
            refTransmittance1View = refTransmittance1Texture.CreateView();
            refAccumulateTexture = CreateOitTarget(device, frameWidth, frameHeight, oitTargetFormat);
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
                    // The composite writes the closest translucent device depth from the original terrain depth bounds,
                    // matching the reference composite (which samples the un-culled target); clouds are excluded from
                    // this depth write, exactly as the reference excludes them.
                    BindGroupEntry.TextureView(3, refDepthBoundsView)
                ]);
            itemOit.Create(
                device,
                frameWidth,
                frameHeight,
                oitTargetFormat,
                refTransmittanceLayout,
                refAccumulateLayout,
                refCompositeLayout,
                CreateOitTarget);
            // Clouds always get an independent depth target so the cloud depth-bounds write never pollutes the main
            // depth the terrain transparency passes test against (otherwise glass/water behind a cloud are culled).
            if (cloudOit is not null)
            {
                cloudDepthTexture = CreateDepthTexture(device, frameWidth, frameHeight);
                cloudDepthView = cloudDepthTexture.CreateView();
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
        var appliedEntitySkinVersion = entitySkinSource?.Version ?? 0;
        var appliedItemAtlasVersion = itemAtlasSource?.Version ?? 0;

        while (true)
        {
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
                    wgpu.AsBindGroupLayout(bindGroupLayout),
                    [
                        BindGroupEntry.TextureView(0, atlasView),
                        BindGroupEntry.Sampler(1, atlasSampler),
                        BindGroupEntry.Buffer(VertexLayout.CameraBinding, cameraBuffer, 0, VertexLayout.CameraUniformSize),
                        BindGroupEntry.Buffer(3, fogBuffer, 0, 32),
                BindGroupEntry.Buffer(Shaders.ShaderBindings.Lightmap, lightmapBuffer, 0, Shaders.ShaderBindings.LightmapBytes)
                    ]);

                uiBindGroup = device.CreateBindGroup(
                    wgpu.AsBindGroupLayout(bindGroupLayout),
                    [
                        BindGroupEntry.TextureView(0, atlasView),
                        BindGroupEntry.Sampler(1, atlasSampler),
                        BindGroupEntry.Buffer(VertexLayout.CameraBinding, uiTransformBuffer, 0, VertexLayout.CameraUniformSize),
                        BindGroupEntry.Buffer(3, fogBuffer, 0, 32),
                BindGroupEntry.Buffer(Shaders.ShaderBindings.Lightmap, lightmapBuffer, 0, Shaders.ShaderBindings.LightmapBytes)
                    ]);

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
                    entitySkin.Upload(backend, bindGroupLayout, refreshedSkin, wgpu.AdoptBuffer(cameraBuffer), wgpu.AdoptBuffer(fogBuffer), wgpu.AdoptBuffer(lightmapBuffer), wgpu.AdoptSampler(atlasSampler));
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
                    itemAtlasGpu.Upload(backend, bindGroupLayout, refreshedItems, wgpu.AdoptBuffer(cameraBuffer), wgpu.AdoptBuffer(fogBuffer), wgpu.AdoptBuffer(lightmapBuffer), wgpu.AdoptSampler(atlasSampler));
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
                    wgpu.AsBindGroupLayout(bindGroupLayout),
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
                        wgpu.AsBindGroupLayout(bindGroupLayout),
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
                surface.Configure(device, format, TextureUsage.RenderAttachment, frameWidth, frameHeight, presentMode, WGPUCompositeAlphaMode.Auto);
                depthView.Dispose();
                depthTexture.Dispose();
                depthTexture = CreateDepthTexture(device, frameWidth, frameHeight);
                depthView = depthTexture.CreateView();

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
                        entityLayerVertex[layerIndex].Dispose();
                        entityLayerVertexCapacity[layerIndex] = Math.Max(neededVertex * 2, MinimumBufferBytes);
                        entityLayerVertex[layerIndex] = CreateGeometryBuffer(
                            device, entityLayerVertexCapacity[layerIndex], BufferUsage.Vertex | BufferUsage.CopyDst);
                    }

                    if (neededIndex > entityLayerIndexCapacity[layerIndex])
                    {
                        entityLayerIndex[layerIndex].Dispose();
                        entityLayerIndexCapacity[layerIndex] = Math.Max(neededIndex * 2, MinimumBufferBytes);
                        entityLayerIndex[layerIndex] = CreateGeometryBuffer(
                            device, entityLayerIndexCapacity[layerIndex], BufferUsage.Index | BufferUsage.CopyDst);
                    }

                    device.Queue.WriteBuffer(entityLayerVertex[layerIndex], 0, geometry.Vertices.AsSpan(0, geometry.VertexFloats));
                    device.Queue.WriteBuffer(entityLayerIndex[layerIndex], 0, geometry.Indices.AsSpan(0, geometry.IndexCount));
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
            var view = presentation.Ensure(frameWidth, frameHeight);
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

            var encoder = backend.CreateCommandEncoder();
            var frameColour = clearColourPerFrame?.Invoke() ?? colour;
            var pass = backend.BeginRenderPass(
                encoder,
                [ColorAttachmentDesc.Clear(wgpu.AdoptTextureView(view), frameColour)],
                DepthAttachmentDesc.Clear(wgpu.AdoptTextureView(depthView), 0f));

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

            device.Queue.WriteBuffer(cameraBuffer, 0, cameraBytes.AsSpan());

            // Per-frame lightmap parameters: day/night changes only this block, never the mesh.
            var frameLightmapBytes = new byte[Shaders.ShaderBindings.LightmapBytes];
            var frameLightmap = lightmapPerFrame?.Invoke() ?? currentRequest.Lightmap;
            var glintTime = currentRequest.GlintTime != 0f
                ? currentRequest.GlintTime
                : (float)(System.Diagnostics.Stopwatch.GetTimestamp() / (double)System.Diagnostics.Stopwatch.Frequency);
            LightmapUniformCodec.Write(frameLightmap with { Padding = glintTime }, frameLightmapBytes);
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

            backend.SetBindGroup(pass, 0, wgpu.AdoptBindGroup(bindGroup));

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

            void DrawScenePass(WorldRenderPassKind kind)
            {
                switch (kind)
                {
                    case WorldRenderPassKind.Sky:
                    {
                        // Sky first, over the cleared target; world geometry then draws on top of it. When the caller
                        // supplies a flat SKY_COLOR through the clear colour, the gradient sky is skipped.
                        if (!currentRequest.SkipGradientSky)
                        {
                            backend.SetPipeline(pass, skyPipeline);
                            backend.Draw(pass, 3, 1, 0, 0);
                        }

                        // Sun/moon (textured) then sunrise/stars (position-colour), still before terrain so world
                        // geometry covers them.
                        if (celestialPerFrame?.Invoke() is { } celestialUpdate)
                        {
                            celestial?.SetDraws(celestialUpdate.Draws);
                            celestialColour?.SetDraws(celestialUpdate.Colors);
                            skyDiscColour?.SetDraws(celestialUpdate.SkyDiscs);
                        }

                        // Apply the camera delta AFTER any per-frame draw rebuild: a freshly built draw uses the base
                        // (initial) camera model, so translating first would be overwritten and the bodies would stay
                        // anchored in the world.
                        celestial?.UpdateCamera(celestialDelta);
                        celestialColour?.UpdateCamera(celestialDelta);
                        skyDiscColour?.UpdateCamera(celestialDelta);

                        if (skyDiscVanillaResources is not null && skyDiscPerFrame?.Invoke() is { } skyDiscState)
                        {
                            skyDiscVanillaResources.Update(skyDiscState.Sky, skyDiscState.Fog, skyDiscState.SkyFogEnd, skyDiscState.Eye);
                            skyDiscVanillaResources.Draw(backend, pass);
                        }

                        skyDiscColour?.Draw(wgpu.AsRenderPass(pass));
                        celestial?.Draw(wgpu.AsRenderPass(pass));
                        celestialColour?.Draw(wgpu.AsRenderPass(pass));
                        break;
                    }

                    case WorldRenderPassKind.TerrainOpaque:
                    {
                        backend.SetPipeline(pass, pipeline);
                        backend.SetBindGroup(pass, 0, wgpu.AdoptBindGroup(bindGroup));
                        if (arena is { ChunkCount: > 0 })
                        {
                            if (disableCulling)
                            {
                                arena.Draw(wgpu.AsRenderPass(pass));
                            }
                            else
                            {
                                arena.Draw(wgpu.AsRenderPass(pass), frustumPlanes);
                            }
                        }

                        // Clouds are drawn after the transparent terrain (below), matching the reference order.
                        break;
                    }

                    case WorldRenderPassKind.TerrainCutout:
                    {
                        if (arenaCutout is not { ChunkCount: > 0 })
                        {
                            break;
                        }

                        backend.SetPipeline(pass, cutoutPipeline);
                        backend.SetBindGroup(pass, 0, wgpu.AdoptBindGroup(bindGroup));
                        if (disableCulling)
                        {
                            arenaCutout.Draw(wgpu.AsRenderPass(pass));
                        }
                        else
                        {
                            arenaCutout.Draw(wgpu.AsRenderPass(pass), frustumPlanes);
                        }
                        break;
                    }

                    case WorldRenderPassKind.Entities:
                    {
                        if (dynamicIndexCount > 0)
                        {
                            backend.SetPipeline(pass, entityPipeline);
                            backend.SetVertexBuffer(pass, 0, wgpu.AdoptBuffer(dynamicVertex), 0, dynamicVertexCapacity);
                            backend.SetIndexBuffer(pass, wgpu.AdoptBuffer(dynamicIndex), IndexFormat.Uint32, 0, (ulong)dynamicIndexCount * sizeof(uint));
                            if (entitySkin.Created)
                            {
                                backend.SetBindGroup(pass, 0, entitySkin.BindGroup);
                            }

                            backend.DrawIndexed(pass, dynamicIndexCount, 1, 0, 0, 1);
                            if (entitySkin.Created)
                            {
                                backend.SetBindGroup(pass, 0, wgpu.AdoptBindGroup(bindGroup));
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
                            backend.SetVertexBuffer(pass, 0, wgpu.AdoptBuffer(entityLayerVertex[layerIndex]), 0, entityLayerVertexCapacity[layerIndex]);
                            backend.SetIndexBuffer(
                                pass,
                                wgpu.AdoptBuffer(entityLayerIndex[layerIndex]),
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
                            backend.SetBindGroup(pass, 0, wgpu.AdoptBindGroup(bindGroup));
                        }
                        else if (useStaticWorld)
                        {
                            backend.SetVertexBuffer(pass, 0, wgpu.AdoptBuffer(vertexBuffer));
                            backend.SetIndexBuffer(pass, wgpu.AdoptBuffer(indexBuffer), IndexFormat.Uint32);
                            backend.DrawIndexed(pass, (uint)currentRequest.Indices.Length, 1, 0, 0, 1);
                        }

                        // The static fallback keeps its original post-entity order. Translucent terrain is not drawn
                        // here: it writes the order independent targets in the next pass (docs/specs/render/pipeline.md 15).
                        if (useStaticWorld && currentRequest.CutoutVertices is { Length: > 0 } windowCutoutVertices &&
                            currentRequest.CutoutIndices is { Length: > 0 } windowCutoutIndices)
                        {
                            using var windowCutoutVertexBuffer = device.CreateBufferInit(
                                new BufferInitDescriptor { Usage = BufferUsage.Vertex },
                                windowCutoutVertices.AsSpan());
                            using var windowCutoutIndexBuffer = device.CreateBufferInit(
                                new BufferInitDescriptor { Usage = BufferUsage.Index },
                                windowCutoutIndices.AsSpan());

                            backend.SetPipeline(pass, cutoutPipeline);
                            backend.SetBindGroup(pass, 0, wgpu.AdoptBindGroup(bindGroup));
                            backend.SetVertexBuffer(pass, 0, wgpu.AdoptBuffer(windowCutoutVertexBuffer));
                            backend.SetIndexBuffer(pass, wgpu.AdoptBuffer(windowCutoutIndexBuffer), IndexFormat.Uint32);
                            backend.DrawIndexed(pass, (uint)windowCutoutIndices.Length, 1, 0, 0, 1);
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

            if (cloudDepthTargetsCreated)
            {
                // Blit the opaque main depth into the cloud depth attachment (reference BLIT_DEPTH_DURING_DEPTH_BOUNDS).
                // The first pass recorded through the seam: it needs no helper that still takes a native pass, so it
                // proves the whole path - encoder adoption, attachments, bindings and the single end - on its own.
                var blitPass = backend.BeginRenderPass(
                    encoder,
                    [],
                    DepthAttachmentDesc.Keep(wgpu.AdoptTextureView(cloudDepthView)));
                try
                {
                    backend.SetPipeline(blitPass, blitDepthPipeline);
                    backend.SetBindGroup(blitPass, 0, wgpu.AdoptBindGroup(device.CreateBindGroup(
                        blitDepthNativeLayout,
                        [BindGroupEntry.TextureView(0, depthView), BindGroupEntry.Sampler(1, blitDepthSampler)])));
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

            if (refTargetsCreated && needsOit)
            {
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

                // Translucent item geometry joins the same reference OIT stages (the reference binds OIT_ITEM to
                // RenderTypes.ITEM_TRANSLUCENT). The OIT pipelines already declare the entity vertex layout and the shared
                // pipeline layout, so only the atlas bind group differs from the terrain draw.
                void DrawItemOitGeometry(Brew.Wgpu.RenderPassEncoder p)
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
                            p.SetBindGroup(0, wgpu.AsBindGroup(itemAtlasGpu.BindGroup));
                            atlasBound = true;
                        }

                        p.SetVertexBuffer(0, entityLayerVertex[itemLayer], 0, entityLayerVertexCapacity[itemLayer]);
                        p.SetIndexBuffer(
                            entityLayerIndex[itemLayer],
                            WGPUIndexFormat.Uint32,
                            0,
                            (ulong)entityLayerIndexCount[itemLayer] * sizeof(uint));
                        p.DrawIndexed(entityLayerIndexCount[itemLayer], 1, 0, 0, 1);
                    }
                }

                // Terrain transparency (glass/water) tests depth against the opaque main depth only; cloud depth writes
                // go to a separate target below so they cannot cull translucent terrain behind a cloud.
                var depthLoadStore = new DepthStencilAttachment
                {
                    View = depthView,
                    DepthLoadOp = WGPULoadOp.Load,
                    DepthStoreOp = WGPUStoreOp.Store
                };

                // Cloud OIT stages use the independent cloud depth target (blitted from the opaque depth above), so the
                // cloud depth-bounds write culls only the cloud's own interior faces without hiding terrain behind it.
                var cloudOitDepth = cloudDepthTargetsCreated
                    ? new DepthStencilAttachment { View = cloudDepthView, DepthLoadOp = WGPULoadOp.Load, DepthStoreOp = WGPUStoreOp.Store }
                    : depthLoadStore;

                ReferenceOitPasses.Record(new ReferenceOitPasses.Parameters
                {
                    Backend = backend,
            Encoder = encoder,
                    Frame = view,
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
                    Clouds = cloudOit,
                    CloudBoundsPipeline = cloudDepthBoundsPipeline,
                    CloudTransmittancePipeline = cloudTransmittancePipeline,
                    CloudAccumulatePipeline = cloudAccumulatePipeline,
                    Items = itemOit,
                    ItemAtlas = wgpu.AsBindGroup(itemAtlasGpu.BindGroup),
                    DrawTerrain = DrawReferenceGeometry,
                    DrawItems = DrawItemOitGeometry
                });
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
            }
            // (The weighted-blended OIT path that used to live here was shelved for its visual quality and is no longer
            // compiled; the reference OIT above is the only transparency path.)

            if (worldText is not null)
            {
                worldText.SetDraws(worldTextPerFrame?.Invoke() ?? []);
                // The helper still takes a native pass, so the handle is unwrapped for that call only; the pass itself
                // is begun and ended through the seam.
                var worldTextPass = backend.BeginRenderPass(
                    encoder,
                    [ColorAttachmentDesc.Keep(wgpu.AdoptTextureView(view))],
                    DepthAttachmentDesc.Keep(wgpu.AdoptTextureView(depthView)));
                try
                {
                    worldText.Draw(wgpu.AsRenderPass(worldTextPass));
                }
                finally
                {
                    backend.EndRenderPass(worldTextPass);
                }
            }
            // Clouds come after the opaque and transparent terrain, with depth testing so terrain and the cloud layer
            // occlude each other correctly (the reference draws them in executeClassicTransparency).
            if (cloudColour is not null && cloudOit is null)
            {
                var cloudPass = backend.BeginRenderPass(
                    encoder,
                    [ColorAttachmentDesc.Keep(wgpu.AdoptTextureView(view))],
                    DepthAttachmentDesc.Keep(wgpu.AdoptTextureView(depthView)));
                try
                {
                    if (cloudDrawFrame is { } cloudDraw)
                    {
                        cloudColour.SetDraws([cloudDraw]);
                    }

                    cloudColour.Draw(wgpu.AsRenderPass(cloudPass));
                }
                finally
                {
                    backend.EndRenderPass(cloudPass);
                }
            }

            // Pass 4: the interface draws last, over the resolved frame, in its own pass so it never participates in the
            // world depth test or the transparency resolve.
            var uiPass = backend.BeginRenderPass(
                encoder,
                [ColorAttachmentDesc.Keep(wgpu.AdoptTextureView(view))],
                DepthAttachmentDesc.Keep(wgpu.AdoptTextureView(depthView)));
            try
            {

            if (uiIndexCount > 0 && uiRanges.Length > 0)
            {
                backend.SetVertexBuffer(uiPass, 0, wgpu.AdoptBuffer(uiVertex), 0, uiVertexCapacity);
                backend.SetIndexBuffer(uiPass, wgpu.AdoptBuffer(uiIndex), IndexFormat.Uint32, 0, (ulong)uiIndexCount * sizeof(uint));

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
                            backend.SetPipeline(uiPass, wgpu.AdoptPipeline(uiTextPipeline));
                            backend.SetBindGroup(uiPass, 0, wgpu.AdoptBindGroup(uiTextBindGroup));
                        }
                        else if (range.Material == UiMaterial.Sprite &&
                                 range.SpriteIndex >= 0 &&
                                 range.SpriteIndex < spriteBindGroups.Count)
                        {
                            backend.SetPipeline(uiPass, wgpu.AdoptPipeline(uiSpritePipeline));
                            backend.SetBindGroup(uiPass, 0, wgpu.AdoptBindGroup(spriteBindGroups[range.SpriteIndex]));
                        }
                        else
                        {
                            backend.SetPipeline(uiPass, wgpu.AdoptPipeline(uiPipeline));
                            backend.SetBindGroup(uiPass, 0, wgpu.AdoptBindGroup(uiBindGroup));
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
            finally
            {
                backend.EndRenderPass(uiPass);
            }

            presentation.RecordPresent(backend, encoder, wgpu.AdoptTextureView(surfaceView), DepthAttachmentDesc.Keep(wgpu.AdoptTextureView(depthView)));

            backend.Submit(encoder);

            // Screenshot (reference Screenshot.grab): only when the host asks, and only after the frame was submitted, so
            // the frame target still holds the presented image (docs/specs/render/screenshot.md 2.2).
            if (screenshotRequest?.Invoke(window) is { Length: > 0 } screenshotPath &&
                FrameCapture.Capture(instance, device, presentation.Target, frameWidth, frameHeight, BackendConvert.FromWgpu(format)) is { } screenshotPixels)
            {
                screenshotCaptured?.Invoke(screenshotPath, screenshotPixels, frameWidth, frameHeight);
            }
            surface.Present();

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

        Console.WriteLine(
            $"window: rebuild {rebuildCount} frames, avg {(rebuildCount == 0 ? 0 : rebuildTotalMs / rebuildCount):F1} ms, "
            + $"worst {worstRebuildMs:F1} ms, uploaded {uploadBytes / (1024d * 1024d):F1} MB; "
            + $"camera avg {(presented == 0 ? 0 : cameraTotalMs / presented):F1} ms, worst {worstCameraMs:F1} ms");

        dynamicVertex.Dispose();
        dynamicIndex.Dispose();
        for (var layerIndex = 0; layerIndex < entityLayerVertex.Count; layerIndex++)
        {
            entityLayerVertex[layerIndex].Dispose();
            entityLayerIndex[layerIndex].Dispose();
        }

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

        // The frame pipelines and pipeline layouts are seam handles, so the frame loop's exit releases them by hand: a
        // handle is pure identity and cannot carry a Dispose (render-backend-seam.md 9).
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
            Usage = TextureUsage.RenderAttachment | TextureUsage.CopySrc | TextureUsage.CopyDst | TextureUsage.TextureBinding
        });

    /// <summary>Creates one order independent transparency target: renderable and sampleable, at frame size.</summary>
    private static Texture CreateOitTarget(Device device, uint width, uint height, TextureFormat format) =>
        device.CreateTexture(new TextureDescriptor
        {
            Size = new WGPUExtent3D { width = width, height = height, depthOrArrayLayers = 1 },
            MipLevelCount = 1,
            SampleCount = 1,
            Dimension = WGPUTextureDimension._2D,
            Format = BackendConvert.ToWgpu(format),
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
    private static RenderPipeline CreateDepthBoundsPipeline(WgpuRenderBackend backend, PipelineLayoutHandle layout, string shaderName = "oit_reference", bool positionColor = false, WGPUTextureFormat format = WGPUTextureFormat.RGBA16Float, bool depthWrite = false)
    {
        var source = System.Text.Encoding.UTF8.GetBytes(Shaders.ShaderLibrary.Load(shaderName));
        return backend.AsPipeline(backend.CreateRenderPipeline(new PipelineDesc(
            source, "vs_main", source, "fs_depth_bounds", layout,
            [new ColorTargetDesc(BackendConvert.FromWgpu(format), new BlendStateDesc(
                new BlendComponentDesc(BlendOperation.Max, BlendFactor.One, BlendFactor.One),
                new BlendComponentDesc(BlendOperation.Max, BlendFactor.One, BlendFactor.One)))],
            new DepthStateDesc(TextureFormat.Depth32Float, CompareFunction.GreaterEqual, depthWrite),
            CullMode.Back, FrontFace.Ccw,
            (ulong)VertexLayout.Stride, VertexStepMode.Vertex,
            LayoutAttributes.From(VertexLayout.Attributes()))));
    }

    /// <summary>Reference transparency stage 2: wavelet-transmittance coefficients, additively blended into two targets.</summary>
    private static RenderPipeline CreateTransmittancePipeline(WgpuRenderBackend backend, PipelineLayoutHandle layout, string shaderName = "oit_reference", bool positionColor = false, WGPUTextureFormat format = WGPUTextureFormat.RGBA16Float, bool depthWrite = false)
    {
        var source = System.Text.Encoding.UTF8.GetBytes(Shaders.ShaderLibrary.Load(shaderName));
        static ColorTargetDesc Additive(WGPUTextureFormat format) => new(
            BackendConvert.FromWgpu(format), new BlendStateDesc(
                new BlendComponentDesc(BlendOperation.Add, BlendFactor.One, BlendFactor.One),
                new BlendComponentDesc(BlendOperation.Add, BlendFactor.One, BlendFactor.One)));
        return backend.AsPipeline(backend.CreateRenderPipeline(new PipelineDesc(
            source, "vs_main", source, "fs_transmittance", layout,
            [Additive(format), Additive(format)],
            new DepthStateDesc(TextureFormat.Depth32Float, CompareFunction.GreaterEqual, depthWrite),
            CullMode.Back, FrontFace.Ccw,
            (ulong)VertexLayout.Stride, VertexStepMode.Vertex,
            LayoutAttributes.From(VertexLayout.Attributes()))));
    }

    /// <summary>Reference transparency stage 3: colour weighted by the transmittance in front of each fragment.</summary>
    private static RenderPipeline CreateAccumulatePipeline(WgpuRenderBackend backend, PipelineLayoutHandle layout, string shaderName = "oit_reference", bool positionColor = false, WGPUTextureFormat format = WGPUTextureFormat.RGBA16Float, bool depthWrite = false)
    {
        var source = System.Text.Encoding.UTF8.GetBytes(Shaders.ShaderLibrary.Load(shaderName));
        return backend.AsPipeline(backend.CreateRenderPipeline(new PipelineDesc(
            source, "vs_main", source, "fs_accumulate", layout,
            [new ColorTargetDesc(BackendConvert.FromWgpu(format), new BlendStateDesc(
                new BlendComponentDesc(BlendOperation.Add, BlendFactor.One, BlendFactor.One),
                new BlendComponentDesc(BlendOperation.Add, BlendFactor.One, BlendFactor.One)))],
            new DepthStateDesc(TextureFormat.Depth32Float, CompareFunction.GreaterEqual, depthWrite),
            CullMode.Back, FrontFace.Ccw,
            (ulong)VertexLayout.Stride, VertexStepMode.Vertex,
            LayoutAttributes.From(VertexLayout.Attributes()))));
    }

    /// <summary>Reference transparency: clip the far depth bound to the nearest opaque surface.</summary>
    private static RenderPipeline CreateCullPipeline(WgpuRenderBackend backend, PipelineLayoutHandle layout, WGPUTextureFormat format = WGPUTextureFormat.RGBA16Float)
    {
        var source = System.Text.Encoding.UTF8.GetBytes(Shaders.ShaderLibrary.Load("oit_depth_bounds_cull"));
        return backend.AsPipeline(backend.CreateRenderPipeline(new PipelineDesc(
            source, "vs_fullscreen", source, "fs_cull", layout,
            [new ColorTargetDesc(BackendConvert.FromWgpu(format))],
            new DepthStateDesc(TextureFormat.Depth32Float, CompareFunction.Always, false),
            CullMode.None, FrontFace.Ccw,
            0, VertexStepMode.Vertex,
            [])));
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

    private static RenderPipeline CreateCompositePipeline(WgpuRenderBackend backend, WGPUTextureFormat format, PipelineLayoutHandle layout, WGPUCompareFunction depthCompare = WGPUCompareFunction.Always)
    {
        var source = System.Text.Encoding.UTF8.GetBytes(Shaders.ShaderLibrary.Load("oit_composite"));
        return backend.AsPipeline(backend.CreateRenderPipeline(new PipelineDesc(
            source, "vs_fullscreen", source, "fs_composite", layout,
            [new ColorTargetDesc(BackendConvert.FromWgpu(format), new BlendStateDesc(
                new BlendComponentDesc(BlendOperation.Add, BlendFactor.One, BlendFactor.OneMinusSrcAlpha),
                new BlendComponentDesc(BlendOperation.Add, BlendFactor.One, BlendFactor.OneMinusSrcAlpha)))],
            new DepthStateDesc(TextureFormat.Depth32Float, BackendConvert.FromWgpu(depthCompare), true),
            CullMode.None, FrontFace.Ccw,
            0, VertexStepMode.Vertex,
            [])));
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
