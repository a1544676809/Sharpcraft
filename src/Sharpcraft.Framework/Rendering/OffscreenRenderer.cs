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
public static partial class OffscreenRenderer
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
            // Block entities draw as their own segment with their own atlas: the reference submits them
            // through the node collector rather than the per section meshes. Both objects are scoped to
            // outlive the recording below, and the request must carry the submission before the resources
            // capture it.
            using var blockEntityAtlas = new EntitySkinTextureGpu();
            using var blockEntityRects = new SpriteRectTable(backend);
            using var blockEntitySubmission = new BlockEntitySubmission(backend);
            // Same rule as the window path: the atlas alone decides whether the segment exists, so a
            // world captured with no block entity still has a submission to draw into later (issue 22).
            if (request.BlockEntityAtlas is { } blockEntityAtlasData)
            {
                var blockEntityGeometry = request.BlockEntityGeometry ?? new DynamicGeometry();
                blockEntityRects.Rebuild(request.BlockEntitySpriteRects);
                blockEntityAtlas.Upload(
                    backend,
                    bindGroupLayout,
                    blockEntityAtlasData,
                    cameraBuffer,
                    fogBuffer,
                    lightmapBuffer,
                    atlasSamplerHandle,
                    blockEntityRects);
                blockEntitySubmission.Upload(blockEntityGeometry, blockEntityAtlas.BindGroup);
                request = request with { BlockEntity = blockEntitySubmission };
            }

            var frameResources = new FrameResources(new FrameResources.Options
            {
                Backend = backend,
                Request = request,
                Format = WGPUTextureFormat.RGBA8Unorm,
                // The offscreen path supplies its own depth in the per-frame target, so this shell needs none.
                CreatesDepthTarget = false,
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
}

/// <summary>Raised when WGPU cannot provide an adapter or device.</summary>
public sealed class RenderingUnavailableException(string message) : Exception(message);
