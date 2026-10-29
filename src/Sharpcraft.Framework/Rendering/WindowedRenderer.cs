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
        Func<FrameRequest?>? rebuildRequest = null)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var window = WindowHost.Create("Sharpcraft", request.Width, request.Height);

        var instance = Instance.Create();
        var source = window.GetSurfaceSource();
        using var surface = instance.CreateSurface(ToSurfaceSource(source));
        using var adapter = instance.RequestAdapterBlocking(surface);
        using var device = adapter.RequestDeviceBlocking();

        Console.WriteLine($"window: surface ready on {adapter.GetInfo().Description}");
        var format = surface.GetPreferredFormat(adapter);

        // Atlas resources: the fragment shader samples it, so the pipeline layout and the bind group
        // have to exist before anything is drawn (docs/modules/render.md 6.1).
        var atlasData = request.Atlas ?? TextureData.White;
        using var atlasTexture = device.CreateTexture(new TextureDescriptor
        {
            Size = new WGPUExtent3D
            {
                width = (uint)atlasData.Width,
                height = (uint)atlasData.Height,
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

        using var atlasView = atlasTexture.CreateView();

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
                VertexLayout.CameraUniformSize)
        ]);

        using var pipelineLayout = device.CreatePipelineLayout([bindGroupLayout]);

        using var cameraBuffer = device.CreateBufferInit(
            new BufferInitDescriptor { Usage = BufferUsage.Uniform | BufferUsage.CopyDst },
            new byte[VertexLayout.CameraUniformSize].AsSpan());

        using var bindGroup = device.CreateBindGroup(
            bindGroupLayout,
            [
                BindGroupEntry.TextureView(0, atlasView),
                BindGroupEntry.Sampler(1, atlasSampler),
                BindGroupEntry.Buffer(VertexLayout.CameraBinding, cameraBuffer, 0, VertexLayout.CameraUniformSize)
            ]);

        using var pipeline = CreatePipeline(device, format, pipelineLayout);
        var indexBuffer = device.CreateBufferInit(
            new BufferInitDescriptor { Usage = BufferUsage.Index },
            request.Indices.AsSpan());

        // A DPI-scaled display reports a logical size smaller than the framebuffer; the swapchain
        // must be configured in physical pixels or every acquire reports "outdated".
        var frameWidth = (uint)window.FramebufferWidth;
        var frameHeight = (uint)window.FramebufferHeight;
        Console.WriteLine($"window: framebuffer {frameWidth}x{frameHeight}, logical {window.Width}x{window.Height}");

        surface.Configure(device, format, TextureUsage.RenderAttachment, frameWidth, frameHeight);

        var currentRequest = request;
        var vertexBuffer = UploadVertices(device, currentRequest, frameWidth, frameHeight);
        var depthTexture = CreateDepthTexture(device, frameWidth, frameHeight);
        var depthView = depthTexture.CreateView();

        Span<float> cameraBlock = stackalloc float[16];
        var cameraBytes = new byte[VertexLayout.CameraUniformSize];

        var colour = request.ClearColour;
        var presented = 0;
        var deadline = maxDuration is { } duration
            ? Environment.TickCount64 + (long)duration.TotalMilliseconds
            : long.MaxValue;

        Console.WriteLine($"window: swapchain configured ({format}, {frameWidth}x{frameHeight})");

        while (true)
        {
            if (!window.Pump() || Environment.TickCount64 > deadline)
            {
                break;
            }

            var nextWidth = (uint)window.FramebufferWidth;
            var nextHeight = (uint)window.FramebufferHeight;
            if (nextWidth != frameWidth || nextHeight != frameHeight)
            {
                Console.WriteLine($"window: resize {frameWidth}x{frameHeight} -> {nextWidth}x{nextHeight}");
                frameWidth = nextWidth;
                frameHeight = nextHeight;

                // Everything sized to the old framebuffer is rebuilt: surface, depth, and the
                // vertices, which carry the aspect ratio in their clip coordinates.
                surface.Configure(device, format, TextureUsage.RenderAttachment, frameWidth, frameHeight);
                depthView.Dispose();
                depthTexture.Dispose();
                depthTexture = CreateDepthTexture(device, frameWidth, frameHeight);
                depthView = depthTexture.CreateView();
                vertexBuffer.Dispose();
                vertexBuffer = UploadVertices(device, request, frameWidth, frameHeight);
            }

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

            // A block change rebuilds the geometry; swapping the buffers is cheap, the meshing is not.
            if (rebuildRequest is not null && rebuildRequest() is { } rebuilt)
            {
                vertexBuffer.Dispose();
                indexBuffer.Dispose();
                currentRequest = rebuilt;
                vertexBuffer = UploadVertices(device, currentRequest, frameWidth, frameHeight);
                indexBuffer = device.CreateBufferInit(
                    new BufferInitDescriptor { Usage = BufferUsage.Index },
                    currentRequest.Indices.AsSpan());
            }

            using var view = acquired.Texture.CreateView();
            var encoder = device.CreateCommandEncoder();
            var pass = encoder.BeginRenderPass(
            [
                new ColorAttachment
                {
                    View = view,
                    LoadOp = WGPULoadOp.Clear,
                    StoreOp = WGPUStoreOp.Store,
                    ClearValue = new WGPUColor { r = colour.X, g = colour.Y, b = colour.Z, a = colour.W }
                }
            ],
            DepthStencilAttachment.DepthClear(depthView));

            pass.SetPipeline(pipeline);
            // Rewriting 64 bytes is the whole cost of moving the camera; the staging buffers are reused.
            var (eye, target) = cameraPerFrame is null
                ? (request.CameraPosition, request.CameraTarget)
                : cameraPerFrame(window);

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

            pass.SetBindGroup(0, bindGroup);
            pass.SetVertexBuffer(0, vertexBuffer);
            pass.SetIndexBuffer(indexBuffer, WGPUIndexFormat.Uint32);
            pass.DrawIndexed((uint)currentRequest.Indices.Length);
            pass.End();

            var commands = encoder.Finish();
            device.Queue.Submit(commands);
            surface.Present();
            commands.Dispose();
            encoder.Dispose();
            acquired.Texture.Dispose();

            onFrame?.Invoke(presented, window);
            presented++;

            if (frames > 0 && presented >= frames)
            {
                break;
            }
        }

        vertexBuffer.Dispose();
        depthView.Dispose();
        depthTexture.Dispose();
        indexBuffer.Dispose();

        return format;
    }

    /// <summary>
    /// Uploads clip-space vertices built for the current framebuffer aspect ratio. The staging
    /// array is pooled, so a resize (or a frame with a moving camera) does not allocate megabytes.
    /// </summary>
    private static WgpuBuffer UploadVertices(Device device, FrameRequest request, uint width, uint height)
    {
        // World-space vertices: the shader applies the camera uniform, so this buffer is independent of
        // the camera and only the geometry decides its contents.
        _ = width;
        _ = height;
        return device.CreateBufferInit(
            new BufferInitDescriptor { Usage = BufferUsage.Vertex },
            request.Vertices.AsSpan());
    }

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

    private static SurfaceSource ToSurfaceSource(NativeSurfaceSource source) => source.Tag switch
    {
        NativeSurfaceKind.Windows => SurfaceSource.WindowsHwnd(source.Handle1, source.Handle0),
        NativeSurfaceKind.X11 => SurfaceSource.XlibWindow(source.Handle0, (ulong)source.Handle1),
        NativeSurfaceKind.Wayland => SurfaceSource.WaylandSurface(source.Handle0, source.Handle1),
        NativeSurfaceKind.MetalLayer => SurfaceSource.MetalLayer(source.Handle0),
        _ => throw new PlatformNotSupportedException($"no surface source for {source.Tag}")
    };

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
