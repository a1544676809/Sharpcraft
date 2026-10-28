// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 MinecraftCSharp contributors

using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.Core.Native;
using Silk.NET.WebGPU;
using WgpuBuffer = Silk.NET.WebGPU.Buffer;

namespace MinecraftCSharp.Framework.Rendering;

/// <summary>
/// Offscreen WGPU renderer: renders one frame into a texture, reads it back and returns RGBA
/// pixels. All WGPU calls live in this assembly (docs/decisions/0003-wgpu-binding.md).
/// </summary>
/// <remarks>
/// Deliberately headless: no window or swapchain yet, so the render pipeline can be validated in
/// isolation. Vertices are transformed on the CPU and the shader only passes clip coordinates
/// through, which keeps the first pipeline free of uniform buffers and bind groups.
/// </remarks>
public static unsafe class OffscreenRenderer
{
    /// <summary>Floats per transformed vertex: clip position (4) plus colour (3).</summary>
    public const int FloatsPerVertex = 7;

    /// <summary>Stride of one transformed vertex in bytes.</summary>
    public const int VertexStride = FloatsPerVertex * sizeof(float);

    private const string ShaderSource = """
        struct VertexOutput {
            @builtin(position) clipPosition : vec4<f32>,
            @location(0) colour : vec3<f32>,
        };

        @vertex
        fn vs_main(
            @location(0) clipPosition : vec4<f32>,
            @location(1) colour : vec3<f32>,
        ) -> VertexOutput {
            var output : VertexOutput;
            output.clipPosition = clipPosition;
            output.colour = colour;
            return output;
        }

        @fragment
        fn fs_main(input : VertexOutput) -> @location(0) vec4<f32> {
            return vec4<f32>(input.colour, 1.0);
        }
        """;

    /// <summary>Describes the adapter that would be used, for diagnostics and acceptance runs.</summary>
    /// <exception cref="RenderingUnavailableException">No adapter is available.</exception>
    public static string DescribeAdapter()
    {
        var api = WebGPU.GetApi();
        var instance = api.CreateInstance(null);
        if (instance == null)
        {
            throw new RenderingUnavailableException("wgpu could not create an instance");
        }

        try
        {
            var adapter = AcquireAdapter(api, instance);
            try
            {
                return AdapterName(api, adapter);
            }
            finally
            {
                api.AdapterRelease(adapter);
            }
        }
        finally
        {
            api.InstanceRelease(instance);
        }
    }

    /// <summary>Renders a frame and returns its pixels.</summary>
    /// <exception cref="RenderingUnavailableException">No adapter or device could be acquired.</exception>
    public static FrameResult Render(FrameRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var viewProjection = CameraMath.BuildViewProjection(
            request.CameraPosition,
            request.CameraTarget,
            request.FieldOfViewDegrees,
            (float)request.Width / request.Height);

        var api = WebGPU.GetApi();
        var instance = api.CreateInstance(null);
        if (instance == null)
        {
            throw new RenderingUnavailableException("wgpu could not create an instance");
        }

        try
        {
            var adapter = AcquireAdapter(api, instance);
            try
            {
                var adapterName = AdapterName(api, adapter);
                var device = AcquireDevice(api, instance, adapter);
                try
                {
                    var pixels = Draw(api, instance, device, request, viewProjection);
                    return new FrameResult(request.Width, request.Height, pixels, viewProjection, adapterName);
                }
                finally
                {
                    api.DeviceRelease(device);
                }
            }
            finally
            {
                api.AdapterRelease(adapter);
            }
        }
        finally
        {
            api.InstanceRelease(instance);
        }
    }

    private static string AdapterName(WebGPU api, Adapter* adapter)
    {
        var properties = default(AdapterProperties);
        api.AdapterGetProperties(adapter, &properties);

        var name = properties.Name;
        if (name == null)
        {
            return "unknown adapter";
        }

        return Marshal.PtrToStringUTF8((nint)name) ?? "unknown adapter";
    }

    private static Adapter* AcquireAdapter(WebGPU api, Instance* instance)
    {
        Adapter* result = null;
        RequestAdapterStatus status = RequestAdapterStatus.Unknown;
        var done = false;

        var callback = new PfnRequestAdapterCallback((s, adapter, _, _) =>
        {
            status = s;
            result = adapter;
            done = true;
        });

        var options = new RequestAdapterOptions
        {
            PowerPreference = PowerPreference.HighPerformance,
            BackendType = BackendType.Undefined,
            CompatibleSurface = null,
            ForceFallbackAdapter = false
        };

        var handle = GCHandle.Alloc(callback);
        try
        {
            api.InstanceRequestAdapter(instance, &options, callback, null);
            PumpUntil(api, instance, () => done);
        }
        finally
        {
            handle.Free();
        }

        if (result == null || status != RequestAdapterStatus.Success)
        {
            throw new RenderingUnavailableException(
                $"no WGPU adapter available (status: {status}); install current graphics drivers or run on a machine with a GPU");
        }

        return result;
    }

    private static Device* AcquireDevice(WebGPU api, Instance* instance, Adapter* adapter)
    {
        Device* result = null;
        RequestDeviceStatus status = RequestDeviceStatus.Unknown;
        var done = false;

        var callback = new PfnRequestDeviceCallback((s, device, _, _) =>
        {
            status = s;
            result = device;
            done = true;
        });

        var descriptor = new DeviceDescriptor
        {
            Label = null,
            RequiredFeatureCount = 0,
            RequiredFeatures = null,
            RequiredLimits = null
        };

        var handle = GCHandle.Alloc(callback);
        try
        {
            api.AdapterRequestDevice(adapter, &descriptor, callback, null);
            PumpUntil(api, instance, () => done);
        }
        finally
        {
            handle.Free();
        }

        if (result == null || status != RequestDeviceStatus.Success)
        {
            throw new RenderingUnavailableException($"WGPU device creation failed (status: {status})");
        }

        return result;
    }

    // WGPUBool is a 4-byte integer, so declaring the wait flag as C# bool would corrupt the
    // call frame and make wgpu-native dispatch into a stub.
    [DllImport("wgpu_native", EntryPoint = "wgpuDevicePoll", CallingConvention = CallingConvention.Cdecl)]
    private static extern unsafe uint RawDevicePoll(Device* device, uint wait, void* wrappedSubmissionIndex);

    private static void PollDevice(Device* device)
    {
        // Buffer mapping callbacks in wgpu-native are delivered by device polling, not by
        // instance event processing. MC_FRAME_POLL selects the blocking variant.
        var wait = Environment.GetEnvironmentVariable("MC_FRAME_POLL") == "wait";
        try
        {
            RawDevicePoll(device, wait ? 1u : 0u, null);
        }
        catch (EntryPointNotFoundException exception)
        {
            throw new RenderingUnavailableException($"wgpuDevicePoll is unavailable: {exception.Message}");
        }
    }

    /// <summary>Runs event callbacks until <paramref name="finished"/> reports success or the timeout hits.</summary>
    private static void PumpUntil(WebGPU api, Instance* instance, Func<bool> finished, int timeoutMilliseconds = 10_000)
    {
        var deadline = Environment.TickCount64 + timeoutMilliseconds;
        while (!finished())
        {
            api.InstanceProcessEvents(instance);
            if (Environment.TickCount64 > deadline)
            {
                throw new RenderingUnavailableException("timed out waiting for a WGPU callback");
            }

            Thread.Sleep(1);
        }
    }

    private static byte[] Draw(
        WebGPU api,
        Instance* instance,
        Device* device,
        FrameRequest request,
        Matrix4x4 viewProjection)
    {
        var width = (uint)request.Width;
        var height = (uint)request.Height;

        var textureDescriptor = new TextureDescriptor
        {
            Label = null,
            Size = new Extent3D { Width = width, Height = height, DepthOrArrayLayers = 1 },
            MipLevelCount = 1,
            SampleCount = 1,
            Dimension = TextureDimension.Dimension2D,
            Format = TextureFormat.Rgba8Unorm,
            Usage = TextureUsage.RenderAttachment | TextureUsage.CopySrc,
            ViewFormats = null,
            ViewFormatCount = 0
        };

        var texture = api.DeviceCreateTexture(device, &textureDescriptor);
        var view = api.TextureCreateView(texture, null);

        // Readback rows must be padded to a multiple of 256 bytes.
        var unpadded = (int)width * 4;
        var padded = (unpadded + 255) / 256 * 256;
        var size = (ulong)padded * height;

        var readbackDescriptor = new BufferDescriptor
        {
            Label = null,
            Size = size,
            Usage = BufferUsage.MapRead | BufferUsage.CopyDst,
            MappedAtCreation = false
        };

        var readback = api.DeviceCreateBuffer(device, &readbackDescriptor);

        // Zero-length buffers are invalid, so empty geometry skips the pipeline entirely.
        var hasGeometry = request.Indices.Length > 0 && request.Vertices.Length > 0;
        RenderPipeline* pipeline = null;
        WgpuBuffer* vertexBuffer = null;
        WgpuBuffer* indexBuffer = null;

        if (hasGeometry)
        {
            pipeline = CreatePipeline(api, device);
            vertexBuffer = CreateMappedBuffer(api, device, BuildVertexBytes(request, viewProjection), BufferUsage.Vertex);
            indexBuffer = CreateMappedBuffer(api, device, BuildIndexBytes(request), BufferUsage.Index);
        }

        RenderIntoView(api, device, view, request, pipeline, vertexBuffer, indexBuffer, request.Indices.Length);

        var encoder = api.DeviceCreateCommandEncoder(device, null);
        var source = new ImageCopyTexture
        {
            Texture = texture,
            MipLevel = 0,
            Origin = new Origin3D { X = 0, Y = 0, Z = 0 },
            Aspect = TextureAspect.All
        };

        var destination = new ImageCopyBuffer
        {
            Buffer = readback,
            Layout = new TextureDataLayout { Offset = 0, BytesPerRow = (uint)padded, RowsPerImage = height }
        };

        var copySize = new Extent3D { Width = width, Height = height, DepthOrArrayLayers = 1 };
        var skipCopy = Environment.GetEnvironmentVariable("MC_FRAME_NOREAD") is { Length: > 0 };
        if (!skipCopy)
        {
            api.CommandEncoderCopyTextureToBuffer(encoder, &source, &destination, &copySize);
        }

        var commands = api.CommandEncoderFinish(encoder, null);
        var queue = api.DeviceGetQueue(device);
        api.QueueSubmit(queue, 1, &commands);
        api.QueueRelease(queue);

        var skipMap = Environment.GetEnvironmentVariable("MC_FRAME_NOMAP") is { Length: > 0 };
        var pixels = skipCopy || skipMap
            ? new byte[unpadded * (int)height]
            : MapAndRead(api, instance, device, readback, padded, unpadded, (int)height);

        api.CommandBufferRelease(commands);
        api.CommandEncoderRelease(encoder);
        if (hasGeometry)
        {
            api.BufferRelease(indexBuffer);
            api.BufferRelease(vertexBuffer);
            api.RenderPipelineRelease(pipeline);
        }

        api.BufferRelease(readback);
        api.TextureViewRelease(view);
        api.TextureRelease(texture);

        return pixels;
    }

    private static void RenderIntoView(
        WebGPU api,
        Device* device,
        TextureView* view,
        FrameRequest request,
        RenderPipeline* pipeline,
        WgpuBuffer* vertexBuffer,
        WgpuBuffer* indexBuffer,
        int indexCount)
    {
        var colour = request.ClearColour;
        var attachment = new RenderPassColorAttachment
        {
            View = view,
            ResolveTarget = null,
            LoadOp = LoadOp.Clear,
            StoreOp = StoreOp.Store,
            ClearValue = new Color { R = colour.X, G = colour.Y, B = colour.Z, A = colour.W },
            DepthSlice = uint.MaxValue
        };

        var descriptor = new RenderPassDescriptor
        {
            Label = null,
            ColorAttachmentCount = 1,
            ColorAttachments = &attachment,
            DepthStencilAttachment = null,
            OcclusionQuerySet = null
        };

        var encoder = api.DeviceCreateCommandEncoder(device, null);
        var pass = api.CommandEncoderBeginRenderPass(encoder, &descriptor);

        if (indexCount > 0)
        {
            api.RenderPassEncoderSetPipeline(pass, pipeline);
            api.RenderPassEncoderSetVertexBuffer(pass, 0, vertexBuffer, 0, ulong.MaxValue);
            api.RenderPassEncoderSetIndexBuffer(pass, indexBuffer, IndexFormat.Uint32, 0, ulong.MaxValue);
            api.RenderPassEncoderDrawIndexed(pass, (uint)indexCount, 1, 0, 0, 0);
        }

        api.RenderPassEncoderEnd(pass);

        var commands = api.CommandEncoderFinish(encoder, null);
        var queue = api.DeviceGetQueue(device);
        api.QueueSubmit(queue, 1, &commands);
        api.QueueRelease(queue);
        api.CommandBufferRelease(commands);
        api.CommandEncoderRelease(encoder);
    }

    private static RenderPipeline* CreatePipeline(WebGPU api, Device* device)
    {
        var wgslCode = SilkMarshal.StringToPtr(ShaderSource);
        var wgsl = new ShaderModuleWGSLDescriptor
        {
            Chain = new ChainedStruct { Next = null, SType = SType.ShaderModuleWgslDescriptor },
            Code = (byte*)wgslCode
        };

        var shaderDescriptor = new ShaderModuleDescriptor { Label = null, NextInChain = &wgsl.Chain };
        var shader = api.DeviceCreateShaderModule(device, &shaderDescriptor);
        SilkMarshal.Free(wgslCode);

        var attributes = stackalloc VertexAttribute[2];
        attributes[0] = new VertexAttribute { Format = VertexFormat.Float32x4, Offset = 0, ShaderLocation = 0 };
        attributes[1] = new VertexAttribute { Format = VertexFormat.Float32x3, Offset = 16, ShaderLocation = 1 };

        var buffers = stackalloc VertexBufferLayout[1];
        buffers[0] = new VertexBufferLayout
        {
            ArrayStride = (ulong)VertexStride,
            StepMode = VertexStepMode.Vertex,
            AttributeCount = 2,
            Attributes = attributes
        };

        var vertexEntry = (byte*)SilkMarshal.StringToPtr("vs_main");
        var fragmentEntry = (byte*)SilkMarshal.StringToPtr("fs_main");

        var vertexState = new VertexState
        {
            Module = shader,
            EntryPoint = vertexEntry,
            ConstantCount = 0,
            Constants = null,
            BufferCount = 1,
            Buffers = buffers
        };

        var targets = stackalloc ColorTargetState[1];
        targets[0] = new ColorTargetState
        {
            Format = TextureFormat.Rgba8Unorm,
            Blend = null,
            WriteMask = ColorWriteMask.All
        };

        var fragmentState = new FragmentState
        {
            Module = shader,
            EntryPoint = fragmentEntry,
            ConstantCount = 0,
            Constants = null,
            TargetCount = 1,
            Targets = targets
        };

        var descriptor = new RenderPipelineDescriptor
        {
            Label = null,
            Layout = null,
            Vertex = vertexState,
            Primitive = new PrimitiveState
            {
                Topology = PrimitiveTopology.TriangleList,
                StripIndexFormat = IndexFormat.Undefined,
                FrontFace = FrontFace.Ccw,
                CullMode = CullMode.None
            },
            DepthStencil = null,
            Multisample = new MultisampleState { Count = 1, Mask = uint.MaxValue, AlphaToCoverageEnabled = false },
            Fragment = &fragmentState
        };

        var pipeline = api.DeviceCreateRenderPipeline(device, &descriptor);

        SilkMarshal.Free((nint)vertexEntry);
        SilkMarshal.Free((nint)fragmentEntry);
        api.ShaderModuleRelease(shader);

        return pipeline;
    }

    private static byte[] BuildVertexBytes(FrameRequest request, Matrix4x4 viewProjection)
    {
        var count = request.VertexCount;
        var data = new float[count * FloatsPerVertex];

        for (var i = 0; i < count; i++)
        {
            var offset = i * FrameRequest.FloatsPerVertex;
            var position = new Vector3(
                request.Vertices[offset],
                request.Vertices[offset + 1],
                request.Vertices[offset + 2]);
            var clip = Vector4.Transform(position, viewProjection);

            var destination = i * FloatsPerVertex;
            data[destination] = clip.X;
            data[destination + 1] = clip.Y;
            data[destination + 2] = clip.Z;
            data[destination + 3] = clip.W;
            data[destination + 4] = request.Vertices[offset + 3];
            data[destination + 5] = request.Vertices[offset + 4];
            data[destination + 6] = request.Vertices[offset + 5];
        }

        return MemoryMarshal.AsBytes(data.AsSpan()).ToArray();
    }

    private static byte[] BuildIndexBytes(FrameRequest request) =>
        MemoryMarshal.AsBytes(request.Indices.AsSpan()).ToArray();

    private static WgpuBuffer* CreateMappedBuffer(WebGPU api, Device* device, byte[] data, BufferUsage usage)
    {
        var descriptor = new BufferDescriptor
        {
            Label = null,
            Size = (ulong)data.Length,
            Usage = usage | BufferUsage.CopyDst,
            MappedAtCreation = true
        };

        var buffer = api.DeviceCreateBuffer(device, &descriptor);
        var pointer = api.BufferGetMappedRange(buffer, 0, (nuint)data.Length);
        data.AsSpan().CopyTo(new Span<byte>(pointer, data.Length));
        api.BufferUnmap(buffer);
        return buffer;
    }

    private static byte[] MapAndRead(
        WebGPU api,
        Instance* instance,
        Device* device,
        WgpuBuffer* buffer,
        int padded,
        int unpadded,
        int height)
    {
        var mapped = false;
        var callback = new PfnBufferMapCallback((status, _) => mapped = status == BufferMapAsyncStatus.Success);

        var handle = GCHandle.Alloc(callback);
        try
        {
            api.BufferMapAsync(buffer, MapMode.Read, 0, (nuint)((ulong)padded * (ulong)height), callback, null);

            var deadline = Environment.TickCount64 + 10_000;
            while (!mapped)
            {
                PollDevice(device);
                api.InstanceProcessEvents(instance);
                if (Environment.TickCount64 > deadline)
                {
                    throw new RenderingUnavailableException("timed out waiting for the readback buffer");
                }

                Thread.Sleep(1);
            }
        }
        finally
        {
            handle.Free();
        }

        if (Environment.GetEnvironmentVariable("MC_FRAME_NOGET") is { Length: > 0 })
        {
            return new byte[unpadded * height];
        }

        var source = (byte*)api.BufferGetMappedRange(buffer, 0, (nuint)((ulong)padded * (ulong)height));
        var pixels = new byte[unpadded * height];
        for (var y = 0; y < height; y++)
        {
            new ReadOnlySpan<byte>(source + (y * padded), unpadded).CopyTo(pixels.AsSpan(y * unpadded, unpadded));
        }

        api.BufferUnmap(buffer);
        return pixels;
    }
}

/// <summary>Raised when WGPU cannot provide an adapter or device.</summary>
public sealed class RenderingUnavailableException(string message) : Exception(message);
