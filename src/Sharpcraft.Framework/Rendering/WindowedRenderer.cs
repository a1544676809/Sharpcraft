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
        Func<IReadOnlyList<(long Key, GeometryData Mesh)>?>? geometryUpdates = null)
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

        // A DPI-scaled display reports a logical size smaller than the framebuffer; the swapchain
        // must be configured in physical pixels or every acquire reports "outdated".
        var frameWidth = (uint)window.FramebufferWidth;
        var frameHeight = (uint)window.FramebufferHeight;
        Console.WriteLine($"window: framebuffer {frameWidth}x{frameHeight}, logical {window.Width}x{window.Height}");

        surface.Configure(device, format, TextureUsage.RenderAttachment, frameWidth, frameHeight);

        var currentRequest = request;

        // With per-chunk geometry the world is drawn from an arena: a chunk that arrives writes only its own
        // bytes, instead of the whole world being recomposed and re-uploaded every frame
        // (docs/specs/network/play-movement.md 7.25).
        var arena = geometryUpdates is null ? null : new ChunkGeometryArena(device);

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

        Span<float> cameraBlock = stackalloc float[16];
        var cameraBytes = new byte[VertexLayout.CameraUniformSize];

        var colour = request.ClearColour;
        var presented = 0;
        var worstFrameMs = 0d;
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

        while (true)
        {
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
                Console.WriteLine($"window: resize {frameWidth}x{frameHeight} -> {nextWidth}x{nextHeight}");
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

                // The geometry is world-space and the camera travels in a uniform, so a resize needs no
                // re-upload at all.
            }

            // Rewriting the persistent buffers costs one upload; nothing is allocated or freed in the
            // steady state, so streaming new terrain does not hitch.
            if (arena is not null)
            {
                var updates = geometryUpdates!();
                if (updates is { Count: > 0 })
                {
                    foreach (var (key, mesh) in updates)
                    {
                        arena.Apply(key, mesh);
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
            worstFrameMs = Math.Max(worstFrameMs, nowMs - lastFrameAt);
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
            var cameraStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            var (eye, target) = cameraPerFrame is null
                ? (request.CameraPosition, request.CameraTarget)
                : cameraPerFrame(window);
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

            pass.SetBindGroup(0, bindGroup);

            if (arena is not null)
            {
                arena.Draw(pass);
            }
            else
            {
                pass.SetVertexBuffer(0, vertexBuffer);
                pass.SetIndexBuffer(indexBuffer, WGPUIndexFormat.Uint32);
                pass.DrawIndexed((uint)currentRequest.Indices.Length);
            }

            pass.End();

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
            $"window: {presented} frames in {frameClock.Elapsed.TotalSeconds:F1}s, worst frame {worstFrameMs:F1} ms");
        if (arena is not null)
        {
            Console.WriteLine(
                $"window: arena {arena.ChunkCount} chunks, uploaded {arena.UploadedBytes / (1024d * 1024d):F1} MB, "
                + $"{arena.FullRebuilds} full rebuilds");
            arena.Dispose();
        }

        Console.WriteLine(
            $"window: rebuild {rebuildCount} frames, avg {(rebuildCount == 0 ? 0 : rebuildTotalMs / rebuildCount):F1} ms, "
            + $"worst {worstRebuildMs:F1} ms, uploaded {uploadBytes / (1024d * 1024d):F1} MB; "
            + $"camera avg {(presented == 0 ? 0 : cameraTotalMs / presented):F1} ms, worst {worstCameraMs:F1} ms");

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
