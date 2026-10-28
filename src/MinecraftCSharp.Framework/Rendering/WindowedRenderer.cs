// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 MinecraftCSharp contributors

using Brew.Wgpu;
using Brew.Wgpu.Native;
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
/// </remarks>
public static class WindowedRenderer
{
    /// <summary>Runs a window showing the supplied geometry.</summary>
    /// <param name="request">Frame request; width and height are overridden by the window size.</param>
    /// <param name="frames">How many frames to present before closing (0 means until the user closes it).</param>
    /// <param name="maxDuration">Optional wall-clock limit, so an unthrottled window cannot flash by.</param>
    /// <param name="onFrame">Optional callback invoked after each presented frame index.</param>
    /// <returns>The surface format that was used, for diagnostics.</returns>
    public static WGPUTextureFormat Run(
        FrameRequest request,
        int frames,
        TimeSpan? maxDuration = null,
        Action<int>? onFrame = null)
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
        // A DPI-scaled display reports a logical size smaller than the framebuffer; the swapchain
        // must be configured in physical pixels or every acquire reports "outdated".
        var width = (uint)window.FramebufferWidth;
        var height = (uint)window.FramebufferHeight;
        Console.WriteLine($"window: framebuffer {width}x{height}, logical {window.Width}x{window.Height}");
        surface.Configure(device, format, TextureUsage.RenderAttachment, width, height);

        using var pipeline = CreatePipeline(device, format);
        using var vertexBuffer = device.CreateBufferInit(
            new BufferInitDescriptor { Usage = BufferUsage.Vertex },
            ClipSpaceTransform.Build(request, CameraMath.BuildViewProjection(
                request.CameraPosition,
                request.CameraTarget,
                request.FieldOfViewDegrees,
                (float)request.Width / request.Height)).AsSpan());
        using var indexBuffer = device.CreateBufferInit(
            new BufferInitDescriptor { Usage = BufferUsage.Index },
            request.Indices.AsSpan());

        using var depth = device.CreateTexture(new TextureDescriptor
        {
            Size = new WGPUExtent3D { width = width, height = height, depthOrArrayLayers = 1 },
            MipLevelCount = 1,
            SampleCount = 1,
            Dimension = WGPUTextureDimension._2D,
            Format = WGPUTextureFormat.Depth32Float,
            Usage = TextureUsage.RenderAttachment
        });
        using var depthView = depth.CreateView();

        var colour = request.ClearColour;
        var presented = 0;

        Console.WriteLine($"window: swapchain configured ({format}, {width}x{height})");

        var deadline = maxDuration is { } duration ? Environment.TickCount64 + (long)duration.TotalMilliseconds : long.MaxValue;

        while (true)
        {
            if (!window.Pump())
            {
                break;
            }

            if (Environment.TickCount64 > deadline)
            {
                break;
            }

            var acquired = surface.GetCurrentTexture();
            // Brew's NeedsReconfigure wraps around on the uint enum (SuccessOptimal reports as
            // needing a reconfigure), so the statuses are compared explicitly instead.
            if (acquired.Status is WGPUSurfaceGetCurrentTextureStatus.Outdated or WGPUSurfaceGetCurrentTextureStatus.Lost)
            {
                // The surface output must be released before the surface is reconfigured.
                acquired.Texture.Dispose();
                surface.Configure(device, format, TextureUsage.RenderAttachment, width, height);
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

            onFrame?.Invoke(presented);
            presented++;

            if (frames > 0 && presented >= frames)
            {
                break;
            }
        }

        return format;
    }

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
