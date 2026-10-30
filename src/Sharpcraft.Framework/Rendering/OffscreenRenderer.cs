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
    internal const string SkyShaderSourceText = """
        struct SkyOutput {
            @builtin(position) clipPosition : vec4<f32>,
            @location(0) height : f32,
        };

        @vertex
        fn vs_sky(@builtin(vertex_index) index : u32) -> SkyOutput {
            // One oversized triangle covering the screen, so no vertex buffer is needed.
            var positions = array<vec2<f32>, 3>(
                vec2<f32>(-1.0, -3.0),
                vec2<f32>(-1.0, 1.0),
                vec2<f32>(3.0, 1.0));

            var output : SkyOutput;
            let position = positions[index];
            output.clipPosition = vec4<f32>(position, 0.0, 1.0);
            // 0 at the bottom of the screen, 1 at the top.
            output.height = (position.y + 1.0) * 0.5;
            return output;
        }

        @fragment
        fn fs_sky(input : SkyOutput) -> @location(0) vec4<f32> {
            let horizon = vec3<f32>(0.75, 0.85, 1.0);
            let zenith = vec3<f32>(0.47, 0.65, 1.0);
            return vec4<f32>(mix(horizon, zenith, input.height), 1.0);
        }
        """;

    internal const string ShaderSourceText = """
        @group(0) @binding(0) var atlasTexture : texture_2d<f32>;
        @group(0) @binding(1) var atlasSampler : sampler;

        struct Camera {
            viewProjection : mat4x4<f32>,
        };
        @group(0) @binding(2) var<uniform> camera : Camera;

        struct Fog {
            colour : vec4<f32>,
            range : vec4<f32>,
        };
        @group(0) @binding(3) var<uniform> fog : Fog;

        struct VertexOutput {
            @builtin(position) clipPosition : vec4<f32>,
            @location(0) colour : vec3<f32>,
            @location(1) uv : vec2<f32>,
            @location(2) spriteRect : vec4<f32>,
            @location(3) fogDistance : f32,
        };

        @vertex
        fn vs_main(
            @location(0) position : vec3<f32>,
            @location(1) colour : vec3<f32>,
            @location(2) uv : vec2<f32>,
            @location(3) spriteRect : vec4<f32>,
        ) -> VertexOutput {
            var output : VertexOutput;

            // Vertices stay in world space; the camera moves by updating this uniform, so a moving camera
            // never forces the mesh to be re-transformed and re-uploaded.
            let clip = camera.viewProjection * vec4<f32>(position, 1.0);
            output.clipPosition = clip;
            output.fogDistance = clip.w;
            output.colour = colour;
            output.uv = uv;
            output.spriteRect = spriteRect;
            return output;
        }

        @fragment
        fn fs_main(input : VertexOutput) -> @location(0) vec4<f32> {
            // The sprite rectangle runs from the centre of the first texel to the centre of the last, so
            // interpolating across it directly spans one texel less than the sprite: a sixteen pixel tile
            // covers fifteen, and the half texel lost at each edge shows the neighbouring sprite along block
            // borders. Recover the texel count from the rectangle, then sample texel centres, which keeps the
            // whole tile and never steps outside it (render.md 6.1,
            // docs/specs/network/play-movement.md 7.22).
            let atlasSize = vec2<f32>(textureDimensions(atlasTexture));
            let rectMin = input.spriteRect.xy;
            let rectMax = input.spriteRect.zw;

            // Centre-to-centre, so the number of texels is the span plus one.
            let spriteTexels = max(round((rectMax - rectMin) * atlasSize) + vec2<f32>(1.0), vec2<f32>(1.0));

            let repeated = input.uv - floor(input.uv);
            let texel = clamp(floor(repeated * spriteTexels), vec2<f32>(0.0), spriteTexels - vec2<f32>(1.0));
            let atlasUv = rectMin + texel / atlasSize;

            let sampled = textureSample(atlasTexture, atlasSampler, atlasUv);
            let base = vec4<f32>(sampled.rgb * input.colour, 1.0);
            let span = max(fog.range.y - fog.range.x, 0.001);
            let fogFactor = clamp((input.fogDistance - fog.range.x) / span, 0.0, 1.0);
            return vec4<f32>(mix(base.rgb, fog.colour.rgb, fogFactor), base.a);
        }
        """;

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
    /// <summary>Reads the depth buffer back and prints its range, proving depth is available for the occlusion pass.</summary>
    private static void ReportDepth(Instance instance, Brew.Wgpu.Buffer depthReadback, ulong depthSize, int depthPadded, int width, int height)
    {
        var status = depthReadback.MapBlocking(instance, MapMode.Read, 0, (nuint)depthSize);

        if (status != WGPUMapAsyncStatus.Success)
        {
            Console.WriteLine($"depth: readback failed ({status})");
            return;
        }

        var mapped = depthReadback.GetMappedRange<byte>(0, (nuint)depthSize);
        var nearest = float.MaxValue;
        var farthest = 0f;
        var centre = BitConverter.ToSingle(mapped.Slice(((height / 2) * depthPadded) + ((width / 2) * 4), 4));

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var value = BitConverter.ToSingle(mapped.Slice((y * depthPadded) + (x * 4), 4));

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
        // Window depth compresses the far field, so the distances are what actually get compared.
        var near = 0.1f;
        var far = 2000f;
        Console.WriteLine(
            $"depth: {width}x{height} nearest {nearest:F4} ({DepthLinearisation.ToDistance(nearest, near, far):F1} blocks) " +
            $"farthest {farthest:F4} ({DepthLinearisation.ToDistance(farthest, near, far):F1} blocks) " +
            $"centre {centre:F4} ({DepthLinearisation.ToDistance(centre, near, far):F1} blocks)");
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

    private static uint[] BuildIndexBytes(FrameRequest request) => request.Indices;

    /// <summary>Renders a frame and returns its pixels.</summary>
    /// <exception cref="RenderingUnavailableException">The GPU path failed.</exception>
    public static FrameResult Render(FrameRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

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
                pass.DrawIndexed((uint)request.Indices.Length);
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
                ReportDepth(instance, depthReadback, depthSize, depthPadded, (int)width, (int)height);
            }

            depthReadback.Dispose();
            commands.Dispose();
            encoder.Dispose();
            depthTexture.Dispose();
            texture.Dispose();

            return new FrameResult(request.Width, request.Height, pixels, viewProjection, adapterName);
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
