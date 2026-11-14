// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Brew.Wgpu;
using Brew.Wgpu.Native;
using System.Numerics;

namespace Sharpcraft.Framework.Rendering;

/// <summary>
/// Offscreen WGPU renderer: renders one frame into a texture, reads it back and returns RGBA
/// pixels. All WGPU calls live in this assembly (docs/decisions/0003-wgpu-binding.md).
/// </summary>
/// <remarks>
/// Binding: Brew.Wgpu, whose managed bindings and bundled <c>wgpu_native</c> ship from the same
/// release. The earlier Silk.NET pairing bundled a native library whose buffer mapping was
/// stubbed, which made readback impossible (see ADR-0003).
/// </remarks>
public static class OffscreenRenderer
{
    /// <summary>Stand-in sprite table entry covering the whole texture, used when no atlas is bound (T-832).</summary>
    private static readonly float[] WholeAtlasRect = [0f, 0f, 1f, 1f];
    /// <summary>Shared canonical pass plan; the offscreen shell must keep this order when extracting the pipeline.</summary>
    public static IReadOnlyList<WorldRenderPass> PassPlan => WorldRenderPipeline.DefaultPasses;

    /// <summary>
    /// Full-screen gradient for the sky stage: a shade at the zenith fading to the horizon colour.
    /// </summary>
    /// <remarks>
    /// A first slice only: vanilla also draws a sky disc, stars and a sunrise band, which are deliberately absent rather
    /// than approximated. Colours are the daytime overworld values until the environment attribute table is extracted.
    /// </remarks>
    internal static readonly string SkyShaderSourceText = Shaders.ShaderLibrary.Load("sky");

    internal static readonly string ShaderSourceText = Shaders.ShaderLibrary.Load("terrain");

    /// <summary>Describes the adapter that would be used, for diagnostics and acceptance runs.</summary>
    /// <exception cref="RenderingUnavailableException">No adapter is available.</exception>
    public static string DescribeAdapter()
    {
        try
        {
            var instance = Instance.Create();
            using var adapter = NativeAdapterSelection.Resolve(instance, spec: null);
            return adapter.GetInfo().Description;
        }
        catch (AdapterRequestException exception)
        {
            throw new RenderingUnavailableException($"no WGPU adapter available: {exception.Message}");
        }
    }

    /// <summary>Builds the sky pipeline: no vertex buffer, no depth, drawn first over the cleared target.</summary>
    /// <param name="device">Device.</param>
    /// <param name="format">Colour target format.</param>
    /// <summary>
    /// Compares sampled motion vectors against the same computation done here, pixel by pixel.
    /// </summary>
    private static void CompareMotion(
        WgpuRenderBackend backend,
        Instance instance,
        BufferHandle depthReadback,
        ulong depthSize,
        int depthPadded,
        BufferHandle motionReadback,
        System.Numerics.Matrix4x4 viewProjection,
        FrameRequest request,
        int width,
        int height)
    {
        System.Numerics.Matrix4x4.Invert(viewProjection, out var inverse);
        var shift = request.Diagnostics?.MotionShiftX ?? 0f;
        var shiftBy = new System.Numerics.Vector3(shift == 0f ? 1f : shift, 0f, 0f);
        var previous = CameraMath.BuildViewProjection(
            request.CameraPosition + shiftBy,
            request.CameraTarget + shiftBy,
            request.FieldOfViewDegrees,
            (float)request.Width / request.Height);

        var motionBytes = (ulong)(width * height * 8);

        // Both blocks come back through the seam: it maps, copies and unmaps, so the comparison only sees bytes.
        var depthMapped = backend.ReadBuffer(instance, depthReadback, 0, (nuint)depthSize);
        var motionMapped = backend.ReadBuffer(instance, motionReadback, 0, (nuint)motionBytes);
        var samples = 0;
        var mismatches = 0;
        var worst = 0f;

        for (var y = 16; y < height; y += 31)
        {
            for (var x = 16; x < width; x += 31)
            {
                var window = BitConverter.ToSingle(depthMapped.AsSpan((y * depthPadded) + (x * 4), 4));

                // Reversed-Z: the sky / far plane is depth 0 (depth 1 is the near plane).
                if (window <= 0f)
                {
                    continue;
                }

                var u = (x + 0.5f) / width;
                var v = (y + 0.5f) / height;
                var clip = new System.Numerics.Vector4((u * 2f) - 1f, 1f - (v * 2f), window, 1f);
                var world = System.Numerics.Vector4.Transform(clip, inverse);
                var point = new System.Numerics.Vector3(world.X / world.W, world.Y / world.W, world.Z / world.W);
                var projected = System.Numerics.Vector4.Transform(new System.Numerics.Vector4(point, 1f), previous);

                if (projected.W <= 0f)
                {
                    continue;
                }

                var previousU = ((projected.X / projected.W) * 0.5f) + 0.5f;
                var previousV = 0.5f - ((projected.Y / projected.W) * 0.5f);
                var expectedX = (previousU - u) * width;
                var expectedY = (previousV - v) * height;

                var offset = ((y * width) + x) * 8;
                var gpuX = BitConverter.ToSingle(motionMapped.AsSpan(offset, 4));
                var gpuY = BitConverter.ToSingle(motionMapped.AsSpan(offset + 4, 4));

                samples++;
                var delta = MathF.Max(MathF.Abs(gpuX - expectedX), MathF.Abs(gpuY - expectedY));

                if (delta > 0.01f)
                {
                    mismatches++;
                    worst = MathF.Max(worst, delta);
                }
            }
        }

        Console.WriteLine($"motion: {samples} samples, mismatches {mismatches}, worst delta {worst:F4} px");
    }

    private static void WriteMatrix(System.Numerics.Matrix4x4 matrix, float[] target, int offset)
    {
        // The C# matrix transforms row vectors and WGSL transforms column vectors, so the rows are uploaded as columns:
        // that transposes it, which is exactly the conversion needed.
        target[offset + 0] = matrix.M11;
        target[offset + 1] = matrix.M12;
        target[offset + 2] = matrix.M13;
        target[offset + 3] = matrix.M14;
        target[offset + 4] = matrix.M21;
        target[offset + 5] = matrix.M22;
        target[offset + 6] = matrix.M23;
        target[offset + 7] = matrix.M24;
        target[offset + 8] = matrix.M31;
        target[offset + 9] = matrix.M32;
        target[offset + 10] = matrix.M33;
        target[offset + 11] = matrix.M34;
        target[offset + 12] = matrix.M41;
        target[offset + 13] = matrix.M42;
        target[offset + 14] = matrix.M43;
        target[offset + 15] = matrix.M44;
    }

    /// <summary>Reads the depth buffer back and prints its range, proving depth is available for the occlusion pass.</summary>
    private static float[]? ReportDepth(WgpuRenderBackend backend, Instance instance, BufferHandle depthReadback, ulong depthSize, int depthPadded, int width, int height)
    {
        byte[] mapped;
        try
        {
            mapped = backend.ReadBuffer(instance, depthReadback, 0, (nuint)depthSize);
        }
        catch (InvalidOperationException)
        {
            Console.WriteLine("depth: readback failed");
            return null;
        }

        var depths = new float[width * height];
        // Reversed-Z: depth 1 is the near plane and 0 the far plane, so the physically nearest pixel carries the LARGEST
        // raw depth. Track both ends of the raw range and convert each to a distance for the report.
        var minRaw = float.MaxValue;
        var maxRaw = 0f;
        var centre = BitConverter.ToSingle(mapped.AsSpan(((height / 2) * depthPadded) + ((width / 2) * 4), 4));

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var value = BitConverter.ToSingle(mapped.AsSpan((y * depthPadded) + (x * 4), 4));
                depths[(y * width) + x] = value;

                if (value < minRaw)
                {
                    minRaw = value;
                }

                if (value > maxRaw)
                {
                    maxRaw = value;
                }
            }
        }

        var near = 0.1f;
        var far = 2000f;
        Console.WriteLine(
            $"depth: {width}x{height} nearest {DepthLinearisation.ToDistance(maxRaw, near, far):F1} blocks (raw {maxRaw:F4}) " +
            $"farthest {DepthLinearisation.ToDistance(minRaw, near, far):F1} blocks (raw {minRaw:F4}) " +
            $"centre {DepthLinearisation.ToDistance(centre, near, far):F1} blocks");

        return depths;
    }

    /// <summary>
    /// Compares every pyramid level against the CPU reference, level by level, and prints the totals.
    /// </summary>
    private static void ComparePyramidChain(
        WgpuRenderBackend backend,
        Instance instance,
        BufferHandle depthReadback,
        ulong depthSize,
        int depthPadded,
        IReadOnlyList<BufferHandle> readbacks,
        IReadOnlyList<(int Width, int Height)> sizes,
        int width,
        int height)
    {
        byte[] depthMapped;
        try
        {
            depthMapped = backend.ReadBuffer(instance, depthReadback, 0, (nuint)depthSize);
        }
        catch (InvalidOperationException)
        {
            Console.WriteLine("pyramid: depth map failed");
            return;
        }

        var depth = new float[width * height];

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                depth[(y * width) + x] = DepthLinearisation.ToDistance(
                    BitConverter.ToSingle(depthMapped.AsSpan((y * depthPadded) + (x * 4), 4)),
                    0.1f,
                    2000f);
            }
        }

        var reference = DepthPyramid.Build(depth, width, height);
        var totalMismatches = 0;
        var worst = 0f;
        var lines = new List<string>();

        for (var level = 0; level < readbacks.Count; level++)
        {
            var (levelWidth, levelHeight) = sizes[level];
            var bytes = (ulong)(levelWidth * levelHeight * 4);

            byte[] mapped;
            try
            {
                mapped = backend.ReadBuffer(instance, readbacks[level], 0, (nuint)bytes);
            }
            catch (InvalidOperationException)
            {
                Console.WriteLine($"pyramid: level {level + 1} map failed");
                return;
            }

            var expected = reference[level + 1];
            var mismatches = 0;

            for (var index = 0; index < levelWidth * levelHeight; index++)
            {
                var delta = MathF.Abs(BitConverter.ToSingle(mapped.AsSpan(index * 4, 4)) - expected[index]);

                if (delta > 0.05f)
                {
                    mismatches++;
                    worst = MathF.Max(worst, delta);
                }
            }

            totalMismatches += mismatches;
            lines.Add($"{level + 1}:{levelWidth}x{levelHeight} mismatches {mismatches}");
        }

        Console.WriteLine($"pyramid: {readbacks.Count} levels, total mismatches {totalMismatches}, worst delta {worst:F6}; " + string.Join(", ", lines));
    }


    /// <summary>
    /// Position-colour pipeline: vertex colour times the per-draw tint, with the given reference blend mode. Sunrise uses
    /// Translucent; stars use Overlay (both match the reference pipelines' ColorTargetState).
    /// </summary>
    private static PipelineHandle CreatePositionColorPipeline(IRenderBackend backend, byte[] source, PipelineLayoutHandle layout, bool overlay, TextureFormat format = TextureFormat.Rgba8Unorm, WGPUCompareFunction depthCompare = WGPUCompareFunction.Always, bool opaque = false)
    {
        return backend.CreateRenderPipeline(new PipelineDesc(
            source,
            "vs_position_color",
            source,
            "fs_position_color",
            layout,
            [
                new ColorTargetDesc(
                    format,
                    opaque ? null : new BlendStateDesc(
                        new BlendComponentDesc(BlendOperation.Add, BlendFactor.SrcAlpha, overlay ? BlendFactor.One : BlendFactor.OneMinusSrcAlpha),
                        new BlendComponentDesc(BlendOperation.Add, overlay ? BlendFactor.Zero : BlendFactor.OneMinusSrcAlpha, BlendFactor.Zero)))
            ],
            // Part of the backdrop by default: depth always passes and nothing is written, so terrain covers it. Clouds pass
            // GreaterEqual instead so terrain and other clouds occlude them (drawn after the world).
            new DepthStateDesc(TextureFormat.Depth32Float, BackendConvert.FromWgpu(depthCompare), false),
            CullMode.None,
            FrontFace.Ccw,
            (ulong)VertexLayout.PositionColorStride,
            VertexStepMode.Vertex,
            LayoutAttributes.From(VertexLayout.PositionColorAttributes())));
    }

    /// <summary>Renders a frame and returns its pixels.</summary>
    /// <exception cref="RenderingUnavailableException">The GPU path failed.</exception>
    public static FrameResult Render(FrameRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Every pipeline is checked against its file once, so a layout that disagrees with its shader fails here rather
        // than as an opaque validation error during a draw.
        Shaders.ShaderPipelines.EnsureValid();

        var viewProjection = CameraMath.BuildViewProjection(
            request.CameraPosition,
            request.CameraTarget,
            request.FieldOfViewDegrees,
            (float)request.Width / request.Height);

        try
        {
            var instance = Instance.Create();
            using var adapter = NativeAdapterSelection.Resolve(instance, request.Diagnostics?.Gpu);
            var adapterName = adapter.GetInfo().Description;
            // Ask for the limits this adapter actually offers rather than the defaults, whose maxBufferSize is 256 MiB. A full
        // amplified view needs more than that for one geometry buffer, and the adapter's own figure is the honest ceiling.
        var deviceLimits = adapter.GetLimits();
        var deviceDescriptor = new DeviceDescriptor { RequiredLimits = deviceLimits };
        using var device = adapter.RequestDeviceBlocking(in deviceDescriptor);

            var width = (uint)request.Width;
            var height = (uint)request.Height;
            var backend = new WgpuRenderBackend(device);

            // Both targets are copy sources: the colour one feeds the screenshot, the depth one feeds the occlusion
            // pyramid, so the seam owns them and the copies below run through it as well.
            var texture = backend.CreateTexture(new TextureDesc(
                width,
                height,
                TextureFormat.Rgba8Unorm,
                1,
                TextureUsages.RenderAttachment | TextureUsages.CopySrc));

            var view = backend.CreateTextureView(texture);

            var depthTexture = backend.CreateTexture(new TextureDesc(
                width,
                height,
                TextureFormat.Depth32Float,
                1,
                TextureUsages.RenderAttachment | TextureUsages.CopySrc));

            var depthView = backend.CreateTextureView(depthTexture);

            // Atlas upload: nearest sampling with no mipmaps keeps pixel art crisp and stops the
            // half-texel inset from blending across sprite borders.
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

            // Mip levels one and up, each half the previous size.
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

            var atlasViewHandle = backend.CreateTextureView(atlasTexture);

            // Same sampler construction as the windowed path, with the request's filter choice preserved so a nearest
            // screenshot request still produces nearest pixels (single track).
            var atlasFilter = request.NearestTextureFilter ? FilterMode.Nearest : FilterMode.Linear;

            var atlasSamplerHandle = backend.CreateSampler(AddressMode.ClampToEdge, atlasFilter, atlasFilter, useMipmaps: true);

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

            // Sprite rectangle table: four floats per sprite, read once per vertex by both terrain passes. The vertex
            // carries only an index, so this is where the atlas bounds come back from (T-832). An empty table still
            // needs one entry so the binding is always valid.
            var spriteRectData = request.SpriteRects.Length > 0 ? request.SpriteRects : WholeAtlasRect;
            var spriteRectBytes = new byte[spriteRectData.Length * sizeof(float)];
            System.Runtime.InteropServices.MemoryMarshal.AsBytes(spriteRectData.AsSpan()).CopyTo(spriteRectBytes);
            var spriteRectBuffer = backend.CreateBufferInit(
                new BufferDesc((ulong)spriteRectBytes.Length, BufferUsages.Storage | BufferUsages.CopyDst),
                spriteRectBytes.AsSpan());

            // Camera uniform: one 4×4 matrix, rewritten per frame.
            var cameraBuffer = backend.CreateBufferInit(
                new BufferDesc(VertexLayout.CameraUniformSize, BufferUsages.Uniform | BufferUsages.CopyDst),
                new byte[VertexLayout.CameraUniformSize].AsSpan());

            // Fog: colour plus start and end distance in blocks.
            var fogBytes = new byte[32];
            var fogValues = new float[8];
            fogValues[0] = request.FogColour.X;
            fogValues[1] = request.FogColour.Y;
            fogValues[2] = request.FogColour.Z;
            fogValues[3] = request.FogColour.W;
            fogValues[4] = request.Diagnostics?.FogStart ?? 64f;
            fogValues[5] = request.Diagnostics?.FogEnd ?? 160f;
            // Always on, and pulled inward by rain - the same helper the window path uses.
            var atmospheric = AtmosphericFog.Range(request.RainLevel);
            fogValues[6] = atmospheric.Start;
            fogValues[7] = atmospheric.End;
            System.Runtime.InteropServices.MemoryMarshal.AsBytes(fogValues.AsSpan()).CopyTo(fogBytes);
            var fogBuffer = backend.CreateBufferInit(
                new BufferDesc(32, BufferUsages.Uniform | BufferUsages.CopyDst),
                fogBytes.AsSpan());
            backend.WriteBuffer(fogBuffer, fogBytes, 0);

            // Lightmap parameters (sky/block factors and colours); rewritten per frame so day/night needs no re-mesh.
            var lightmapBytes = new byte[Shaders.ShaderBindings.LightmapBytes];
            LightmapUniformCodec.Write(request.Lightmap with { Padding = request.GlintTime }, lightmapBytes);
            var lightmapBuffer = backend.CreateBufferInit(
                new BufferDesc(Shaders.ShaderBindings.LightmapBytes, BufferUsages.Uniform | BufferUsages.CopyDst),
                lightmapBytes.AsSpan());
            backend.WriteBuffer(lightmapBuffer, lightmapBytes, 0);

            Span<float> cameraBlock = stackalloc float[36];
            CameraUniform.Write(cameraBlock, request.CameraPosition, request.CameraTarget, request.FieldOfViewDegrees, (int)width, (int)height);
            var cameraBytes = new byte[VertexLayout.CameraUniformSize];
            for (var index = 0; index < 36; index++)
            {
                BitConverter.TryWriteBytes(cameraBytes.AsSpan(index * sizeof(float)), cameraBlock[index]);
            }

            backend.WriteBuffer(cameraBuffer, cameraBytes, 0);

            // The seam from here on: the scene body describes its layout, pipeline layout and buffers in handle terms, so
            // the passes only ever receive handles.

            // Celestial bodies (sun/moon): textured quads that sit in the sky backdrop. Built here so their buffers live
            // until the command buffer is submitted; drawn right after the sky gradient.
            using var celestial = request.CelestialDraws is { Length: > 0 } && request.CelestialTextures is { Length: > 0 }
                ? new CelestialResources(backend, request, cameraBuffer, VertexLayout.CameraUniformSize)
                : null;

            // Position-colour bodies (sunrise fan, stars): no texture, per-vertex colour times a tint.
            using var celestialColour = request.CelestialColorDraws is { Length: > 0 }
                ? new CelestialColorResources(backend, request, cameraBuffer, VertexLayout.CameraUniformSize)
                : null;

            var bindGroup = backend.CreateBindGroup(
                bindGroupLayout,
                [
                    BindGroupEntryDesc.Texture(0, atlasViewHandle),
                    BindGroupEntryDesc.Sampler(1, atlasSamplerHandle),
                    BindGroupEntryDesc.Uniform(VertexLayout.CameraBinding, cameraBuffer, 0, VertexLayout.CameraUniformSize),
                    BindGroupEntryDesc.Uniform(3, fogBuffer, 0, 32),
                    BindGroupEntryDesc.Uniform(Shaders.ShaderBindings.Lightmap, lightmapBuffer, 0, Shaders.ShaderBindings.LightmapBytes),
                    BindGroupEntryDesc.ReadOnlyStorage(VertexLayout.SpriteBinding, spriteRectBuffer, 0, (ulong)spriteRectBytes.Length)
                ]);

            // Readback rows must be padded to 256 bytes.
            var unpadded = (int)width * 4;
            var padded = (unpadded + 255) / 256 * 256;
            var size = (ulong)padded * height;

            var readback = backend.CreateBuffer(new BufferDesc(size, BufferUsages.MapRead | BufferUsages.CopyDst));

            // Depth rows are 4 bytes per pixel and padded the same way as colour.
            var depthPadded = (((int)width * 4) + 255) / 256 * 256;
            var depthSize = (ulong)depthPadded * height;
            var depthReadback = backend.CreateBuffer(new BufferDesc(depthSize, BufferUsages.MapRead | BufferUsages.CopyDst));

            // The frame is recorded by the shared executor into this shell's own target; the shell only owns the
            // target, the readback buffers and the compute diagnostics (docs/specs/render/single-track-executor.md).
            using var samplers = new SamplerCache();
            samplers.Initialize(device);
            using var entitySkin = new EntitySkinTextureGpu();
            using var itemAtlasGpu = new EntitySkinTextureGpu();
            var frameResources = new FrameResources(new FrameResources.Options
            {
                Backend = backend,
                Request = request,
                Format = WGPUTextureFormat.RGBA8Unorm,
                PipelineLayout = pipelineLayout,
                BindGroupLayout = bindGroupLayout,
                Samplers = samplers,
                CameraBuffer = cameraBuffer,
                FogBuffer = fogBuffer,
                LightmapBuffer = lightmapBuffer,
                BindGroup = bindGroup,
                EntitySkin = entitySkin,
                ItemAtlasGpu = itemAtlasGpu,
                SpriteBindGroups = [],
                Celestial = celestial,
                CelestialColour = celestialColour,
                Width = width,
                Height = height
            });
            var recordedFrame = FrameExecutor.Record(
                frameResources.ExecutorResources,
                frameResources.CreateFrame(new FrameTarget(view, depthView, width, height)));
            var encoder = recordedFrame.Encoder;

            backend.CopyTextureToBuffer(encoder, texture, readback, (uint)padded, width, height);

            backend.CopyTextureToBuffer(encoder, depthTexture, depthReadback, (uint)depthPadded, width, height);

            float[]? depthPixels = null;
            var pyramidLevels = new List<BufferHandle>();
            var pyramidReadbacks = new List<BufferHandle>();
            var pyramidParameters = new List<BufferHandle>();
            var pyramidSizes = new List<(int Width, int Height)>();
            var hasPyramid = request.Diagnostics?.BuildPyramid == true;

            if (hasPyramid)
            {
                // Source level zero: the depth pixels, copied a second time because a map-read buffer cannot be a copy source.
                var sourceBytes = (ulong)depthPadded * height;
                var source = backend.CreateBuffer(new BufferDesc(sourceBytes, BufferUsages.Storage | BufferUsages.CopyDst));

                backend.CopyTextureToBuffer(encoder, depthTexture, source, (uint)depthPadded, width, height);

                // The compute chain speaks the seam too, so the pass, its layout and every buffer are handles: the
                // pyramid is diagnosable without a single native object crossing the boundary.
                var computeLayout = backend.CreateBindGroupLayout(
                [
                    new BindGroupLayoutEntryDesc(4, ShaderStages.Compute, BindingKind.ReadOnlyStorageBuffer),
                    new BindGroupLayoutEntryDesc(5, ShaderStages.Compute, BindingKind.StorageBuffer),
                    new BindGroupLayoutEntryDesc(6, ShaderStages.Compute, BindingKind.UniformBuffer) with { MinBindingSize = 32 }
                ]);

                var computePipelineLayout = backend.CreatePipelineLayout([computeLayout]);
                var computePipelineHandle = backend.CreateComputePipeline(new ComputePipelineDesc(
                    System.Text.Encoding.UTF8.GetBytes(Shaders.ShaderLibrary.Load("depth_pyramid")),
                    "cs_downsample",
                    computePipelineLayout));

                var levelWidth = (int)width;
                var levelHeight = (int)height;
                var levelStride = depthPadded / 4;
                var levelSource = source;
                // The shader needs to know whether it is reading the padded depth copy (first level) or an earlier level.
                var sourceIsLevelZero = true;

                while (levelWidth > 1 || levelHeight > 1)
                {
                    var nextWidth = Math.Max(1, levelWidth / 2);
                    var nextHeight = Math.Max(1, levelHeight / 2);
                    var nextBytes = (ulong)(nextWidth * nextHeight * 4);

                    var destination = backend.CreateBuffer(new BufferDesc(nextBytes, BufferUsages.Storage | BufferUsages.CopySrc));

                    var levelReadback = backend.CreateBuffer(new BufferDesc(nextBytes, BufferUsages.MapRead | BufferUsages.CopyDst));

                    var parameters = new uint[]
                    {
                        (uint)levelWidth, (uint)levelHeight, (uint)levelStride, sourceIsLevelZero ? 1u : 0u,
                        0x3DCCCCCDu, 0x44FA0000u, 0, 0
                    };
                    var parameterBytes = new byte[32];
                    System.Runtime.InteropServices.MemoryMarshal.AsBytes(parameters.AsSpan()).CopyTo(parameterBytes);
                    var parameterBuffer = backend.CreateBufferInit(
                        new BufferDesc(32, BufferUsages.Uniform | BufferUsages.CopyDst),
                        parameterBytes.AsSpan());

                    var levelBindGroup = backend.CreateBindGroup(
                        computeLayout,
                        [
                            BindGroupEntryDesc.ReadOnlyStorage(4, levelSource, 0, (ulong)levelStride * 4 * (ulong)levelHeight),
                            BindGroupEntryDesc.Storage(5, destination, 0, nextBytes),
                            BindGroupEntryDesc.Uniform(6, parameterBuffer, 0, 32)
                        ]);

                    var computePass = backend.BeginComputePass(encoder);
                    backend.SetPipeline(computePass, computePipelineHandle);
                    backend.SetBindGroup(computePass, 0, levelBindGroup);
                    backend.DispatchWorkgroups(
                        computePass,
                        (uint)Math.Max(1, (nextWidth + 7) / 8),
                        (uint)Math.Max(1, (nextHeight + 7) / 8),
                        1);
                    backend.EndComputePass(computePass);

                    backend.CopyBufferToBuffer(encoder, destination, 0, levelReadback, 0, nextBytes);

                    pyramidLevels.Add(destination);
                    pyramidReadbacks.Add(levelReadback);
                    pyramidParameters.Add(parameterBuffer);
                    pyramidSizes.Add((nextWidth, nextHeight));

                    levelSource = destination;
                    sourceIsLevelZero = false;
                    levelWidth = nextWidth;
                    levelHeight = nextHeight;
                    levelStride = nextWidth;
                }

                // Recorded commands keep their own references, so the per-chain objects are released as soon as the last
                // level is recorded (the same lifetime rule the scene pipelines follow).
                backend.Destroy(computePipelineHandle);
                backend.Destroy(computePipelineLayout);
                backend.Destroy(computeLayout);
                backend.Destroy(source);
            }

            BufferHandle motionOutput = default;
            BufferHandle motionReadback = default;
            var hasMotion = request.Diagnostics?.MotionVectors == true;

            if (hasMotion)
            {
                var sourceBytes = (ulong)depthPadded * height;
                var depthForMotion = backend.CreateBuffer(new BufferDesc(sourceBytes, BufferUsages.Storage | BufferUsages.CopyDst));

                backend.CopyTextureToBuffer(encoder, depthTexture, depthForMotion, (uint)depthPadded, width, height);

                var motionBytes = (ulong)((int)width * (int)height * 8);
                motionOutput = backend.CreateBuffer(new BufferDesc(motionBytes, BufferUsages.Storage | BufferUsages.CopySrc));
                motionReadback = backend.CreateBuffer(new BufferDesc(motionBytes, BufferUsages.MapRead | BufferUsages.CopyDst));

                var motionParameters = new uint[] { width, height, (uint)(depthPadded / 4), 0 };
                var motionParameterBytes = new byte[16];
                System.Runtime.InteropServices.MemoryMarshal.AsBytes(motionParameters.AsSpan()).CopyTo(motionParameterBytes);
                var motionParameterBuffer = backend.CreateBufferInit(
                    new BufferDesc(16, BufferUsages.Uniform | BufferUsages.CopyDst),
                    motionParameterBytes.AsSpan());

                // Previous camera: the same view shifted along X, so the motion has a known sign and magnitude.
                var shift = request.Diagnostics?.MotionShiftX ?? 0f;
                var shiftBy = new System.Numerics.Vector3(shift == 0f ? 1f : shift, 0f, 0f);
                var previousViewProjection = CameraMath.BuildViewProjection(
                    request.CameraPosition + shiftBy,
                    request.CameraTarget + shiftBy,
                    request.FieldOfViewDegrees,
                    (float)request.Width / request.Height);

                System.Numerics.Matrix4x4.Invert(viewProjection, out var inverseViewProjection);
                var matrixBytes = new byte[128];
                var matrixFloats = new float[32];
                WriteMatrix(inverseViewProjection, matrixFloats, 0);
                WriteMatrix(previousViewProjection, matrixFloats, 16);
                System.Runtime.InteropServices.MemoryMarshal.AsBytes(matrixFloats.AsSpan()).CopyTo(matrixBytes);
                var motionMatrixBuffer = backend.CreateBufferInit(
                    new BufferDesc(128, BufferUsages.Uniform | BufferUsages.CopyDst),
                    matrixBytes.AsSpan());

                var motionLayout = backend.CreateBindGroupLayout(
                [
                    new BindGroupLayoutEntryDesc(4, ShaderStages.Compute, BindingKind.ReadOnlyStorageBuffer),
                    new BindGroupLayoutEntryDesc(5, ShaderStages.Compute, BindingKind.StorageBuffer),
                    new BindGroupLayoutEntryDesc(6, ShaderStages.Compute, BindingKind.UniformBuffer) with { MinBindingSize = 16 },
                    new BindGroupLayoutEntryDesc(7, ShaderStages.Compute, BindingKind.UniformBuffer) with { MinBindingSize = 128 }
                ]);

                var motionPipelineHandle = backend.CreateComputePipeline(new ComputePipelineDesc(
                    System.Text.Encoding.UTF8.GetBytes(Shaders.ShaderLibrary.Load("motion_vectors")),
                    "cs_motion",
                    backend.CreatePipelineLayout([motionLayout])));

                var motionBindGroup = backend.CreateBindGroup(
                    motionLayout,
                    [
                        BindGroupEntryDesc.ReadOnlyStorage(4, depthForMotion, 0, sourceBytes),
                        BindGroupEntryDesc.Storage(5, motionOutput, 0, motionBytes),
                        BindGroupEntryDesc.Uniform(6, motionParameterBuffer, 0, 16),
                        BindGroupEntryDesc.Uniform(7, motionMatrixBuffer, 0, 128)
                    ]);

                var motionPass = backend.BeginComputePass(encoder);
                backend.SetPipeline(motionPass, motionPipelineHandle);
                backend.SetBindGroup(motionPass, 0, motionBindGroup);
                backend.DispatchWorkgroups(
                    motionPass,
                    (uint)Math.Max(1, ((int)width + 7) / 8),
                    (uint)Math.Max(1, ((int)height + 7) / 8),
                    1);
                backend.EndComputePass(motionPass);

                backend.CopyBufferToBuffer(encoder, motionOutput, 0, motionReadback, 0, motionBytes);
                backend.Destroy(depthForMotion);
                backend.Destroy(motionBindGroup);
                backend.Destroy(motionPipelineHandle);
                backend.Destroy(motionLayout);
                backend.Destroy(motionParameterBuffer);
                backend.Destroy(motionMatrixBuffer);
                backend.Destroy(motionOutput);
            }

            backend.Submit(encoder);

            // The readback itself is seam-shaped: it maps, copies and unmaps, so the rows arrive as plain bytes.
            var rowBlock = backend.ReadBuffer(instance, readback, 0, (nuint)size);
            var pixels = new byte[unpadded * (int)height];
            for (var y = 0; y < (int)height; y++)
            {
                rowBlock.AsSpan(y * padded, unpadded).CopyTo(pixels.AsSpan(y * unpadded, unpadded));
            }

            backend.Destroy(readback);

            if (request.Diagnostics?.ReadDepth == true)
            {
                depthPixels = ReportDepth(backend, instance, depthReadback, depthSize, depthPadded, (int)width, (int)height);
            }

            if (hasPyramid)
            {
                ComparePyramidChain(backend, instance, depthReadback, depthSize, depthPadded, pyramidReadbacks, pyramidSizes, (int)width, (int)height);
            }

            if (hasMotion)
            {
                CompareMotion(backend, instance, depthReadback, depthSize, depthPadded, motionReadback, viewProjection, request, (int)width, (int)height);
            }

            backend.Destroy(depthReadback);
            foreach (var handle in pyramidLevels)
            {
                backend.Destroy(handle);
            }

            foreach (var handle in pyramidReadbacks)
            {
                backend.Destroy(handle);
            }

            foreach (var handle in pyramidParameters)
            {
                backend.Destroy(handle);
            }

            if (motionReadback.IsValid)
            {
                backend.Destroy(motionReadback);
            }

            backend.Destroy(depthView);
            backend.Destroy(depthTexture);
            backend.Destroy(view);
            backend.Destroy(texture);

            // FrameResources owns the pipelines, layouts, OIT targets and geometry buffers; this shell only owns
            // its target, atlas and readback resources.
            frameResources.DynamicBuffers.Destroy();
            for (var layerIndex = 0; layerIndex < frameResources.LayerPool.Count; layerIndex++)
            {
                frameResources.DeferredRelease.Release(frameResources.LayerPool.Vertex(layerIndex));
                frameResources.DeferredRelease.Release(frameResources.LayerPool.Index(layerIndex));
            }

            frameResources.UiBuffers.Destroy();
            frameResources.StaticWorld.Destroy();
            frameResources.Dispose();

            backend.Destroy(bindGroup);
            backend.Destroy(atlasSamplerHandle);
            backend.Destroy(atlasViewHandle);
            backend.Destroy(atlasTexture);
            backend.Destroy(lightmapBuffer);
            backend.Destroy(fogBuffer);
            backend.Destroy(cameraBuffer);
            backend.Destroy(pipelineLayout);
            backend.Destroy(bindGroupLayout);

            return new FrameResult(request.Width, request.Height, pixels, viewProjection, adapterName, depthPixels);
        }
        catch (ResourceCreationException exception)
        {
            throw new RenderingUnavailableException($"WGPU resource creation failed: {exception.Message}");
        }
        catch (DeviceRequestException exception)
        {
            throw new RenderingUnavailableException($"WGPU device request failed: {exception.Message}");
        }
        catch (AdapterRequestException exception)
        {
            throw new RenderingUnavailableException($"WGPU adapter request failed: {exception.Message}");
        }
    }

    /// <summary>
    /// Owns the GPU objects for one frame's celestial bodies: textures, a shared bind-group layout and pipeline, plus a
    /// vertex/index/transform buffer and bind group per draw. Built per frame and disposed with it.
    /// </summary>
    internal sealed class CelestialResources : IDisposable
    {
        // mat4x4 model (64 bytes) followed by vec4 tint (16 bytes); matches celestial.wgsl CelestialTransform.
        private const ulong TransformUniformSize = 80;

        private readonly IRenderBackend _backend;
        private readonly TextureHandle[] _textures;
        private readonly TextureViewHandle[] _views;
        private readonly SamplerHandle _sampler;
        private readonly BindGroupLayoutHandle _layout;
        private readonly PipelineLayoutHandle _pipelineLayout;
        private readonly PipelineHandle _pipeline;
        private BufferHandle[] _vertexBuffers = [];
        private BufferHandle[] _indexBuffers = [];
        private BindGroupHandle[] _bindGroups = [];
        private uint[] _indexCounts = [];
        private readonly BufferHandle _cameraBuffer;
        private readonly ulong _cameraSize;
        private readonly List<(BufferHandle Vertex, BufferHandle Index, BindGroupHandle BindGroup)> _retired = new();

        public CelestialResources(IRenderBackend backend, FrameRequest request, BufferHandle cameraBuffer, ulong cameraSize, TextureFormat format = TextureFormat.Rgba8Unorm)
        {
            var textures = request.CelestialTextures!;
            var draws = request.CelestialDraws!;
            _backend = backend;
            _cameraBuffer = cameraBuffer;
            _cameraSize = cameraSize;

            _textures = new TextureHandle[textures.Length];
            _views = new TextureViewHandle[textures.Length];

            for (var index = 0; index < textures.Length; index++)
            {
                var data = textures[index];
                _textures[index] = backend.CreateTexture(new TextureDesc(
                    (uint)data.Width,
                    (uint)data.Height,
                    TextureFormat.Rgba8Unorm,
                    1,
                    TextureUsages.TextureBinding | TextureUsages.CopyDst));
                backend.WriteTexture(
                    _textures[index],
                    data.Rgba,
                    (uint)(data.Width * 4),
                    (uint)data.Width,
                    (uint)data.Height,
                    0);
                _views[index] = backend.CreateTextureView(_textures[index]);
            }

            // Nearest sampling keeps the crisp pixel-art sun and moon discs from smearing.
            _sampler = backend.CreateSampler(AddressMode.ClampToEdge, FilterMode.Nearest, FilterMode.Nearest, useMipmaps: false);

            _layout = backend.CreateBindGroupLayout(
            [
                BindGroupLayoutEntryDesc.UniformBuffer(0, ShaderStages.Vertex) with { MinBindingSize = cameraSize },
                BindGroupLayoutEntryDesc.Texture(1, ShaderStages.Fragment),
                BindGroupLayoutEntryDesc.Sampler(2, ShaderStages.Fragment)
            ]);

            _pipelineLayout = backend.CreatePipelineLayout([_layout]);

            // Described to the seam rather than built from a native shader module, so every pipeline is created in one place.
            var source = System.Text.Encoding.UTF8.GetBytes(Shaders.ShaderLibrary.Load("celestial"));
            _pipeline = backend.CreateRenderPipeline(new PipelineDesc(
                source,
                "vs_celestial",
                source,
                "fs_celestial",
                _pipelineLayout,
                [
                    // The reference draws celestial bodies with BlendFunction.OVERLAY: colour SrcAlpha/One, alpha One/Zero.
                    // That adds the body onto the sky and makes black/transparent pixels contribute nothing.
                    new ColorTargetDesc(
                        format,
                        new BlendStateDesc(
                            new BlendComponentDesc(BlendOperation.Add, BlendFactor.SrcAlpha, BlendFactor.One),
                            new BlendComponentDesc(BlendOperation.Add, BlendFactor.One, BlendFactor.Zero)))
                ],
                // Bodies are part of the backdrop: depth always passes and nothing is written, so terrain covers them.
                new DepthStateDesc(TextureFormat.Depth32Float, CompareFunction.Always, false),
                CullMode.None,
                FrontFace.Ccw,
                (ulong)VertexLayout.CelestialStride,
                VertexStepMode.Vertex,
                LayoutAttributes.From(VertexLayout.CelestialAttributes())));

            SetDraws(draws);
        }

        /// <summary>Replaces the sun/moon draws while keeping the loaded textures and pipeline (per-frame angles).</summary>
        public void SetDraws(IReadOnlyList<CelestialQuadDraw> draws)
        {
            foreach (var (vertex, index, bindGroup) in _retired)
            {
                _backend.Destroy(bindGroup);
                _backend.Destroy(index);
                _backend.Destroy(vertex);
            }

            _retired.Clear();
            for (var i = 0; i < _bindGroups.Length; i++)
            {
                _retired.Add((_vertexBuffers[i], _indexBuffers[i], _bindGroups[i]));
            }

            _vertexBuffers = new BufferHandle[draws.Count];
            _indexBuffers = new BufferHandle[draws.Count];
            _bindGroups = new BindGroupHandle[draws.Count];
            _indexCounts = new uint[draws.Count];

            for (var index = 0; index < draws.Count; index++)
            {
                var draw = draws[index];
                // The model matrix is deliberately NOT applied: it only carries the camera translation, which the sky
                // matrix in the camera uniform now provides by not being applied at all (T-811, T-812). The vertices stay
                // exactly as CelestialGeometry produced them, on a dome around the origin.
                var vertices = CopyTint(draw.Vertices, draw.Tint);
                _vertexBuffers[index] = _backend.CreateBufferInit(
                    new BufferDesc((ulong)vertices.Length * sizeof(float), BufferUsages.Vertex),
                    System.Runtime.InteropServices.MemoryMarshal.AsBytes(vertices.AsSpan()));
                _indexBuffers[index] = _backend.CreateBufferInit(
                    new BufferDesc((ulong)draw.Indices.Length * sizeof(ushort), BufferUsages.Index),
                    System.Runtime.InteropServices.MemoryMarshal.AsBytes(draw.Indices.AsSpan()));

                _bindGroups[index] = _backend.CreateBindGroup(
                    _layout,
                    [
                        BindGroupEntryDesc.Uniform(0, _cameraBuffer, 0, _cameraSize),
                        BindGroupEntryDesc.Texture(1, _views[draw.TextureIndex]),
                        BindGroupEntryDesc.Sampler(2, _sampler)
                    ]);

                _indexCounts[index] = (uint)draw.Indices.Length;
            }
        }

        /// <summary>Draws every body with the shared pipeline; each has its own texture, transform and buffers.</summary>
        public void Draw(IRenderBackend backend, RenderPassHandle pass)
        {
            if (_bindGroups.Length == 0)
            {
                return;
            }

            backend.SetPipeline(pass, _pipeline);

            for (var index = 0; index < _bindGroups.Length; index++)
            {
                backend.SetBindGroup(pass, 0, _bindGroups[index]);
                backend.SetVertexBuffer(pass, 0, _vertexBuffers[index]);
                backend.SetIndexBuffer(pass, _indexBuffers[index], IndexFormat.Uint16);
                backend.DrawIndexed(pass, _indexCounts[index], 1, 0, 0, 0);
            }
        }

        /// <summary>
        /// Kept for the call sites: the dome is re-centred by the shared sky matrix, so a camera move uploads nothing.
        /// </summary>
        /// <param name="eyeDelta">Unused; the bodies follow the camera through the camera uniform (T-812).</param>
        public void UpdateCamera(Vector3 eyeDelta)
        {
            _ = eyeDelta;
            NoOpCameraUpdates++;
        }

        /// <summary>Camera updates that still touched a buffer; zero now that the sky matrix carries the camera (T-812).</summary>
        public long AppliedCameraUpdates { get; private set; }

        /// <summary>Camera updates skipped by the old threshold; kept so the existing diagnostics still compile.</summary>
        public long SkippedCameraUpdates => 0;

        /// <summary>How often the call site asked the bodies to follow the camera; every one is a no-op now.</summary>
        public long NoOpCameraUpdates { get; private set; }

        /// <summary>
        /// Copies the per-body tint onto every vertex, so the shader needs no per-draw uniform (T-812).
        /// </summary>
        /// <param name="vertices">Source vertices: position (3) then uv (2) floats per vertex.</param>
        /// <param name="tint">Tint copied onto every vertex.</param>
        /// <returns>The vertex array with the tint appended: position (3), uv (2), tint (4).</returns>
        private static float[] CopyTint(float[] vertices, Vector4 tint)
        {
            const int sourceStride = 5;
            const int bakedStride = 9;
            var count = vertices.Length / sourceStride;
            var result = new float[count * bakedStride];
            for (var vertex = 0; vertex < count; vertex++)
            {
                var source = vertex * sourceStride;
                var target = vertex * bakedStride;
                result[target] = vertices[source];
                result[target + 1] = vertices[source + 1];
                result[target + 2] = vertices[source + 2];
                result[target + 3] = vertices[source + 3];
                result[target + 4] = vertices[source + 4];
                result[target + 5] = tint.X;
                result[target + 6] = tint.Y;
                result[target + 7] = tint.Z;
                result[target + 8] = tint.W;
            }

            return result;
        }

        public void Dispose()
        {
            foreach (var bindGroup in _bindGroups)
            {
                _backend.Destroy(bindGroup);
            }


            foreach (var buffer in _indexBuffers)
            {
                _backend.Destroy(buffer);
            }

            foreach (var buffer in _vertexBuffers)
            {
                _backend.Destroy(buffer);
            }

            foreach (var (vertex, index, bindGroup) in _retired)
            {
                _backend.Destroy(bindGroup);
                _backend.Destroy(index);
                _backend.Destroy(vertex);
            }

            _backend.Destroy(_pipeline);
            _backend.Destroy(_pipelineLayout);
            _backend.Destroy(_layout);
            _backend.Destroy(_sampler);

            foreach (var view in _views)
            {
                _backend.Destroy(view);
            }

            foreach (var texture in _textures)
            {
                _backend.Destroy(texture);
            }
        }
    }

    /// <summary>
    /// Owns the GPU objects for one frame's position-colour celestial draws (sunrise fan, stars): a pipeline per blend
    /// mode and, per draw, a vertex/index/transform buffer and bind group. Built per frame and disposed with it.
    /// </summary>
    internal sealed class CelestialColorResources : IDisposable
    {
        private const ulong TransformUniformSize = 16;

        private readonly IRenderBackend _backend;
        private readonly BindGroupLayoutHandle _layout;
        private readonly PipelineLayoutHandle _pipelineLayout;
        private readonly PipelineHandle _translucentPipeline;
        private readonly PipelineHandle _overlayPipeline;
        private readonly PipelineHandle _opaquePipeline;
        private BufferHandle[] _vertexBuffers = [];
        private BufferHandle[] _indexBuffers = [];
        private BufferHandle[] _transformBuffers = [];

        private readonly byte[] _tintScratch = new byte[TransformUniformSize];
        private BindGroupHandle[] _bindGroups = [];
        private uint[] _indexCounts = [];
        private bool[] _overlay = [];
        private bool[] _opaque = [];
        private Vector4[] _tints = [];
        private Vector4[] _writtenTints = [];
        private readonly List<(BufferHandle Vertex, BufferHandle Index, BufferHandle Transform, BindGroupHandle BindGroup)> _retired = new();
        private readonly BufferHandle _cameraBuffer;
        private readonly ulong _cameraSize;
        private long[] _vertexCapacity = [];
        private long[] _indexCapacity = [];
        private float[][] _sourceVertices = [];
        private uint[][] _sourceIndices = [];

        public CelestialColorResources(IRenderBackend backend, FrameRequest request, BufferHandle cameraBuffer, ulong cameraSize, TextureFormat format = TextureFormat.Rgba8Unorm, WGPUCompareFunction depthCompare = WGPUCompareFunction.Always)
        {
            var draws = request.CelestialColorDraws ?? [];
            _backend = backend;
            _cameraBuffer = cameraBuffer;
            _cameraSize = cameraSize;
            _layout = backend.CreateBindGroupLayout(
            [
                BindGroupLayoutEntryDesc.UniformBuffer(0, ShaderStages.Vertex) with { MinBindingSize = cameraSize },
                BindGroupLayoutEntryDesc.UniformBuffer(1, ShaderStages.Vertex | ShaderStages.Fragment) with { MinBindingSize = TransformUniformSize }
            ]);

            _pipelineLayout = backend.CreatePipelineLayout([_layout]);

            // The seam takes the source rather than a module, so this builds the pipeline in one place like every other.
            var source = System.Text.Encoding.UTF8.GetBytes(Shaders.ShaderLibrary.Load("position_color"));
            _translucentPipeline = CreatePositionColorPipeline(backend, source, _pipelineLayout, overlay: false, format, depthCompare);
            _overlayPipeline = CreatePositionColorPipeline(backend, source, _pipelineLayout, overlay: true, format, depthCompare);
            _opaquePipeline = CreatePositionColorPipeline(backend, source, _pipelineLayout, overlay: false, format, depthCompare, opaque: true);

            SetDraws(draws);
        }

        /// <summary>
        /// Replaces the draw set (used every frame for clouds). The previous buffers are retired and released on the next
        /// update, so a buffer is not destroyed while the frame that referenced it may still be in flight.
        /// </summary>
        public void SetDraws(IReadOnlyList<CelestialColorDraw> draws)
        {
            var reuse = _bindGroups.Length == draws.Count && _vertexCapacity.Length == draws.Count;
            if (reuse)
            {
                for (var i = 0; i < draws.Count; i++)
                {
                    var draw = draws[i];
                    if ((long)draw.Vertices.Length * sizeof(float) > _vertexCapacity[i]
                        || (long)draw.Indices.Length * sizeof(uint) > _indexCapacity[i])
                    {
                        reuse = false;
                        break;
                    }
                }
            }

            if (reuse)
            {
                for (var i = 0; i < draws.Count; i++)
                {
                    WriteDraw(i, draws[i]);
                }

                return;
            }

            // Capacity/structure changed: recreate buffers (rare - only at start or when the cloud face count grows).
            foreach (var (vertex, index, transform, bindGroup) in _retired)
            {
                _backend.Destroy(bindGroup);
                _backend.Destroy(transform);
                _backend.Destroy(index);
                _backend.Destroy(vertex);
            }

            _retired.Clear();
            for (var i = 0; i < _bindGroups.Length; i++)
            {
                _retired.Add((_vertexBuffers[i], _indexBuffers[i], _transformBuffers[i], _bindGroups[i]));
            }

            _vertexBuffers = new BufferHandle[draws.Count];
            _indexBuffers = new BufferHandle[draws.Count];
            _transformBuffers = new BufferHandle[draws.Count];
            _bindGroups = new BindGroupHandle[draws.Count];
            _indexCounts = new uint[draws.Count];
            _overlay = new bool[draws.Count];
            _opaque = new bool[draws.Count];
            _tints = new Vector4[draws.Count];
            _writtenTints = new Vector4[draws.Count];
            Array.Fill(_writtenTints, new Vector4(float.NaN, float.NaN, float.NaN, float.NaN));
            _vertexCapacity = new long[draws.Count];
            _indexCapacity = new long[draws.Count];
            _sourceVertices = new float[draws.Count][];
            _sourceIndices = new uint[draws.Count][];

            for (var index = 0; index < draws.Count; index++)
            {
                var draw = draws[index];
                _vertexCapacity[index] = Math.Max((long)draw.Vertices.Length * sizeof(float), 4096);
                _indexCapacity[index] = Math.Max((long)draw.Indices.Length * sizeof(uint), 4096);
                _vertexBuffers[index] = _backend.CreateBuffer(
                    new BufferDesc((ulong)_vertexCapacity[index], BufferUsages.Vertex | BufferUsages.CopyDst));
                _indexBuffers[index] = _backend.CreateBuffer(
                    new BufferDesc((ulong)_indexCapacity[index], BufferUsages.Index | BufferUsages.CopyDst));
                _transformBuffers[index] = _backend.CreateBuffer(
                    new BufferDesc(TransformUniformSize, BufferUsages.Uniform | BufferUsages.CopyDst));

                _bindGroups[index] = _backend.CreateBindGroup(
                    _layout,
                    [
                        BindGroupEntryDesc.Uniform(0, _cameraBuffer, 0, _cameraSize),
                        BindGroupEntryDesc.Uniform(1, _transformBuffers[index], 0, TransformUniformSize)
                    ]);

                WriteDraw(index, draw);
            }
        }

        private void WriteDraw(int index, CelestialColorDraw draw)
        {
            _tints[index] = draw.Tint;
            _indexCounts[index] = (uint)draw.Indices.Length;
            _overlay[index] = draw.Blend == CelestialColorBlend.Overlay;
            _opaque[index] = draw.Blend == CelestialColorBlend.Opaque;

            // The cloud mesh is cached and only its translation changes per frame; skip the (multi-megabyte) upload
            // unless the caller actually provided new vertex/index arrays.
            var unchanged = index < _sourceVertices.Length
                && ReferenceEquals(_sourceVertices[index], draw.Vertices)
                && ReferenceEquals(_sourceIndices[index], draw.Indices);
            if (!unchanged)
            {
                _backend.WriteBuffer(
                    _vertexBuffers[index],
                    System.Runtime.InteropServices.MemoryMarshal.AsBytes(draw.Vertices.AsSpan()),
                    0);
                _backend.WriteBuffer(
                    _indexBuffers[index],
                    System.Runtime.InteropServices.MemoryMarshal.AsBytes(draw.Indices.AsSpan()),
                    0);
                _sourceVertices[index] = draw.Vertices;
                _sourceIndices[index] = draw.Indices;
            }

            // The model matrix is deliberately not applied: it only carried the camera translation, which the sky matrix
            // now supplies by not being applied at all (T-811). Only the tint remains per draw, so the uniform is written
            // when the tint changes instead of every frame (T-810).
            if (index < _writtenTints.Length && _writtenTints[index] == draw.Tint)
            {
                TintSkips++;
                return;
            }

            _tintScratch[0] = (byte)(draw.Tint.X * 255f);
            BitConverter.TryWriteBytes(_tintScratch.AsSpan(0), draw.Tint.X);
            BitConverter.TryWriteBytes(_tintScratch.AsSpan(4), draw.Tint.Y);
            BitConverter.TryWriteBytes(_tintScratch.AsSpan(8), draw.Tint.Z);
            BitConverter.TryWriteBytes(_tintScratch.AsSpan(12), draw.Tint.W);
            _backend.WriteBuffer(_transformBuffers[index], _tintScratch, 0);
            _writtenTints[index] = draw.Tint;
            TintWrites++;
        }

        /// <summary>Draws every colour body, picking the pipeline that matches its blend mode.</summary>
        public void Draw(IRenderBackend backend, RenderPassHandle pass)
        {
            if (_bindGroups.Length == 0)
            {
                return;
            }

            for (var index = 0; index < _bindGroups.Length; index++)
            {
                backend.SetPipeline(pass, _opaque[index] ? _opaquePipeline : (_overlay[index] ? _overlayPipeline : _translucentPipeline));
                backend.SetBindGroup(pass, 0, _bindGroups[index]);
                backend.SetVertexBuffer(pass, 0, _vertexBuffers[index]);
                backend.SetIndexBuffer(pass, _indexBuffers[index], IndexFormat.Uint32);
                backend.DrawIndexed(pass, _indexCounts[index], 1, 0, 0, 0);
            }
        }

        /// <summary>
        /// Kept for the call sites: the dome is re-centred by the shared sky matrix, so a camera move uploads nothing.
        /// </summary>
        /// <param name="eyeDelta">Unused; the bodies follow the camera through the camera uniform (T-810).</param>
        public void UpdateCamera(Vector3 eyeDelta)
        {
            _ = eyeDelta;
            NoOpCameraUpdates++;
        }

        /// <summary>Camera updates that still touched a buffer; zero now that the sky matrix carries the camera (T-810).</summary>
        public long AppliedCameraUpdates { get; private set; }

        /// <summary>Camera updates skipped by the old threshold; kept so the existing diagnostics still compile.</summary>
        public long SkippedCameraUpdates => 0;

        /// <summary>How often the call site asked the bodies to follow the camera; every one is a no-op now.</summary>
        public long NoOpCameraUpdates { get; private set; }

        /// <summary>Per-draw tint uploads actually issued (diagnostic for T-810).</summary>
        public long TintWrites { get; private set; }

        /// <summary>Frames where the tint was unchanged, so the upload was skipped (diagnostic for T-810).</summary>
        public long TintSkips { get; private set; }

        public void Dispose()
        {
            foreach (var bindGroup in _bindGroups)
            {
                _backend.Destroy(bindGroup);
            }

            foreach (var buffer in _transformBuffers)
            {
                _backend.Destroy(buffer);
            }

            foreach (var buffer in _indexBuffers)
            {
                _backend.Destroy(buffer);
            }

            foreach (var buffer in _vertexBuffers)
            {
                _backend.Destroy(buffer);
            }

            foreach (var (vertex, index, transform, bindGroup) in _retired)
            {
                _backend.Destroy(bindGroup);
                _backend.Destroy(transform);
                _backend.Destroy(index);
                _backend.Destroy(vertex);
            }

            _backend.Destroy(_translucentPipeline);
            _backend.Destroy(_overlayPipeline);
            _backend.Destroy(_opaquePipeline);
            _backend.Destroy(_pipelineLayout);
            _backend.Destroy(_layout);
        }
    }
}

/// <summary>Raised when WGPU cannot provide an adapter or device.</summary>
public sealed class RenderingUnavailableException(string message) : Exception(message);
