// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using System.Runtime.InteropServices;

namespace Sharpcraft.Framework.Rendering;

/// <summary>
/// Owns the cloud transparency resources: the transform uniform, the per-frame scalars the procedural vertex stage
/// needs, and the face table it reads (T-827).
/// </summary>
/// <remarks>
/// Clouds are no longer uploaded as vertices. The reference sends three integers per face and generates the corners in
/// the vertex shader, which is twelve bytes per face against the hundred and twelve an explicit quad costs; the
/// behaviour is written out in docs/specs/render/cloud-geometry.md. The grid scaling rides in the model matrix, so the
/// only scalar the shader cannot derive is the fade distance.
/// <para>
/// Every resource is a seam handle: the layout, the bind group, the buffers and the draw all go through the backend, so
/// this type holds nothing the ring could not re-create on a different implementation.
/// </para>
/// </remarks>
internal sealed class CloudOitResources : IDisposable
{
    private const ulong TransformUniformSize = 80;

    /// <summary>One vec4: the fade distance in x, the rest padding for the 16 byte uniform alignment.</summary>
    private const ulong CloudInfoSize = 16;

    /// <summary>Size of one cloud cell across, matching CloudGeometryBuilder's default.</summary>
    private const float CellSize = 12f;

    /// <summary>Thickness of the cloud slab, matching CloudGeometryBuilder's default.</summary>
    private const float CloudThickness = 4f;

    private const ulong MinFaceCapacity = 4096;

    private readonly IRenderBackend _backend;
    private readonly BufferHandle _camera;
    private readonly ulong _cameraSize;
    private readonly BufferHandle _transform;
    private readonly BufferHandle _cloudInfo;
    private BufferHandle _faces;
    private ulong _faceCapacity = MinFaceCapacity;
    private uint _faceCount;
    private int[]? _sourceFaces;

    public CloudOitResources(IRenderBackend backend, BufferHandle cameraBuffer, ulong cameraSize)
    {
        _backend = backend;
        _camera = cameraBuffer;
        _cameraSize = cameraSize;
        Layout = backend.CreateBindGroupLayout(
        [
            BindGroupLayoutEntryDesc.UniformBuffer(0, ShaderStages.Vertex) with { MinBindingSize = cameraSize },
            BindGroupLayoutEntryDesc.UniformBuffer(1, ShaderStages.Vertex | ShaderStages.Fragment) with { MinBindingSize = TransformUniformSize },
            BindGroupLayoutEntryDesc.UniformBuffer(2, ShaderStages.Vertex) with { MinBindingSize = CloudInfoSize },
            // Read only storage is allowed in the vertex stage; only writable storage is not. The rest of the project
            // binds storage in compute, so this is the first vertex stage use and the pipeline build is what proves it.
            new BindGroupLayoutEntryDesc(3, ShaderStages.Vertex, BindingKind.ReadOnlyStorageBuffer)
        ]);

        _transform = backend.CreateBuffer(new BufferDesc(TransformUniformSize, BufferUsages.Uniform | BufferUsages.CopyDst));
        _cloudInfo = backend.CreateBuffer(new BufferDesc(CloudInfoSize, BufferUsages.Uniform | BufferUsages.CopyDst));
        _faces = backend.CreateBuffer(new BufferDesc(_faceCapacity, BufferUsages.Storage | BufferUsages.CopyDst));

        BindGroup = CreateBindGroup();
    }

    private BindGroupHandle CreateBindGroup() =>
        _backend.CreateBindGroup(
            Layout,
            [
                BindGroupEntryDesc.Uniform(0, _camera, 0, _cameraSize),
                BindGroupEntryDesc.Uniform(1, _transform, 0, TransformUniformSize),
                BindGroupEntryDesc.Uniform(2, _cloudInfo, 0, CloudInfoSize),
                BindGroupEntryDesc.ReadOnlyStorage(3, _faces, 0, _faceCapacity)
            ]);

    public BindGroupLayoutHandle Layout { get; }

    public BindGroupHandle BindGroup { get; private set; }

    /// <summary>
    /// Uploads the face table when it changes and always refreshes the uniforms the vertex stage reads.
    /// </summary>
    /// <param name="faces">The face table, three words per face.</param>
    /// <param name="faceCount">How many faces it describes, so this layer needs no opinion on the word count.</param>
    /// <param name="origin">World position of the cloud grid's corner.</param>
    /// <param name="tint">Colour and alpha modulation, as before.</param>
    /// <param name="fogEnd">Distance at which a cloud fades out.</param>
    public void Update(int[] faces, int faceCount, Vector3 origin, Vector4 tint, float fogEnd)
    {
        ArgumentNullException.ThrowIfNull(faces);

        if (!ReferenceEquals(_sourceFaces, faces))
        {
            UploadFaces(faces, faceCount);
            _sourceFaces = faces;
        }

        // Scale the cell grid first, then move it into place; the shader works in whole cells.
        var model = Matrix4x4.CreateScale(CellSize, CloudThickness, CellSize)
            * Matrix4x4.CreateTranslation(origin);

        var transformBytes = new byte[TransformUniformSize];
        var floats = new float[20];
        WriteMatrix(model, floats, 0);
        floats[16] = tint.X;
        floats[17] = tint.Y;
        floats[18] = tint.Z;
        floats[19] = tint.W;
        MemoryMarshal.AsBytes(floats.AsSpan()).CopyTo(transformBytes);
        _backend.WriteBuffer(_transform, transformBytes, 0);

        var info = new float[4];
        info[0] = fogEnd;
        _backend.WriteBuffer(_cloudInfo, MemoryMarshal.AsBytes(info.AsSpan()), 0);
    }

    private void UploadFaces(int[] faces, int faceCount)
    {
        var bytes = (ulong)(faces.Length * sizeof(int));
        if (bytes > _faceCapacity)
        {
            _backend.Destroy(_faces);
            _faceCapacity = Math.Max(bytes * 2, MinFaceCapacity);
            _faces = _backend.CreateBuffer(new BufferDesc(_faceCapacity, BufferUsages.Storage | BufferUsages.CopyDst));

            // The bind group points at the old buffer, so it has to be rebuilt against the new one.
            _backend.Destroy(BindGroup);
            BindGroup = CreateBindGroup();
        }

        if (faces.Length > 0)
        {
            _backend.WriteBuffer(_faces, MemoryMarshal.AsBytes(faces.AsSpan()), 0);
        }

        _faceCount = (uint)faceCount;
    }

    /// <summary>Records the cloud faces into a pass the caller has already bound the pipeline for.</summary>
    /// <remarks>
    /// Six vertices per face and no vertex buffer: a triangle list needs two triangles a quad, and the vertex shader
    /// derives the corner from the index it is given.
    /// </remarks>
    public void DrawGeometry(IRenderBackend backend, RenderPassHandle pass)
    {
        if (_faceCount == 0)
        {
            return;
        }

        backend.Draw(pass, _faceCount * 6, 1, 0, 0);
    }

    public void Dispose()
    {
        _backend.Destroy(_faces);
        _backend.Destroy(_cloudInfo);
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
