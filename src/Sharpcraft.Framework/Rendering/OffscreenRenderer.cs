// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Brew.Wgpu;
using Brew.Wgpu.Native;

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
        var shiftText = Environment.GetEnvironmentVariable("MC_FRAME_MOTION_DX");
        var shift = float.TryParse(shiftText, out var shiftValue) ? shiftValue : 1f;
        var shiftBy = new System.Numerics.Vector3(shift, 0f, 0f);
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

                if (window >= 1f)
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
        var nearest = float.MaxValue;
        var farthest = 0f;
        var centre = BitConverter.ToSingle(mapped.Slice(((height / 2) * depthPadded) + ((width / 2) * 4), 4));

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var value = BitConverter.ToSingle(mapped.Slice((y * depthPadded) + (x * 4), 4));
                depths[(y * width) + x] = value;

                if (value < nearest)
                {
                    nearest = value;
                }

                if (value > farthest)
                {
                    farthest = value;
                }
            }
        }

        depthReadback.Unmap();
        var near = 0.1f;
        var far = 2000f;
        Console.WriteLine(
            $"depth: {width}x{height} nearest {nearest:F4} ({DepthLinearisation.ToDistance(nearest, near, far):F1} blocks) " +
            $"farthest {farthest:F4} ({DepthLinearisation.ToDistance(farthest, near, far):F1} blocks) " +
            $"centre {centre:F4} ({DepthLinearisation.ToDistance(centre, near, far):F1} blocks)");

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

    private static RenderPipeline CreateSkyPipeline(Device device, WGPUTextureFormat format)
    {
        using var shader = device.CreateShaderModule(new ShaderModuleDescriptor
        {
            Source = ShaderSource.FromWgsl(System.Text.Encoding.UTF8.GetBytes(SkyShaderSourceText))
        });

        return device.CreateRenderPipeline(
            shader,
            "vs_sky"u8,
            shader,
            "fs_sky"u8,
            [new ColorTargetState(format)],
            primitive: PrimitiveState.Default with { CullMode = WGPUCullMode.None },
            multisample: MultisampleState.Default,
            // Depth test always passes and writes nothing: the sky is a backdrop, not geometry.
            depthStencil: DepthStencilState.Depth(WGPUTextureFormat.Depth32Float, WGPUCompareFunction.Always, write: false),
            vertexStride: 0,
            vertexStepMode: WGPUVertexStepMode.Vertex,
            vertexAttributes: []);
    }

    private static RenderPipeline CreatePipeline(Device device, PipelineLayout layout)
    {
        using var shader = device.CreateShaderModule(new ShaderModuleDescriptor
        {
            Source = ShaderSource.FromWgsl(System.Text.Encoding.UTF8.GetBytes(ShaderSourceText))
        });

        return device.CreateRenderPipeline(
            shader,
            "vs_main"u8,
            shader,
            "fs_main"u8,
            [new ColorTargetState(WGPUTextureFormat.RGBA8Unorm)],
            layout: layout,
            primitive: PrimitiveState.Default with { CullMode = WGPUCullMode.Back, FrontFace = WGPUFrontFace.CCW },
            multisample: MultisampleState.Default,
            depthStencil: DepthStencilState.Depth(WGPUTextureFormat.Depth32Float),
            vertexStride: (ulong)VertexLayout.Stride,
            vertexStepMode: WGPUVertexStepMode.Vertex,
            vertexAttributes: VertexLayout.Attributes());
    }

    /// <summary>
    /// The translucent pipeline: the same shader and layout as terrain, but blending on and depth writes off so layers
    /// behind still show through and the order of the draw calls matters.
    /// </summary>
    private static RenderPipeline CreateTranslucentPipeline(Device device, PipelineLayout layout)
    {
        using var shader = device.CreateShaderModule(new ShaderModuleDescriptor
        {
            Source = ShaderSource.FromWgsl(System.Text.Encoding.UTF8.GetBytes(ShaderSourceText))
        });

        return device.CreateRenderPipeline(
            shader,
            "vs_main"u8,
            shader,
            "fs_main"u8,
            [new ColorTargetState(WGPUTextureFormat.RGBA8Unorm)],
            layout: layout,
            primitive: PrimitiveState.Default with { CullMode = WGPUCullMode.Back, FrontFace = WGPUFrontFace.CCW },
            multisample: MultisampleState.Default,
            depthStencil: DepthStencilState.Depth(WGPUTextureFormat.Depth32Float, WGPUCompareFunction.LessEqual, write: false),
            vertexStride: (ulong)VertexLayout.Stride,
            vertexStepMode: WGPUVertexStepMode.Vertex,
            vertexAttributes: VertexLayout.Attributes());
    }

    private static uint[] BuildIndexBytes(FrameRequest request) => request.Indices;

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
            using var device = adapter.RequestDeviceBlocking();

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
                    VertexLayout.CameraUniformSize),
                BindGroupLayoutEntry.Buffer(
                    3,
                    ShaderStage.Vertex | ShaderStage.Fragment,
                    WGPUBufferBindingType.Uniform,
                    false,
                    32)
            ]);

            using var pipelineLayout = device.CreatePipelineLayout([bindGroupLayout]);

            // Camera uniform: one 4×4 matrix, rewritten per frame.
            using var cameraBuffer = device.CreateBufferInit(
                new BufferInitDescriptor { Usage = BufferUsage.Uniform | BufferUsage.CopyDst },
                new byte[VertexLayout.CameraUniformSize].AsSpan());

            // Fog: colour plus start and end distance in blocks.
            var fogBytes = new byte[32];
            var fogValues = new float[8];
            fogValues[0] = 0.75f;
            fogValues[1] = 0.85f;
            fogValues[2] = 1.0f;
            fogValues[3] = 1.0f;
            fogValues[4] = Environment.GetEnvironmentVariable("MC_FOG_START") is { } startText && float.TryParse(startText, out var start) ? start : 64f;
            fogValues[5] = Environment.GetEnvironmentVariable("MC_FOG_END") is { } endText && float.TryParse(endText, out var end) ? end : 160f;
            System.Runtime.InteropServices.MemoryMarshal.AsBytes(fogValues.AsSpan()).CopyTo(fogBytes);
            using var fogBuffer = device.CreateBufferInit(
                new BufferInitDescriptor { Usage = BufferUsage.Uniform | BufferUsage.CopyDst },
                fogBytes.AsSpan());
            device.Queue.WriteBuffer(fogBuffer, 0, fogBytes.AsSpan());

            Span<float> cameraBlock = stackalloc float[16];
            CameraUniform.Write(cameraBlock, request.CameraPosition, request.CameraTarget, request.FieldOfViewDegrees, (int)width, (int)height);
            var cameraBytes = new byte[VertexLayout.CameraUniformSize];
            for (var index = 0; index < 16; index++)
            {
                BitConverter.TryWriteBytes(cameraBytes.AsSpan(index * sizeof(float)), cameraBlock[index]);
            }

            device.Queue.WriteBuffer(cameraBuffer, 0, cameraBytes.AsSpan());

            using var bindGroup = device.CreateBindGroup(
                bindGroupLayout,
                [
                    BindGroupEntry.TextureView(0, atlasView),
                    BindGroupEntry.Sampler(1, atlasSampler),
                    BindGroupEntry.Buffer(VertexLayout.CameraBinding, cameraBuffer, 0, VertexLayout.CameraUniformSize),
                BindGroupEntry.Buffer(3, fogBuffer, 0, 32)
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

            using var pipeline = hasGeometry ? CreatePipeline(device, pipelineLayout) : default;
            using var vertexBuffer = hasGeometry
                ? device.CreateBufferInit(new BufferInitDescriptor { Usage = BufferUsage.Vertex }, vertexBytes.AsSpan())
                : default;
            using var indexBuffer = hasGeometry
                ? device.CreateBufferInit(new BufferInitDescriptor { Usage = BufferUsage.Index }, indexBytes.AsSpan())
                : default;

            var encoder = device.CreateCommandEncoder();
            var colour = request.ClearColour;
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

            // Sky first: it fills the cleared target, then the world draws over it.
            using (var skyPipeline = CreateSkyPipeline(device, WGPUTextureFormat.RGBA8Unorm))
            {
                pass.SetPipeline(skyPipeline);
                pass.Draw(3);
            }

            if (hasGeometry)
            {
                pass.SetPipeline(pipeline);
                pass.SetBindGroup(0, bindGroup, []);
                pass.SetVertexBuffer(0, vertexBuffer);
                pass.SetIndexBuffer(indexBuffer, WGPUIndexFormat.Uint32);

                if (request.DrawRanges is { Length: > 0 } ranges)
                {
                    // One call per chunk, so an omitted chunk actually disappears rather than being merged into one draw.
                    foreach (var (firstIndex, indexCount) in ranges)
                    {
                        if (indexCount > 0)
                        {
                            pass.DrawIndexed((uint)indexCount, 1, (uint)firstIndex, 0, 0);
                        }
                    }
                }
                else
                {
                    pass.DrawIndexed((uint)request.Indices.Length);
                }
            }

            if (request.TranslucentVertices is { Length: > 0 } translucentVertices &&
                request.TranslucentIndices is { Length: > 0 } translucentIndices)
            {
                // Blended, no depth writes, drawn last: the order of these calls is what makes overlapping water read right.
                using var translucentPipeline = CreateTranslucentPipeline(device, pipelineLayout);
                using var translucentVertexBuffer = device.CreateBufferInit(
                    new BufferInitDescriptor { Usage = BufferUsage.Vertex },
                    translucentVertices.AsSpan());
                using var translucentIndexBuffer = device.CreateBufferInit(
                    new BufferInitDescriptor { Usage = BufferUsage.Index },
                    translucentIndices.AsSpan());

                pass.SetPipeline(translucentPipeline);
                pass.SetBindGroup(0, bindGroup, []);
                pass.SetVertexBuffer(0, translucentVertexBuffer);
                pass.SetIndexBuffer(translucentIndexBuffer, WGPUIndexFormat.Uint32);
                pass.DrawIndexed((uint)translucentIndices.Length);
            }

            pass.End();

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
            var hasPyramid = Environment.GetEnvironmentVariable("MC_FRAME_PYRAMID") == "1";

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
                using var computePipeline = device.CreateComputePipeline(shader, "cs_downsample"u8, computePipelineLayout);

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
                    computePass.SetPipeline(computePipeline);
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
            var hasMotion = Environment.GetEnvironmentVariable("MC_FRAME_MOTION") == "1";

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
                var shiftText = Environment.GetEnvironmentVariable("MC_FRAME_MOTION_DX");
                var shift = float.TryParse(shiftText, out var shiftValue) ? shiftValue : 1f;
                var shiftBy = new System.Numerics.Vector3(shift, 0f, 0f);
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
                using var motionPipeline = device.CreateComputePipeline(motionShader, "cs_motion"u8, motionPipelineLayout);
                using var motionBindGroup = device.CreateBindGroup(
                    motionLayout,
                    [
                        BindGroupEntry.Buffer(4, depthForMotion, 0, sourceBytes),
                        BindGroupEntry.Buffer(5, motionOutput, 0, motionBytes),
                        BindGroupEntry.Buffer(6, motionParameterBuffer, 0, 16),
                        BindGroupEntry.Buffer(7, motionMatrixBuffer, 0, 128)
                    ]);

                using var motionPass = encoder.BeginComputePass();
                motionPass.SetPipeline(motionPipeline);
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

            if (Environment.GetEnvironmentVariable("MC_FRAME_DEPTH") == "1")
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
}

/// <summary>Raised when WGPU cannot provide an adapter or device.</summary>
public sealed class RenderingUnavailableException(string message) : Exception(message);
