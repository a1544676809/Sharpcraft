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
            using var adapter = instance.RequestAdapterBlocking();
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
        Instance instance,
        Brew.Wgpu.Buffer depthReadback,
        ulong depthSize,
        int depthPadded,
        Brew.Wgpu.Buffer motionReadback,
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

        if (depthReadback.MapBlocking(instance, MapMode.Read, 0, (nuint)depthSize) != WGPUMapAsyncStatus.Success ||
            motionReadback.MapBlocking(instance, MapMode.Read, 0, (nuint)motionBytes) != WGPUMapAsyncStatus.Success)
        {
            Console.WriteLine("motion: map failed");
            return;
        }

        var depthMapped = depthReadback.GetMappedRange<byte>(0, (nuint)depthSize);
        var motionMapped = motionReadback.GetMappedRange<byte>(0, (nuint)motionBytes);
        var samples = 0;
        var mismatches = 0;
        var worst = 0f;

        for (var y = 16; y < height; y += 31)
        {
            for (var x = 16; x < width; x += 31)
            {
                var window = BitConverter.ToSingle(depthMapped.Slice((y * depthPadded) + (x * 4), 4));

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
                var gpuX = BitConverter.ToSingle(motionMapped.Slice(offset, 4));
                var gpuY = BitConverter.ToSingle(motionMapped.Slice(offset + 4, 4));

                samples++;
                var delta = MathF.Max(MathF.Abs(gpuX - expectedX), MathF.Abs(gpuY - expectedY));

                if (delta > 0.01f)
                {
                    mismatches++;
                    worst = MathF.Max(worst, delta);
                }
            }
        }

        depthReadback.Unmap();
        motionReadback.Unmap();
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
    private static float[]? ReportDepth(Instance instance, Brew.Wgpu.Buffer depthReadback, ulong depthSize, int depthPadded, int width, int height)
    {
        var status = depthReadback.MapBlocking(instance, MapMode.Read, 0, (nuint)depthSize);

        if (status != WGPUMapAsyncStatus.Success)
        {
            Console.WriteLine($"depth: readback failed ({status})");
            return null;
        }

        var mapped = depthReadback.GetMappedRange<byte>(0, (nuint)depthSize);
        var depths = new float[width * height];
        // Reversed-Z: depth 1 is the near plane and 0 the far plane, so the physically nearest pixel carries the LARGEST
        // raw depth. Track both ends of the raw range and convert each to a distance for the report.
        var minRaw = float.MaxValue;
        var maxRaw = 0f;
        var centre = BitConverter.ToSingle(mapped.Slice(((height / 2) * depthPadded) + ((width / 2) * 4), 4));

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var value = BitConverter.ToSingle(mapped.Slice((y * depthPadded) + (x * 4), 4));
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

        depthReadback.Unmap();
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
        Instance instance,
        Brew.Wgpu.Buffer depthReadback,
        ulong depthSize,
        int depthPadded,
        IReadOnlyList<Brew.Wgpu.Buffer> readbacks,
        IReadOnlyList<(int Width, int Height)> sizes,
        int width,
        int height)
    {
        if (depthReadback.MapBlocking(instance, MapMode.Read, 0, (nuint)depthSize) != WGPUMapAsyncStatus.Success)
        {
            Console.WriteLine("pyramid: depth map failed");
            return;
        }

        var depthMapped = depthReadback.GetMappedRange<byte>(0, (nuint)depthSize);
        var depth = new float[width * height];

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                depth[(y * width) + x] = DepthLinearisation.ToDistance(
                    BitConverter.ToSingle(depthMapped.Slice((y * depthPadded) + (x * 4), 4)),
                    0.1f,
                    2000f);
            }
        }

        depthReadback.Unmap();

        var reference = DepthPyramid.Build(depth, width, height);
        var totalMismatches = 0;
        var worst = 0f;
        var lines = new List<string>();

        for (var level = 0; level < readbacks.Count; level++)
        {
            var (levelWidth, levelHeight) = sizes[level];
            var bytes = (ulong)(levelWidth * levelHeight * 4);

            if (readbacks[level].MapBlocking(instance, MapMode.Read, 0, (nuint)bytes) != WGPUMapAsyncStatus.Success)
            {
                Console.WriteLine($"pyramid: level {level + 1} map failed");
                return;
            }

            var mapped = readbacks[level].GetMappedRange<byte>(0, (nuint)bytes);
            var expected = reference[level + 1];
            var mismatches = 0;

            for (var index = 0; index < levelWidth * levelHeight; index++)
            {
                var delta = MathF.Abs(BitConverter.ToSingle(mapped.Slice(index * 4, 4)) - expected[index]);

                if (delta > 0.05f)
                {
                    mismatches++;
                    worst = MathF.Max(worst, delta);
                }
            }

            readbacks[level].Unmap();
            totalMismatches += mismatches;
            lines.Add($"{level + 1}:{levelWidth}x{levelHeight} mismatches {mismatches}");
        }

        Console.WriteLine($"pyramid: {readbacks.Count} levels, total mismatches {totalMismatches}, worst delta {worst:F6}; " + string.Join(", ", lines));
    }

    private static PipelineHandle CreateSkyPipeline(WgpuRenderBackend backend, TextureFormat format)
    {
        var skySource = System.Text.Encoding.UTF8.GetBytes(SkyShaderSourceText);
        return backend.CreateRenderPipeline(new PipelineDesc(
            skySource, "vs_sky", skySource, "fs_sky", null,
            [new ColorTargetDesc(format)],
            new DepthStateDesc(TextureFormat.Depth32Float, CompareFunction.Always, false),
            CullMode.None, FrontFace.Ccw,
            0, VertexStepMode.Vertex,
            []));
    }

    private static PipelineHandle CreatePipeline(WgpuRenderBackend backend, PipelineLayoutHandle layout)
    {
        var source = System.Text.Encoding.UTF8.GetBytes(ShaderSourceText);
        return backend.CreateRenderPipeline(new PipelineDesc(
            source, "vs_main", source, "fs_main", layout,
            [new ColorTargetDesc(TextureFormat.Rgba8Unorm)],
            new DepthStateDesc(TextureFormat.Depth32Float, CompareFunction.GreaterEqual, true),
            CullMode.Back, FrontFace.Ccw,
            (ulong)VertexLayout.Stride, VertexStepMode.Vertex,
            LayoutAttributes.From(VertexLayout.Attributes())));
    }

    /// <summary>
    /// The translucent pipeline: the same shader and layout as terrain, but blending on and depth writes off so layers
    /// behind still show through and the order of the draw calls matters.
    /// </summary>
    private static PipelineHandle CreateTranslucentPipeline(WgpuRenderBackend backend, PipelineLayoutHandle layout, bool alphaOne)
    {
        var source = System.Text.Encoding.UTF8.GetBytes(ShaderSourceText);
        return backend.CreateRenderPipeline(new PipelineDesc(
            source, "vs_main", source, alphaOne ? "fs_main" : "fs_translucent", layout,
            [new ColorTargetDesc(TextureFormat.Rgba8Unorm, new BlendStateDesc(
                new BlendComponentDesc(BlendOperation.Add, BlendFactor.SrcAlpha, BlendFactor.OneMinusSrcAlpha),
                new BlendComponentDesc(BlendOperation.Add, BlendFactor.One, BlendFactor.OneMinusSrcAlpha)))],
            new DepthStateDesc(TextureFormat.Depth32Float, CompareFunction.GreaterEqual, false),
            CullMode.Back, FrontFace.Ccw,
            (ulong)VertexLayout.Stride, VertexStepMode.Vertex,
            LayoutAttributes.From(VertexLayout.Attributes())));
    }

    /// <summary>
    /// The cutout pipeline: same shader family with alpha testing instead of blending, depth writes on.
    /// </summary>
    private static PipelineHandle CreateCutoutPipeline(WgpuRenderBackend backend, PipelineLayoutHandle layout)
    {
        var source = System.Text.Encoding.UTF8.GetBytes(ShaderSourceText);
        return backend.CreateRenderPipeline(new PipelineDesc(
            source, "vs_main", source, "fs_cutout", layout,
            [new ColorTargetDesc(TextureFormat.Rgba8Unorm)],
            new DepthStateDesc(TextureFormat.Depth32Float, CompareFunction.GreaterEqual, true),
            CullMode.Back, FrontFace.Ccw,
            (ulong)VertexLayout.Stride, VertexStepMode.Vertex,
            LayoutAttributes.From(VertexLayout.Attributes())));
    }

    private static uint[] BuildIndexBytes(FrameRequest request) => request.Indices;

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
            using var adapter = instance.RequestAdapterBlocking();
            var adapterName = adapter.GetInfo().Description;
            // Ask for the limits this adapter actually offers rather than the defaults, whose maxBufferSize is 256 MiB. A full
        // amplified view needs more than that for one geometry buffer, and the adapter's own figure is the honest ceiling.
        var deviceLimits = adapter.GetLimits();
        var deviceDescriptor = new DeviceDescriptor { RequiredLimits = deviceLimits };
        using var device = adapter.RequestDeviceBlocking(in deviceDescriptor);

            var width = (uint)request.Width;
            var height = (uint)request.Height;

            var texture = device.CreateTexture(new TextureDescriptor
            {
                Size = new WGPUExtent3D { width = width, height = height, depthOrArrayLayers = 1 },
                MipLevelCount = 1,
                SampleCount = 1,
                Dimension = WGPUTextureDimension._2D,
                Format = WGPUTextureFormat.RGBA8Unorm,
                Usage = TextureUsage.RenderAttachment | TextureUsage.CopySrc
            });

            using var view = texture.CreateView();

            var depthTexture = device.CreateTexture(new TextureDescriptor
            {
                Size = new WGPUExtent3D { width = width, height = height, depthOrArrayLayers = 1 },
                MipLevelCount = 1,
                SampleCount = 1,
                Dimension = WGPUTextureDimension._2D,
                Format = WGPUTextureFormat.Depth32Float,
                // CopySrc so the depth can be read back: it is the input the occlusion pyramid is built from.
                Usage = TextureUsage.RenderAttachment | TextureUsage.CopySrc
            });

            using var depthView = depthTexture.CreateView();

            // Atlas upload: nearest sampling with no mipmaps keeps pixel art crisp and stops the
            // half-texel inset from blending across sprite borders.
            var atlasData = request.Atlas ?? TextureData.White;

            var atlasMips = atlasData.Mips ?? [];
            using var atlasTexture = device.CreateTexture(new TextureDescriptor
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

            // Mip levels one and up, each half the previous size.
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

            using var atlasView = atlasTexture.CreateView();

            // Same sampler construction as the windowed path, with the request's filter choice preserved so a nearest
            // screenshot request still produces nearest pixels (single track).
            var atlasFilter = request.NearestTextureFilter ? FilterMode.Nearest : FilterMode.Linear;
            using var atlasSampler = SamplerCache.Create(device, AddressMode.ClampToEdge, atlasFilter, atlasFilter, useMipmaps: true);

            // The atlas texture and sampler stay native (the upload path and the sampler cache own them), so their views
            // are adopted once for the bind group the scene draws with.
            var backend = new WgpuRenderBackend(device);
            var atlasViewHandle = backend.AdoptTextureView(atlasView);
            var atlasSamplerHandle = backend.AdoptSampler(atlasSampler);

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

            Span<float> cameraBlock = stackalloc float[16];
            CameraUniform.Write(cameraBlock, request.CameraPosition, request.CameraTarget, request.FieldOfViewDegrees, (int)width, (int)height);
            var cameraBytes = new byte[VertexLayout.CameraUniformSize];
            for (var index = 0; index < 16; index++)
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
                    BindGroupEntryDesc.Uniform(Shaders.ShaderBindings.Lightmap, lightmapBuffer, 0, Shaders.ShaderBindings.LightmapBytes)
                ]);

            // Readback rows must be padded to 256 bytes.
            var unpadded = (int)width * 4;
            var padded = (unpadded + 255) / 256 * 256;
            var size = (ulong)padded * height;

            var readback = device.CreateBuffer(new BufferDescriptor
            {
                Size = size,
                Usage = BufferUsage.MapRead | BufferUsage.CopyDst
            });

            // Depth rows are 4 bytes per pixel and padded the same way as colour.
            var depthPadded = (((int)width * 4) + 255) / 256 * 256;
            var depthSize = (ulong)depthPadded * height;
            var depthReadback = device.CreateBuffer(new BufferDescriptor
            {
                Size = depthSize,
                Usage = BufferUsage.MapRead | BufferUsage.CopyDst
            });

            var hasGeometry = request.Indices.Length > 0 && request.Vertices.Length > 0;
            // World-space vertices: the shader applies the camera, so the buffer only changes when the
            // geometry changes.
            var vertexBytes = hasGeometry ? request.Vertices : [];
            var indexBytes = hasGeometry ? BuildIndexBytes(request) : [];

            var pipeline = hasGeometry ? CreatePipeline(backend, pipelineLayout) : default;
            var vertexBuffer = hasGeometry
                ? backend.CreateBufferInit(
                    new BufferDesc((ulong)vertexBytes.Length * sizeof(float), BufferUsages.Vertex),
                    System.Runtime.InteropServices.MemoryMarshal.AsBytes(vertexBytes.AsSpan()))
                : default;
            var indexBuffer = hasGeometry
                ? backend.CreateBufferInit(
                    new BufferDesc((ulong)indexBytes.Length * sizeof(uint), BufferUsages.Index),
                    System.Runtime.InteropServices.MemoryMarshal.AsBytes(indexBytes.AsSpan()))
                : default;

            var encoder = device.CreateCommandEncoder();
            var colour = request.ClearColour;
            var pass = backend.BeginRenderPass(
                backend.AdoptCommandEncoder(encoder),
                [ColorAttachmentDesc.Clear(backend.AdoptTextureView(view), colour)],
                DepthAttachmentDesc.Clear(backend.AdoptTextureView(depthView), 0f));

            // The scene pass order comes from WorldRenderPipeline; each kind is a no-op when this request has no such
            // geometry. The GPU state and buffer lifetimes stay local to this shell.
            void DrawScenePass(WorldRenderPassKind kind)
            {
                switch (kind)
                {
                    case WorldRenderPassKind.Sky:
                    {
                        // Sky first: it fills the cleared target, then the world draws over it. Skipped when the caller
                        // supplies a sky disc backdrop instead (SkipGradientSky).
                        if (!request.SkipGradientSky)
                        {
                            var skyPipeline = CreateSkyPipeline(backend, TextureFormat.Rgba8Unorm);
                            backend.SetPipeline(pass, skyPipeline);
                            backend.Draw(pass, 3, 1, 0, 0);
                            backend.Destroy(skyPipeline);
                        }

                        // Sun and moon sit in the sky backdrop, before terrain, so world geometry covers them.
                        celestial?.Draw(backend, pass);

                        // Sunrise band and stars, drawn on top of the bodies but still behind world geometry.
                        celestialColour?.Draw(backend, pass);
                        break;
                    }

                    case WorldRenderPassKind.TerrainOpaque:
                    {
                        if (!hasGeometry)
                        {
                            break;
                        }

                        backend.SetPipeline(pass, pipeline);
                        backend.SetBindGroup(pass, 0, bindGroup);
                        backend.SetVertexBuffer(pass, 0, vertexBuffer);
                        backend.SetIndexBuffer(pass, indexBuffer, IndexFormat.Uint32);

                        if (request.DrawRanges is { Length: > 0 } ranges)
                        {
                            // One call per chunk, so an omitted chunk actually disappears rather than being merged into one draw.
                            foreach (var (firstIndex, indexCount) in ranges)
                            {
                                if (indexCount > 0)
                                {
                                    backend.DrawIndexed(pass, (uint)indexCount, 1, (uint)firstIndex, 0, 0);
                                }
                            }
                        }
                        else
                        {
                            backend.DrawIndexed(pass, (uint)request.Indices.Length, 1, 0, 0, 1);
                        }
                        break;
                    }

                    case WorldRenderPassKind.TerrainCutout:
                    {
                        if (request.CutoutVertices is not { Length: > 0 } cutoutVertices ||
                            request.CutoutIndices is not { Length: > 0 } cutoutIndices)
                        {
                            break;
                        }

                        var cutoutPipeline = CreateCutoutPipeline(backend, pipelineLayout);
                        var cutoutVertexBuffer = backend.CreateBufferInit(
                            new BufferDesc((ulong)cutoutVertices.Length * sizeof(float), BufferUsages.Vertex),
                            System.Runtime.InteropServices.MemoryMarshal.AsBytes(cutoutVertices.AsSpan()));
                        var cutoutIndexBuffer = backend.CreateBufferInit(
                            new BufferDesc((ulong)cutoutIndices.Length * sizeof(uint), BufferUsages.Index),
                            System.Runtime.InteropServices.MemoryMarshal.AsBytes(cutoutIndices.AsSpan()));

                        backend.SetPipeline(pass, cutoutPipeline);
                        backend.SetBindGroup(pass, 0, bindGroup);
                        backend.SetVertexBuffer(pass, 0, cutoutVertexBuffer);
                        backend.SetIndexBuffer(pass, cutoutIndexBuffer, IndexFormat.Uint32);
                        backend.DrawIndexed(pass, (uint)cutoutIndices.Length, 1, 0, 0, 1);
                        backend.Destroy(cutoutIndexBuffer);
                        backend.Destroy(cutoutVertexBuffer);
                        backend.Destroy(cutoutPipeline);
                        break;
                    }

                    case WorldRenderPassKind.Translucent:
                    {
                        if (request.TranslucentVertices is not { Length: > 0 } translucentVertices ||
                            request.TranslucentIndices is not { Length: > 0 } translucentIndices)
                        {
                            break;
                        }

                        // Blended, no depth writes, drawn last: the order of these calls is what makes overlapping water read right.
                        var translucentPipeline = CreateTranslucentPipeline(backend, pipelineLayout, request.Diagnostics?.AlphaOne == true);
                        var translucentVertexBuffer = backend.CreateBufferInit(
                            new BufferDesc((ulong)translucentVertices.Length * sizeof(float), BufferUsages.Vertex),
                            System.Runtime.InteropServices.MemoryMarshal.AsBytes(translucentVertices.AsSpan()));
                        var translucentIndexBuffer = backend.CreateBufferInit(
                            new BufferDesc((ulong)translucentIndices.Length * sizeof(uint), BufferUsages.Index),
                            System.Runtime.InteropServices.MemoryMarshal.AsBytes(translucentIndices.AsSpan()));

                        backend.SetPipeline(pass, translucentPipeline);
                        backend.SetBindGroup(pass, 0, bindGroup);
                        backend.SetVertexBuffer(pass, 0, translucentVertexBuffer);
                        backend.SetIndexBuffer(pass, translucentIndexBuffer, IndexFormat.Uint32);

                        if (request.TranslucentRanges is { Length: > 0 } translucentRanges)
                        {
                            // Drawn in the order the caller sorted them, which is far to near.
                            foreach (var (firstIndex, indexCount) in translucentRanges)
                            {
                                if (indexCount > 0)
                                {
                                    backend.DrawIndexed(pass, (uint)indexCount, 1, (uint)firstIndex, 0, 0);
                                }
                            }
                        }
                        else
                        {
                            backend.DrawIndexed(pass, (uint)translucentIndices.Length, 1, 0, 0, 1);
                        }

                        backend.Destroy(translucentIndexBuffer);
                        backend.Destroy(translucentVertexBuffer);
                        backend.Destroy(translucentPipeline);
                        break;
                    }
                }
            }

            WorldRenderPipeline.RecordScenePasses(DrawScenePass);
            backend.EndRenderPass(pass);

            encoder.CopyTextureToBuffer(
                texture,
                readback,
                (uint)padded,
                height,
                new WGPUExtent3D { width = width, height = height, depthOrArrayLayers = 1 });

            encoder.CopyTextureToBuffer(
                depthTexture,
                depthReadback,
                (uint)depthPadded,
                height,
                new WGPUExtent3D { width = width, height = height, depthOrArrayLayers = 1 });

            float[]? depthPixels = null;
            var pyramidLevels = new List<Brew.Wgpu.Buffer>();
            var pyramidReadbacks = new List<Brew.Wgpu.Buffer>();
            var pyramidParameters = new List<Brew.Wgpu.Buffer>();
            var pyramidSizes = new List<(int Width, int Height)>();
            var hasPyramid = request.Diagnostics?.BuildPyramid == true;

            if (hasPyramid)
            {
                // Source level zero: the depth pixels, copied a second time because a map-read buffer cannot be a copy source.
                var sourceBytes = (ulong)depthPadded * height;
                var source = device.CreateBuffer(new BufferDescriptor
                {
                    Size = sourceBytes,
                    Usage = BufferUsage.Storage | BufferUsage.CopyDst
                });

                encoder.CopyTextureToBuffer(
                    depthTexture,
                    source,
                    (uint)depthPadded,
                    height,
                    new WGPUExtent3D { width = width, height = height, depthOrArrayLayers = 1 });

                using var computeLayout = device.CreateBindGroupLayout(
                [
                    BindGroupLayoutEntry.Buffer(4, ShaderStage.Compute, WGPUBufferBindingType.ReadOnlyStorage, false, 0),
                    BindGroupLayoutEntry.Buffer(5, ShaderStage.Compute, WGPUBufferBindingType.Storage, false, 0),
                    BindGroupLayoutEntry.Buffer(6, ShaderStage.Compute, WGPUBufferBindingType.Uniform, false, 32)
                ]);

                using var computePipelineLayout = device.CreatePipelineLayout([computeLayout]);
                using var shader = device.CreateShaderModule(new ShaderModuleDescriptor
                {
                    Source = ShaderSource.FromWgsl(System.Text.Encoding.UTF8.GetBytes(Shaders.ShaderLibrary.Load("depth_pyramid")))
                });
                var computePipelineHandle = backend.CreateComputePipeline(new ComputePipelineDesc(
                    System.Text.Encoding.UTF8.GetBytes(Shaders.ShaderLibrary.Load("depth_pyramid")),
                    "cs_downsample",
                    backend.AdoptPipelineLayout(computePipelineLayout)));

                var levelWidth = (int)width;
                var levelHeight = (int)height;
                var levelStride = depthPadded / 4;
                var levelSource = source;

                while (levelWidth > 1 || levelHeight > 1)
                {
                    var nextWidth = Math.Max(1, levelWidth / 2);
                    var nextHeight = Math.Max(1, levelHeight / 2);
                    var nextBytes = (ulong)(nextWidth * nextHeight * 4);

                    var destination = device.CreateBuffer(new BufferDescriptor
                    {
                        Size = nextBytes,
                        Usage = BufferUsage.Storage | BufferUsage.CopySrc
                    });

                    var levelReadback = device.CreateBuffer(new BufferDescriptor
                    {
                        Size = nextBytes,
                        Usage = BufferUsage.MapRead | BufferUsage.CopyDst
                    });

                    var parameters = new uint[]
                    {
                        (uint)levelWidth, (uint)levelHeight, (uint)levelStride, levelSource == source ? 1u : 0u,
                        0x3DCCCCCDu, 0x44FA0000u, 0, 0
                    };
                    var parameterBytes = new byte[32];
                    System.Runtime.InteropServices.MemoryMarshal.AsBytes(parameters.AsSpan()).CopyTo(parameterBytes);
                    var parameterBuffer = device.CreateBufferInit(
                        new BufferInitDescriptor { Usage = BufferUsage.Uniform | BufferUsage.CopyDst },
                        parameterBytes.AsSpan());

                    var levelBindGroup = device.CreateBindGroup(
                        computeLayout,
                        [
                            BindGroupEntry.Buffer(4, levelSource, 0, (ulong)levelStride * 4 * (ulong)levelHeight),
                            BindGroupEntry.Buffer(5, destination, 0, nextBytes),
                            BindGroupEntry.Buffer(6, parameterBuffer, 0, 32)
                        ]);

                    using var computePass = encoder.BeginComputePass();
                    computePass.SetPipeline(backend.AsComputePipeline(computePipelineHandle));
                    computePass.SetBindGroup(0, levelBindGroup, []);
                    computePass.DispatchWorkgroups(
                        (uint)Math.Max(1, (nextWidth + 7) / 8),
                        (uint)Math.Max(1, (nextHeight + 7) / 8));
                    computePass.End();

                    encoder.CopyBufferToBuffer(destination, 0, levelReadback, 0, nextBytes);

                    pyramidLevels.Add(destination);
                    pyramidReadbacks.Add(levelReadback);
                    pyramidParameters.Add(parameterBuffer);
                    pyramidSizes.Add((nextWidth, nextHeight));

                    levelSource = destination;
                    levelWidth = nextWidth;
                    levelHeight = nextHeight;
                    levelStride = nextWidth;
                }
            }

            Brew.Wgpu.Buffer motionOutput = default;
            Brew.Wgpu.Buffer motionReadback = default;
            var hasMotion = request.Diagnostics?.MotionVectors == true;

            if (hasMotion)
            {
                var sourceBytes = (ulong)depthPadded * height;
                var depthForMotion = device.CreateBuffer(new BufferDescriptor
                {
                    Size = sourceBytes,
                    Usage = BufferUsage.Storage | BufferUsage.CopyDst
                });

                encoder.CopyTextureToBuffer(
                    depthTexture,
                    depthForMotion,
                    (uint)depthPadded,
                    height,
                    new WGPUExtent3D { width = width, height = height, depthOrArrayLayers = 1 });

                var motionBytes = (ulong)((int)width * (int)height * 8);
                motionOutput = device.CreateBuffer(new BufferDescriptor
                {
                    Size = motionBytes,
                    Usage = BufferUsage.Storage | BufferUsage.CopySrc
                });
                motionReadback = device.CreateBuffer(new BufferDescriptor
                {
                    Size = motionBytes,
                    Usage = BufferUsage.MapRead | BufferUsage.CopyDst
                });

                var motionParameters = new uint[] { width, height, (uint)(depthPadded / 4), 0 };
                var motionParameterBytes = new byte[16];
                System.Runtime.InteropServices.MemoryMarshal.AsBytes(motionParameters.AsSpan()).CopyTo(motionParameterBytes);
                using var motionParameterBuffer = device.CreateBufferInit(
                    new BufferInitDescriptor { Usage = BufferUsage.Uniform | BufferUsage.CopyDst },
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
                using var motionMatrixBuffer = device.CreateBufferInit(
                    new BufferInitDescriptor { Usage = BufferUsage.Uniform | BufferUsage.CopyDst },
                    matrixBytes.AsSpan());

                using var motionLayout = device.CreateBindGroupLayout(
                [
                    BindGroupLayoutEntry.Buffer(4, ShaderStage.Compute, WGPUBufferBindingType.ReadOnlyStorage, false, 0),
                    BindGroupLayoutEntry.Buffer(5, ShaderStage.Compute, WGPUBufferBindingType.Storage, false, 0),
                    BindGroupLayoutEntry.Buffer(6, ShaderStage.Compute, WGPUBufferBindingType.Uniform, false, 16),
                    BindGroupLayoutEntry.Buffer(7, ShaderStage.Compute, WGPUBufferBindingType.Uniform, false, 128)
                ]);

                using var motionPipelineLayout = device.CreatePipelineLayout([motionLayout]);
                using var motionShader = device.CreateShaderModule(new ShaderModuleDescriptor
                {
                    Source = ShaderSource.FromWgsl(System.Text.Encoding.UTF8.GetBytes(Shaders.ShaderLibrary.Load("motion_vectors")))
                });
                var motionPipelineHandle = backend.CreateComputePipeline(new ComputePipelineDesc(
                    System.Text.Encoding.UTF8.GetBytes(Shaders.ShaderLibrary.Load("motion_vectors")),
                    "cs_motion",
                    backend.AdoptPipelineLayout(motionPipelineLayout)));
                using var motionBindGroup = device.CreateBindGroup(
                    motionLayout,
                    [
                        BindGroupEntry.Buffer(4, depthForMotion, 0, sourceBytes),
                        BindGroupEntry.Buffer(5, motionOutput, 0, motionBytes),
                        BindGroupEntry.Buffer(6, motionParameterBuffer, 0, 16),
                        BindGroupEntry.Buffer(7, motionMatrixBuffer, 0, 128)
                    ]);

                using var motionPass = encoder.BeginComputePass();
                motionPass.SetPipeline(backend.AsComputePipeline(motionPipelineHandle));
                motionPass.SetBindGroup(0, motionBindGroup, []);
                motionPass.DispatchWorkgroups((uint)Math.Max(1, ((int)width + 7) / 8), (uint)Math.Max(1, ((int)height + 7) / 8));
                motionPass.End();

                encoder.CopyBufferToBuffer(motionOutput, 0, motionReadback, 0, motionBytes);
                depthForMotion.Dispose();
            }

            var commands = encoder.Finish();
            device.Queue.Submit(commands);

            var status = readback.MapBlocking(instance, MapMode.Read, 0, (nuint)size);
            if (status != WGPUMapAsyncStatus.Success)
            {
                throw new RenderingUnavailableException($"readback mapping failed: {status}");
            }

            var mapped = readback.GetMappedRange<byte>(0, (nuint)size);
            var pixels = new byte[unpadded * (int)height];
            for (var y = 0; y < (int)height; y++)
            {
                mapped.Slice(y * padded, unpadded).CopyTo(pixels.AsSpan(y * unpadded, unpadded));
            }

            readback.Unmap();
            readback.Dispose();

            if (request.Diagnostics?.ReadDepth == true)
            {
                depthPixels = ReportDepth(instance, depthReadback, depthSize, depthPadded, (int)width, (int)height);
            }

            if (hasPyramid)
            {
                ComparePyramidChain(instance, depthReadback, depthSize, depthPadded, pyramidReadbacks, pyramidSizes, (int)width, (int)height);
            }

            if (hasMotion)
            {
                CompareMotion(instance, depthReadback, depthSize, depthPadded, motionReadback, viewProjection, request, (int)width, (int)height);
            }

            depthReadback.Dispose();
            commands.Dispose();
            encoder.Dispose();
            depthTexture.Dispose();
            texture.Dispose();

            // The scene resources are seam handles now, so they are released by hand once the frame is read back.
            if (indexBuffer.IsValid)
            {
                backend.Destroy(indexBuffer);
            }

            if (vertexBuffer.IsValid)
            {
                backend.Destroy(vertexBuffer);
            }

            if (pipeline.IsValid)
            {
                backend.Destroy(pipeline);
            }

            backend.Destroy(bindGroup);
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
        private BufferHandle[] _transformBuffers = [];
        private BindGroupHandle[] _bindGroups = [];
        private uint[] _indexCounts = [];
        private Matrix4x4[] _baseModels = [];
        private Vector4[] _tints = [];
        private readonly BufferHandle _cameraBuffer;
        private readonly ulong _cameraSize;
        private readonly List<(BufferHandle Vertex, BufferHandle Index, BufferHandle Transform, BindGroupHandle BindGroup)> _retired = new();

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
                BindGroupLayoutEntryDesc.UniformBuffer(1, ShaderStages.Vertex | ShaderStages.Fragment) with { MinBindingSize = TransformUniformSize },
                BindGroupLayoutEntryDesc.Texture(2, ShaderStages.Fragment),
                BindGroupLayoutEntryDesc.Sampler(3, ShaderStages.Fragment)
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
            _baseModels = new Matrix4x4[draws.Count];
            _tints = new Vector4[draws.Count];

            for (var index = 0; index < draws.Count; index++)
            {
                var draw = draws[index];
                _baseModels[index] = draw.Model;
                _tints[index] = draw.Tint;
                _vertexBuffers[index] = _backend.CreateBufferInit(
                    new BufferDesc((ulong)draw.Vertices.Length * sizeof(float), BufferUsages.Vertex),
                    System.Runtime.InteropServices.MemoryMarshal.AsBytes(draw.Vertices.AsSpan()));
                _indexBuffers[index] = _backend.CreateBufferInit(
                    new BufferDesc((ulong)draw.Indices.Length * sizeof(ushort), BufferUsages.Index),
                    System.Runtime.InteropServices.MemoryMarshal.AsBytes(draw.Indices.AsSpan()));

                var transformBytes = new byte[TransformUniformSize];
                var transformFloats = new float[20];
                WriteMatrix(draw.Model, transformFloats, 0);
                transformFloats[16] = draw.Tint.X;
                transformFloats[17] = draw.Tint.Y;
                transformFloats[18] = draw.Tint.Z;
                transformFloats[19] = draw.Tint.W;
                System.Runtime.InteropServices.MemoryMarshal.AsBytes(transformFloats.AsSpan()).CopyTo(transformBytes);
                _transformBuffers[index] = _backend.CreateBufferInit(
                    new BufferDesc(TransformUniformSize, BufferUsages.Uniform | BufferUsages.CopyDst),
                    transformBytes.AsSpan());

                _bindGroups[index] = _backend.CreateBindGroup(
                    _layout,
                    [
                        BindGroupEntryDesc.Uniform(0, _cameraBuffer, 0, _cameraSize),
                        BindGroupEntryDesc.Uniform(1, _transformBuffers[index], 0, TransformUniformSize),
                        BindGroupEntryDesc.Texture(2, _views[draw.TextureIndex]),
                        BindGroupEntryDesc.Sampler(3, _sampler)
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

        /// <summary>Re-centres the bodies on the camera by translating each base model by <paramref name="eyeDelta"/>.</summary>
        public void UpdateCamera(Vector3 eyeDelta)
        {
            if (eyeDelta == Vector3.Zero)
            {
                return;
            }

            var translation = Matrix4x4.CreateTranslation(eyeDelta);
            for (var index = 0; index < _transformBuffers.Length; index++)
            {
                var transformBytes = new byte[TransformUniformSize];
                var transformFloats = new float[20];
                WriteMatrix(translation * _baseModels[index], transformFloats, 0);
                transformFloats[16] = _tints[index].X;
                transformFloats[17] = _tints[index].Y;
                transformFloats[18] = _tints[index].Z;
                transformFloats[19] = _tints[index].W;
                System.Runtime.InteropServices.MemoryMarshal.AsBytes(transformFloats.AsSpan()).CopyTo(transformBytes);
                _backend.WriteBuffer(_transformBuffers[index], transformBytes, 0);
            }
        }

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
        private const ulong TransformUniformSize = 80;

        private readonly IRenderBackend _backend;
        private readonly BindGroupLayoutHandle _layout;
        private readonly PipelineLayoutHandle _pipelineLayout;
        private readonly PipelineHandle _translucentPipeline;
        private readonly PipelineHandle _overlayPipeline;
        private readonly PipelineHandle _opaquePipeline;
        private BufferHandle[] _vertexBuffers = [];
        private BufferHandle[] _indexBuffers = [];
        private BufferHandle[] _transformBuffers = [];
        private BindGroupHandle[] _bindGroups = [];
        private uint[] _indexCounts = [];
        private bool[] _overlay = [];
        private bool[] _opaque = [];
        private Matrix4x4[] _baseModels = [];
        private Vector4[] _tints = [];
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
            _baseModels = new Matrix4x4[draws.Count];
            _tints = new Vector4[draws.Count];
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
            _baseModels[index] = draw.Model;
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

            var transformBytes = new byte[TransformUniformSize];
            var transformFloats = new float[20];
            WriteMatrix(draw.Model, transformFloats, 0);
            transformFloats[16] = draw.Tint.X;
            transformFloats[17] = draw.Tint.Y;
            transformFloats[18] = draw.Tint.Z;
            transformFloats[19] = draw.Tint.W;
            System.Runtime.InteropServices.MemoryMarshal.AsBytes(transformFloats.AsSpan()).CopyTo(transformBytes);
            _backend.WriteBuffer(_transformBuffers[index], transformBytes, 0);
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

        /// <summary>Re-centres the colour bodies on the camera by translating each base model by <paramref name="eyeDelta"/>.</summary>
        public void UpdateCamera(Vector3 eyeDelta)
        {
            if (eyeDelta == Vector3.Zero)
            {
                return;
            }

            var translation = Matrix4x4.CreateTranslation(eyeDelta);
            for (var index = 0; index < _transformBuffers.Length; index++)
            {
                var transformBytes = new byte[TransformUniformSize];
                var transformFloats = new float[20];
                WriteMatrix(translation * _baseModels[index], transformFloats, 0);
                transformFloats[16] = _tints[index].X;
                transformFloats[17] = _tints[index].Y;
                transformFloats[18] = _tints[index].Z;
                transformFloats[19] = _tints[index].W;
                System.Runtime.InteropServices.MemoryMarshal.AsBytes(transformFloats.AsSpan()).CopyTo(transformBytes);
                _backend.WriteBuffer(_transformBuffers[index], transformBytes, 0);
            }
        }

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
