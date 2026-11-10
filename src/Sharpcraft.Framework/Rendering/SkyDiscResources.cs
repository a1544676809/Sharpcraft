// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using System.Runtime.InteropServices;
using Brew.Wgpu;
using Brew.Wgpu.Native;

namespace Sharpcraft.Framework.Rendering;

/// <summary>
/// Exact per-fragment sky disc (reference core/sky shader): a radius-512 fan with the fog mix computed per pixel from
/// spherical/cylindrical distance. Kept alongside the cheaper precomputed-gradient tray for low-end devices.
/// </summary>
internal sealed class SkyDiscResources : IDisposable
{
    private readonly Device _device;
    private readonly BindGroupLayout _layout;
    private readonly PipelineLayout _pipelineLayout;
    private readonly RenderPipeline _pipeline;
    private readonly BindGroup _bindGroup;
    private readonly Brew.Wgpu.Buffer _skyUniform;
    private readonly Brew.Wgpu.Buffer _vertex;
    private readonly Brew.Wgpu.Buffer _index;
    private readonly uint _indexCount;

    public SkyDiscResources(Device device, Brew.Wgpu.Buffer cameraBuffer, ulong cameraSize, TextureFormat format)
    {
        _device = device;
        _layout = device.CreateBindGroupLayout(
        [
            BindGroupLayoutEntry.Buffer(0, ShaderStage.Vertex, WGPUBufferBindingType.Uniform, false, cameraSize),
            BindGroupLayoutEntry.Buffer(1, ShaderStage.Vertex | ShaderStage.Fragment, WGPUBufferBindingType.Uniform, false, 48)
        ]);
        _pipelineLayout = device.CreatePipelineLayout([_layout]);
        _skyUniform = device.CreateBuffer(new BufferDescriptor { Size = 48, Usage = BufferUsage.Uniform | BufferUsage.CopyDst });

        using var shader = device.CreateShaderModule(new ShaderModuleDescriptor
        {
            Source = ShaderSource.FromWgsl(System.Text.Encoding.UTF8.GetBytes(Shaders.ShaderLibrary.Load("sky_disc")))
        });

        _pipeline = device.CreateRenderPipeline(
            shader,
            "vs_main"u8,
            shader,
            "fs_main"u8,
            [new ColorTargetState { Format = BackendConvert.ToWgpu(format), WriteMask = ColorWriteMask.All, Blend = null }],
            layout: _pipelineLayout,
            primitive: PrimitiveState.Default with { CullMode = WGPUCullMode.None },
            multisample: MultisampleState.Default,
            depthStencil: DepthStencilState.Depth(WGPUTextureFormat.Depth32Float, WGPUCompareFunction.Always, write: false),
            vertexStride: VertexLayout.SkyDiscStride,
            vertexStepMode: WGPUVertexStepMode.Vertex,
            vertexAttributes: VertexLayout.SkyDiscAttributes());

        _bindGroup = device.CreateBindGroup(
            _layout,
            [BindGroupEntry.Buffer(0, cameraBuffer, 0, cameraSize), BindGroupEntry.Buffer(1, _skyUniform, 0, 48)]);

        const int segments = 9;
        var positions = new float[10 * 3];
        positions[1] = 16f;
        for (var i = 0; i < segments; i++)
        {
            var angle = (-180 + (i * 45)) * (MathF.PI / 180f);
            var offset = (i + 1) * 3;
            positions[offset] = 512f * MathF.Cos(angle);
            positions[offset + 1] = 16f;
            positions[offset + 2] = 512f * MathF.Sin(angle);
        }

        var indices = new uint[8 * 3];
        for (var i = 0; i < 8; i++)
        {
            indices[i * 3] = 0;
            indices[i * 3 + 1] = (uint)(i + 1);
            indices[i * 3 + 2] = (uint)(i + 2);
        }

        _vertex = device.CreateBufferInit(new BufferInitDescriptor { Usage = BufferUsage.Vertex }, positions.AsSpan());
        _index = device.CreateBufferInit(new BufferInitDescriptor { Usage = BufferUsage.Index }, indices.AsSpan());
        _indexCount = (uint)indices.Length;
    }

    public void Update(Vector3 skyColour, Vector3 fogColour, float skyFogEnd, Vector3 eye)
    {
        var floats = new float[12];
        floats[0] = skyColour.X;
        floats[1] = skyColour.Y;
        floats[2] = skyColour.Z;
        floats[3] = 1f;
        floats[4] = fogColour.X;
        floats[5] = fogColour.Y;
        floats[6] = fogColour.Z;
        floats[7] = 1f;
        floats[8] = skyFogEnd;
        floats[9] = eye.X;
        floats[10] = eye.Y;
        floats[11] = eye.Z;
        var bytes = MemoryMarshal.AsBytes(floats.AsSpan()).ToArray();
        _device.Queue.WriteBuffer(_skyUniform, 0, bytes.AsSpan());
    }

    public void Draw(RenderPassEncoder pass)
    {
        pass.SetPipeline(_pipeline);
        pass.SetBindGroup(0, _bindGroup, []);
        pass.SetVertexBuffer(0, _vertex);
        pass.SetIndexBuffer(_index, WGPUIndexFormat.Uint32);
        pass.DrawIndexed(_indexCount);
    }

    public void Dispose()
    {
        _vertex.Dispose();
        _index.Dispose();
        _skyUniform.Dispose();
        _bindGroup.Dispose();
        _pipeline.Dispose();
        _pipelineLayout.Dispose();
        _layout.Dispose();
    }
}
