// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using System.Runtime.InteropServices;
using Brew.Wgpu;
using Brew.Wgpu.Native;

namespace Sharpcraft.Framework.Rendering;

/// <summary>
/// Owns the persistent cloud OIT geometry and its transform uniform. The mesh is uploaded only when the cloud cache
/// produces new arrays; every frame writes just the 80-byte transform (model + tint), matching the reference CloudInfo
/// uniform pattern instead of re-expanding the whole mesh.
/// </summary>
internal sealed class CloudOitResources : IDisposable
{
    private const ulong TransformUniformSize = 80;

    private readonly Device _device;
    private readonly Brew.Wgpu.Buffer _transform;
    private const float MinCapacity = 4096;
    private Brew.Wgpu.Buffer _vertex;
    private Brew.Wgpu.Buffer _index;
    private ulong _vertexCapacity = (ulong)MinCapacity;
    private ulong _indexCapacity = (ulong)MinCapacity;
    private uint _indexCount;
    private float[]? _sourceVertices;
    private uint[]? _sourceIndices;

    public CloudOitResources(Device device, Brew.Wgpu.Buffer cameraBuffer, ulong cameraSize)
    {
        _device = device;
        Layout = device.CreateBindGroupLayout(
        [
            BindGroupLayoutEntry.Buffer(0, ShaderStage.Vertex, WGPUBufferBindingType.Uniform, false, cameraSize),
            BindGroupLayoutEntry.Buffer(1, ShaderStage.Vertex | ShaderStage.Fragment, WGPUBufferBindingType.Uniform, false, TransformUniformSize)
        ]);

        _transform = device.CreateBuffer(new BufferDescriptor
        {
            Size = TransformUniformSize,
            Usage = BufferUsage.Uniform | BufferUsage.CopyDst
        });
        BindGroup = device.CreateBindGroup(
            Layout,
            [
                BindGroupEntry.Buffer(0, cameraBuffer, 0, cameraSize),
                BindGroupEntry.Buffer(1, _transform, 0, TransformUniformSize)
            ]);

        _vertex = device.CreateBuffer(new BufferDescriptor { Size = _vertexCapacity, Usage = BufferUsage.Vertex | BufferUsage.CopyDst });
        _index = device.CreateBuffer(new BufferDescriptor { Size = _indexCapacity, Usage = BufferUsage.Index | BufferUsage.CopyDst });
    }

    public BindGroupLayout Layout { get; }

    public BindGroup BindGroup { get; }

    /// <summary>Uploads geometry only when the cache arrays change; always refreshes the transform uniform.</summary>
    public void Update(float[] vertices, uint[] indices, Matrix4x4 model, Vector4 tint)
    {
        if (!ReferenceEquals(_sourceVertices, vertices) || !ReferenceEquals(_sourceIndices, indices))
        {
            UploadGeometry(vertices, indices);
            _sourceVertices = vertices;
            _sourceIndices = indices;
        }

        var transformBytes = new byte[TransformUniformSize];
        var floats = new float[20];
        WriteMatrix(model, floats, 0);
        floats[16] = tint.X;
        floats[17] = tint.Y;
        floats[18] = tint.Z;
        floats[19] = tint.W;
        MemoryMarshal.AsBytes(floats.AsSpan()).CopyTo(transformBytes);
        _device.Queue.WriteBuffer(_transform, 0, transformBytes.AsSpan());
    }

    private void UploadGeometry(float[] vertices, uint[] indices)
    {
        // Expand the cached 7-float cloud vertices into the terrain OIT vertex format once per geometry change: the vertex
        // colour stays brightness, uv.x carries the cloud alpha, and the transform uniform applies model + CLOUD_COLOR.
        var count = vertices.Length / 7;
        var packed = new float[count * 14];
        for (var i = 0; i < count; i++)
        {
            var s = i * 7;
            var d = i * 14;
            packed[d] = vertices[s];
            packed[d + 1] = vertices[s + 1];
            packed[d + 2] = vertices[s + 2];
            packed[d + 3] = vertices[s + 3];
            packed[d + 4] = vertices[s + 4];
            packed[d + 5] = vertices[s + 5];
            packed[d + 6] = Math.Clamp(vertices[s + 6], 0f, 1f); // alpha in uv.x
            packed[d + 7] = 0f;
            packed[d + 8] = 0f;
            packed[d + 9] = 0f;
            packed[d + 10] = 1f;
            packed[d + 11] = 1f;
            packed[d + 12] = 1f;
            packed[d + 13] = 1f;
        }

        var vertexBytes = (ulong)(packed.Length * sizeof(float));
        if (vertexBytes > _vertexCapacity)
        {
            _vertex.Dispose();
            _vertexCapacity = Math.Max(vertexBytes * 2, (ulong)MinCapacity);
            _vertex = _device.CreateBuffer(new BufferDescriptor { Size = _vertexCapacity, Usage = BufferUsage.Vertex | BufferUsage.CopyDst });
        }

        var indexBytes = (ulong)(indices.Length * sizeof(uint));
        if (indexBytes > _indexCapacity)
        {
            _index.Dispose();
            _indexCapacity = Math.Max(indexBytes * 2, (ulong)MinCapacity);
            _index = _device.CreateBuffer(new BufferDescriptor { Size = _indexCapacity, Usage = BufferUsage.Index | BufferUsage.CopyDst });
        }

        if (packed.Length > 0)
        {
            _device.Queue.WriteBuffer(_vertex, 0, MemoryMarshal.AsBytes(packed.AsSpan()));
        }

        if (indices.Length > 0)
        {
            _device.Queue.WriteBuffer(_index, 0, MemoryMarshal.AsBytes(indices.AsSpan()));
        }

        _indexCount = (uint)indices.Length;
    }

    public void DrawGeometry(RenderPassEncoder pass)
    {
        if (_indexCount == 0)
        {
            return;
        }

        pass.SetVertexBuffer(0, _vertex);
        pass.SetIndexBuffer(_index, WGPUIndexFormat.Uint32);
        pass.DrawIndexed(_indexCount);
    }

    public void Dispose()
    {
        _vertex.Dispose();
        _index.Dispose();
        _transform.Dispose();
    }

    private static void WriteMatrix(Matrix4x4 matrix, float[] destination, int offset)
    {
        destination[offset] = matrix.M11;
        destination[offset + 1] = matrix.M12;
        destination[offset + 2] = matrix.M13;
        destination[offset + 3] = matrix.M14;
        destination[offset + 4] = matrix.M21;
        destination[offset + 5] = matrix.M22;
        destination[offset + 6] = matrix.M23;
        destination[offset + 7] = matrix.M24;
        destination[offset + 8] = matrix.M31;
        destination[offset + 9] = matrix.M32;
        destination[offset + 10] = matrix.M33;
        destination[offset + 11] = matrix.M34;
        destination[offset + 12] = matrix.M41;
        destination[offset + 13] = matrix.M42;
        destination[offset + 14] = matrix.M43;
        destination[offset + 15] = matrix.M44;
    }
}
