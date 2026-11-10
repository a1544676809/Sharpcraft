// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using System.Runtime.InteropServices;

namespace Sharpcraft.Framework.Rendering;

/// <summary>
/// Owns the persistent cloud OIT geometry and its transform uniform. The mesh is uploaded only when the cloud cache
/// produces new arrays; every frame writes just the 80-byte transform (model + tint), matching the reference CloudInfo
/// uniform pattern instead of re-expanding the whole mesh.
/// </summary>
/// <remarks>
/// Every resource is a seam handle: the layout, the bind group, the buffers and the draw all go through the backend, so
/// this type holds nothing the ring could not re-create on a different implementation.
/// </remarks>
internal sealed class CloudOitResources : IDisposable
{
    private const ulong TransformUniformSize = 80;

    private readonly IRenderBackend _backend;
    private readonly BufferHandle _transform;
    private const float MinCapacity = 4096;
    private BufferHandle _vertex;
    private BufferHandle _index;
    private ulong _vertexCapacity = (ulong)MinCapacity;
    private ulong _indexCapacity = (ulong)MinCapacity;
    private uint _indexCount;
    private float[]? _sourceVertices;
    private uint[]? _sourceIndices;

    public CloudOitResources(IRenderBackend backend, BufferHandle cameraBuffer, ulong cameraSize)
    {
        _backend = backend;
        Layout = backend.CreateBindGroupLayout(
        [
            BindGroupLayoutEntryDesc.UniformBuffer(0, ShaderStages.Vertex) with { MinBindingSize = cameraSize },
            BindGroupLayoutEntryDesc.UniformBuffer(1, ShaderStages.Vertex | ShaderStages.Fragment) with { MinBindingSize = TransformUniformSize }
        ]);

        _transform = backend.CreateBuffer(new BufferDesc(TransformUniformSize, BufferUsages.Uniform | BufferUsages.CopyDst));
        BindGroup = backend.CreateBindGroup(
            Layout,
            [
                BindGroupEntryDesc.Uniform(0, cameraBuffer, 0, cameraSize),
                BindGroupEntryDesc.Uniform(1, _transform, 0, TransformUniformSize)
            ]);

        _vertex = backend.CreateBuffer(new BufferDesc(_vertexCapacity, BufferUsages.Vertex | BufferUsages.CopyDst));
        _index = backend.CreateBuffer(new BufferDesc(_indexCapacity, BufferUsages.Index | BufferUsages.CopyDst));
    }

    public BindGroupLayoutHandle Layout { get; }

    public BindGroupHandle BindGroup { get; }

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
        _backend.WriteBuffer(_transform, transformBytes, 0);
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
            _backend.Destroy(_vertex);
            _vertexCapacity = Math.Max(vertexBytes * 2, (ulong)MinCapacity);
            _vertex = _backend.CreateBuffer(new BufferDesc(_vertexCapacity, BufferUsages.Vertex | BufferUsages.CopyDst));
        }

        var indexBytes = (ulong)(indices.Length * sizeof(uint));
        if (indexBytes > _indexCapacity)
        {
            _backend.Destroy(_index);
            _indexCapacity = Math.Max(indexBytes * 2, (ulong)MinCapacity);
            _index = _backend.CreateBuffer(new BufferDesc(_indexCapacity, BufferUsages.Index | BufferUsages.CopyDst));
        }

        if (packed.Length > 0)
        {
            _backend.WriteBuffer(_vertex, MemoryMarshal.AsBytes(packed.AsSpan()), 0);
        }

        if (indices.Length > 0)
        {
            _backend.WriteBuffer(_index, MemoryMarshal.AsBytes(indices.AsSpan()), 0);
        }

        _indexCount = (uint)indices.Length;
    }

    /// <summary>Records the cloud mesh into a pass that the caller has already bound the pipeline and atlas for.</summary>
    public void DrawGeometry(IRenderBackend backend, RenderPassHandle pass)
    {
        if (_indexCount == 0)
        {
            return;
        }

        backend.SetVertexBuffer(pass, 0, _vertex);
        backend.SetIndexBuffer(pass, _index, IndexFormat.Uint32);
        backend.DrawIndexed(pass, _indexCount, 1, 0, 0, 0);
    }

    public void Dispose()
    {
        _backend.Destroy(_vertex);
        _backend.Destroy(_index);
        _backend.Destroy(_transform);
        _backend.Destroy(BindGroup);
        _backend.Destroy(Layout);
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
