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
    // Held as seam handles rather than native objects: the draw site passes them straight back, so nothing has to be
    // adopted at the point of use and a second backend only has to implement the seam.
    private readonly WgpuRenderBackend _backend;
    private readonly BindGroupLayoutHandle _layout;
    private readonly PipelineLayoutHandle _pipelineLayout;
    private readonly PipelineHandle _pipeline;
    private readonly BindGroupHandle _bindGroup;
    private readonly BufferHandle _skyUniform;
    private readonly BufferHandle _vertex;
    private readonly BufferHandle _index;
    private readonly uint _indexCount;

    public SkyDiscResources(WgpuRenderBackend backend, BufferHandle cameraBuffer, ulong cameraSize, TextureFormat format)
    {
        _backend = backend;
        _layout = _backend.CreateBindGroupLayout(
        [
            new BindGroupLayoutEntryDesc(0, ShaderStages.Vertex, BindingKind.UniformBuffer) { MinBindingSize = cameraSize },
            new BindGroupLayoutEntryDesc(1, ShaderStages.Vertex | ShaderStages.Fragment, BindingKind.UniformBuffer) { MinBindingSize = 48 }
        ]);
        _pipelineLayout = _backend.CreatePipelineLayout([_layout]);
        _skyUniform = backend.CreateBuffer(new BufferDesc(48, BufferUsages.Uniform | BufferUsages.CopyDst));

        // The pipeline is described to the backend rather than built here, so this resource speaks the same language as
        // the frame that draws it.
        var source = System.Text.Encoding.UTF8.GetBytes(Shaders.ShaderLibrary.Load("sky_disc"));
        _pipeline = _backend.CreateRenderPipeline(new PipelineDesc(
            source,
            "vs_main",
            source,
            "fs_main",
            _pipelineLayout,
            [new ColorTargetDesc(format)],
            new DepthStateDesc(TextureFormat.Depth32Float, CompareFunction.Always, false),
            CullMode.None,
            FrontFace.Ccw,
            (ulong)VertexLayout.SkyDiscStride,
            VertexStepMode.Vertex,
            LayoutAttributes.From(VertexLayout.SkyDiscAttributes())));

        _bindGroup = backend.CreateBindGroup(
            _layout,
            [BindGroupEntryDesc.Uniform(0, cameraBuffer, 0, cameraSize), BindGroupEntryDesc.Uniform(1, _skyUniform, 0, 48)]);

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

        _vertex = backend.CreateBufferInit(
            new BufferDesc((ulong)positions.Length * sizeof(float), BufferUsages.Vertex),
            MemoryMarshal.AsBytes(positions.AsSpan()));
        _index = backend.CreateBufferInit(
            new BufferDesc((ulong)indices.Length * sizeof(uint), BufferUsages.Index),
            MemoryMarshal.AsBytes(indices.AsSpan()));
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
        _backend.WriteBuffer(_skyUniform, bytes, 0);
    }

    public void Draw(IRenderBackend backend, RenderPassHandle pass)
    {
        backend.SetPipeline(pass, _pipeline);
        backend.SetBindGroup(pass, 0, _bindGroup);
        backend.SetVertexBuffer(pass, 0, _vertex);
        backend.SetIndexBuffer(pass, _index, IndexFormat.Uint32);
        backend.DrawIndexed(pass, _indexCount, 1, 0, 0, 1);
    }

    public void Dispose()
    {
        _backend.Destroy(_vertex);
        _backend.Destroy(_index);
        _backend.Destroy(_skyUniform);
        _backend.Destroy(_bindGroup);
        _backend.Destroy(_pipeline);
        _backend.Destroy(_pipelineLayout);
        _backend.Destroy(_layout);
    }
}
