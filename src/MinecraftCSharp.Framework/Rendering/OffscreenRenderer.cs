// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 MinecraftCSharp contributors

using Brew.Wgpu;
using Brew.Wgpu.Native;

namespace MinecraftCSharp.Framework.Rendering;

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

    internal const string ShaderSourceText = """
        @group(0) @binding(0) var atlasTexture : texture_2d<f32>;
        @group(0) @binding(1) var atlasSampler : sampler;

        struct VertexOutput {
            @builtin(position) clipPosition : vec4<f32>,
            @location(0) colour : vec3<f32>,
            @location(1) uv : vec2<f32>,
            @location(2) spriteRect : vec4<f32>,
        };

        @vertex
        fn vs_main(
            @location(0) clipPosition : vec4<f32>,
            @location(1) colour : vec3<f32>,
            @location(2) uv : vec2<f32>,
            @location(3) spriteRect : vec4<f32>,
        ) -> VertexOutput {
            var output : VertexOutput;
            output.clipPosition = clipPosition;
            output.colour = colour;
            output.uv = uv;
            output.spriteRect = spriteRect;
            return output;
        }

        @fragment
        fn fs_main(input : VertexOutput) -> @location(0) vec4<f32> {
            // Tile inside the sprite cell: the fractional part repeats the sprite, and using the
            // cell rect keeps sampling away from neighbouring sprites (render.md 6.1).
            let repeated = input.uv - floor(input.uv);
            let atlasUv = input.spriteRect.xy + repeated * (input.spriteRect.zw - input.spriteRect.xy);
            let sampled = textureSample(atlasTexture, atlasSampler, atlasUv);
            return vec4<f32>(sampled.rgb * input.colour, 1.0);
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
            vertexStride: (ulong)ClipSpaceTransform.VertexStride,
            vertexStepMode: WGPUVertexStepMode.Vertex,
            vertexAttributes:
            [
                new VertexAttribute(WGPUVertexFormat.Float32x4, 0, 0),
                new VertexAttribute(WGPUVertexFormat.Float32x3, 16, 1),
                new VertexAttribute(WGPUVertexFormat.Float32x2, 28, 2),
                new VertexAttribute(WGPUVertexFormat.Float32x4, 36, 3)
            ]);
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
                Usage = TextureUsage.RenderAttachment
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
                BindGroupLayoutEntry.Sampler(1, ShaderStage.Fragment, WGPUSamplerBindingType.Filtering)
            ]);

            using var pipelineLayout = device.CreatePipelineLayout([bindGroupLayout]);

            using var bindGroup = device.CreateBindGroup(
                bindGroupLayout,
                [BindGroupEntry.TextureView(0, atlasView), BindGroupEntry.Sampler(1, atlasSampler)]);

            // Readback rows must be padded to 256 bytes.
            var unpadded = (int)width * 4;
            var padded = (unpadded + 255) / 256 * 256;
            var size = (ulong)padded * height;

            var readback = device.CreateBuffer(new BufferDescriptor
            {
                Size = size,
                Usage = BufferUsage.MapRead | BufferUsage.CopyDst
            });

            var hasGeometry = request.Indices.Length > 0 && request.Vertices.Length > 0;
            var vertexBytes = hasGeometry ? ClipSpaceTransform.Build(request, viewProjection) : [];
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
