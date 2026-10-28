// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 MinecraftCSharp contributors

using Brew.Wgpu;
using Brew.Wgpu.Native;
using WgpuBuffer = Brew.Wgpu.Buffer;
using MinecraftCSharp.Framework.Platform;

namespace MinecraftCSharp.Framework.Rendering;

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
        Action<int, WindowHost>? onFrame = null)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var window = WindowHost.Create("MinecraftCSharp", request.Width, request.Height);

        var instance = Instance.Create();
        var source = window.GetSurfaceSource();
        using var surface = instance.CreateSurface(ToSurfaceSource(source));
        using var adapter = instance.RequestAdapterBlocking(surface);
        using var device = adapter.RequestDeviceBlocking();

        Console.WriteLine($"window: surface ready on {adapter.GetInfo().Description}");
        var format = surface.GetPreferredFormat(adapter);

        using var pipeline = CreatePipeline(device, format);
        var indexBuffer = device.CreateBufferInit(
            new BufferInitDescriptor { Usage = BufferUsage.Index },
            request.Indices.AsSpan());

        // A DPI-scaled display reports a logical size smaller than the framebuffer; the swapchain
        // must be configured in physical pixels or every acquire reports "outdated".
        var frameWidth = (uint)window.FramebufferWidth;
        var frameHeight = (uint)window.FramebufferHeight;
        Console.WriteLine($"window: framebuffer {frameWidth}x{frameHeight}, logical {window.Width}x{window.Height}");

        surface.Configure(device, format, TextureUsage.RenderAttachment, frameWidth, frameHeight);

        var vertexBuffer = UploadVertices(device, request, frameWidth, frameHeight);
        var depthTexture = CreateDepthTexture(device, frameWidth, frameHeight);
        var depthView = depthTexture.CreateView();

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
            pass.SetVertexBuffer(0, vertexBuffer);
            pass.SetIndexBuffer(indexBuffer, WGPUIndexFormat.Uint32);
            pass.DrawIndexed((uint)request.Indices.Length);
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

    /// <summary>Uploads clip-space vertices built for the current framebuffer aspect ratio.</summary>
    private static WgpuBuffer UploadVertices(Device device, FrameRequest request, uint width, uint height) =>
        device.CreateBufferInit(
            new BufferInitDescriptor { Usage = BufferUsage.Vertex },
            ClipSpaceTransform.Build(request, CameraMath.BuildViewProjection(
                request.CameraPosition,
                request.CameraTarget,
                request.FieldOfViewDegrees,
                (int)width,
                (int)height)).AsSpan());

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

    private static RenderPipeline CreatePipeline(Device device, WGPUTextureFormat format)
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
            layout: default,
            primitive: PrimitiveState.Default,
            multisample: MultisampleState.Default,
            depthStencil: DepthStencilState.Depth(WGPUTextureFormat.Depth32Float),
            vertexStride: (ulong)ClipSpaceTransform.VertexStride,
            vertexStepMode: WGPUVertexStepMode.Vertex,
            vertexAttributes:
            [
                new VertexAttribute(WGPUVertexFormat.Float32x4, 0, 0),
                new VertexAttribute(WGPUVertexFormat.Float32x3, 16, 1)
            ]);
    }
}
