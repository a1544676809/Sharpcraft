// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Brew.Wgpu;
using Brew.Wgpu.Native;
using System.Numerics;

namespace Sharpcraft.Framework.Rendering;


public static partial class OffscreenRenderer
{
    /// <summary>
    /// Owns the GPU objects for one frame's celestial bodies: textures, a shared bind-group layout and pipeline, plus a
    /// vertex/index/transform buffer and bind group per draw. Built per frame and disposed with it.
    /// </summary>
    internal sealed class CelestialResources : IDisposable
    {
        // mat4x4 model (64 bytes) followed by vec4 tint (16 bytes); matches celestial.wgsl CelestialTransform.
        private const ulong TransformUniformSize = 80;

        private readonly IRenderBackend _backend;
        private readonly TextureHandle[] _textures;
        private readonly TextureViewHandle[] _views;
        private readonly SamplerHandle _sampler;
        private readonly BindGroupLayoutHandle _layout;
        private readonly PipelineLayoutHandle _pipelineLayout;
        private readonly PipelineHandle _pipeline;
        private BufferHandle[] _vertexBuffers = [];
        private BufferHandle[] _indexBuffers = [];
        private BindGroupHandle[] _bindGroups = [];
        private uint[] _indexCounts = [];
        private readonly BufferHandle _cameraBuffer;
        private readonly ulong _cameraSize;
        private readonly List<(BufferHandle Vertex, BufferHandle Index, BindGroupHandle BindGroup)> _retired = new();

        public CelestialResources(IRenderBackend backend, FrameRequest request, BufferHandle cameraBuffer, ulong cameraSize, TextureFormat format = TextureFormat.Rgba8Unorm)
        {
            var textures = request.CelestialTextures!;
            var draws = request.CelestialDraws!;
            _backend = backend;
            _cameraBuffer = cameraBuffer;
            _cameraSize = cameraSize;

            _textures = new TextureHandle[textures.Length];
            _views = new TextureViewHandle[textures.Length];

            for (var index = 0; index < textures.Length; index++)
            {
                var data = textures[index];
                _textures[index] = backend.CreateTexture(new TextureDesc(
                    (uint)data.Width,
                    (uint)data.Height,
                    TextureFormat.Rgba8Unorm,
                    1,
                    TextureUsages.TextureBinding | TextureUsages.CopyDst));
                backend.WriteTexture(
                    _textures[index],
                    data.Rgba,
                    (uint)(data.Width * 4),
                    (uint)data.Width,
                    (uint)data.Height,
                    0);
                _views[index] = backend.CreateTextureView(_textures[index]);
            }

            // Nearest sampling keeps the crisp pixel-art sun and moon discs from smearing.
            _sampler = backend.CreateSampler(AddressMode.ClampToEdge, FilterMode.Nearest, FilterMode.Nearest, useMipmaps: false);

            _layout = backend.CreateBindGroupLayout(
            [
                BindGroupLayoutEntryDesc.UniformBuffer(0, ShaderStages.Vertex) with { MinBindingSize = cameraSize },
                BindGroupLayoutEntryDesc.Texture(1, ShaderStages.Fragment),
                BindGroupLayoutEntryDesc.Sampler(2, ShaderStages.Fragment)
            ]);

            _pipelineLayout = backend.CreatePipelineLayout([_layout]);

            // Described to the seam rather than built from a native shader module, so every pipeline is created in one place.
            var source = System.Text.Encoding.UTF8.GetBytes(Shaders.ShaderLibrary.Load("celestial"));
            _pipeline = backend.CreateRenderPipeline(new PipelineDesc(
                source,
                "vs_celestial",
                source,
                "fs_celestial",
                _pipelineLayout,
                [
                    // The reference draws celestial bodies with BlendFunction.OVERLAY: colour SrcAlpha/One, alpha One/Zero.
                    // That adds the body onto the sky and makes black/transparent pixels contribute nothing.
                    new ColorTargetDesc(
                        format,
                        new BlendStateDesc(
                            new BlendComponentDesc(BlendOperation.Add, BlendFactor.SrcAlpha, BlendFactor.One),
                            new BlendComponentDesc(BlendOperation.Add, BlendFactor.One, BlendFactor.Zero)))
                ],
                // Bodies are part of the backdrop: depth always passes and nothing is written, so terrain covers them.
                new DepthStateDesc(TextureFormat.Depth32Float, CompareFunction.Always, false),
                CullMode.None,
                FrontFace.Ccw,
                (ulong)VertexLayout.CelestialStride,
                VertexStepMode.Vertex,
                LayoutAttributes.From(VertexLayout.CelestialAttributes())));

            SetDraws(draws);
        }

        /// <summary>Replaces the sun/moon draws while keeping the loaded textures and pipeline (per-frame angles).</summary>
        public void SetDraws(IReadOnlyList<CelestialQuadDraw> draws)
        {
            foreach (var (vertex, index, bindGroup) in _retired)
            {
                _backend.Destroy(bindGroup);
                _backend.Destroy(index);
                _backend.Destroy(vertex);
            }

            _retired.Clear();
            for (var i = 0; i < _bindGroups.Length; i++)
            {
                _retired.Add((_vertexBuffers[i], _indexBuffers[i], _bindGroups[i]));
            }

            _vertexBuffers = new BufferHandle[draws.Count];
            _indexBuffers = new BufferHandle[draws.Count];
            _bindGroups = new BindGroupHandle[draws.Count];
            _indexCounts = new uint[draws.Count];

            for (var index = 0; index < draws.Count; index++)
            {
                var draw = draws[index];
                // The model matrix is deliberately NOT applied: it only carries the camera translation, which the sky
                // matrix in the camera uniform now provides by not being applied at all (T-811, T-812). The vertices stay
                // exactly as CelestialGeometry produced them, on a dome around the origin.
                var vertices = CopyTint(draw.Vertices, draw.Tint);
                _vertexBuffers[index] = _backend.CreateBufferInit(
                    new BufferDesc((ulong)vertices.Length * sizeof(float), BufferUsages.Vertex),
                    System.Runtime.InteropServices.MemoryMarshal.AsBytes(vertices.AsSpan()));
                _indexBuffers[index] = _backend.CreateBufferInit(
                    new BufferDesc((ulong)draw.Indices.Length * sizeof(ushort), BufferUsages.Index),
                    System.Runtime.InteropServices.MemoryMarshal.AsBytes(draw.Indices.AsSpan()));

                _bindGroups[index] = _backend.CreateBindGroup(
                    _layout,
                    [
                        BindGroupEntryDesc.Uniform(0, _cameraBuffer, 0, _cameraSize),
                        BindGroupEntryDesc.Texture(1, _views[draw.TextureIndex]),
                        BindGroupEntryDesc.Sampler(2, _sampler)
                    ]);

                _indexCounts[index] = (uint)draw.Indices.Length;
            }
        }

        /// <summary>Draws every body with the shared pipeline; each has its own texture, transform and buffers.</summary>
        public void Draw(IRenderBackend backend, RenderPassHandle pass)
        {
            if (_bindGroups.Length == 0)
            {
                return;
            }

            backend.SetPipeline(pass, _pipeline);

            for (var index = 0; index < _bindGroups.Length; index++)
            {
                backend.SetBindGroup(pass, 0, _bindGroups[index]);
                backend.SetVertexBuffer(pass, 0, _vertexBuffers[index]);
                backend.SetIndexBuffer(pass, _indexBuffers[index], IndexFormat.Uint16);
                backend.DrawIndexed(pass, _indexCounts[index], 1, 0, 0, 0);
            }
        }

        /// <summary>
        /// Kept for the call sites: the dome is re-centred by the shared sky matrix, so a camera move uploads nothing.
        /// </summary>
        /// <param name="eyeDelta">Unused; the bodies follow the camera through the camera uniform (T-812).</param>
        public void UpdateCamera(Vector3 eyeDelta)
        {
            _ = eyeDelta;
            NoOpCameraUpdates++;
        }

        /// <summary>Camera updates that still touched a buffer; zero now that the sky matrix carries the camera (T-812).</summary>
        public long AppliedCameraUpdates { get; private set; }

        /// <summary>Camera updates skipped by the old threshold; kept so the existing diagnostics still compile.</summary>
        public long SkippedCameraUpdates => 0;

        /// <summary>How often the call site asked the bodies to follow the camera; every one is a no-op now.</summary>
        public long NoOpCameraUpdates { get; private set; }

        /// <summary>
        /// Copies the per-body tint onto every vertex, so the shader needs no per-draw uniform (T-812).
        /// </summary>
        /// <param name="vertices">Source vertices: position (3) then uv (2) floats per vertex.</param>
        /// <param name="tint">Tint copied onto every vertex.</param>
        /// <returns>The vertex array with the tint appended: position (3), uv (2), tint (4).</returns>
        private static float[] CopyTint(float[] vertices, Vector4 tint)
        {
            const int sourceStride = 5;
            const int bakedStride = 9;
            var count = vertices.Length / sourceStride;
            var result = new float[count * bakedStride];
            for (var vertex = 0; vertex < count; vertex++)
            {
                var source = vertex * sourceStride;
                var target = vertex * bakedStride;
                result[target] = vertices[source];
                result[target + 1] = vertices[source + 1];
                result[target + 2] = vertices[source + 2];
                result[target + 3] = vertices[source + 3];
                result[target + 4] = vertices[source + 4];
                result[target + 5] = tint.X;
                result[target + 6] = tint.Y;
                result[target + 7] = tint.Z;
                result[target + 8] = tint.W;
            }

            return result;
        }

        public void Dispose()
        {
            foreach (var bindGroup in _bindGroups)
            {
                _backend.Destroy(bindGroup);
            }


            foreach (var buffer in _indexBuffers)
            {
                _backend.Destroy(buffer);
            }

            foreach (var buffer in _vertexBuffers)
            {
                _backend.Destroy(buffer);
            }

            foreach (var (vertex, index, bindGroup) in _retired)
            {
                _backend.Destroy(bindGroup);
                _backend.Destroy(index);
                _backend.Destroy(vertex);
            }

            _backend.Destroy(_pipeline);
            _backend.Destroy(_pipelineLayout);
            _backend.Destroy(_layout);
            _backend.Destroy(_sampler);

            foreach (var view in _views)
            {
                _backend.Destroy(view);
            }

            foreach (var texture in _textures)
            {
                _backend.Destroy(texture);
            }
        }
    }

    /// <summary>
    /// Owns the GPU objects for one frame's position-colour celestial draws (sunrise fan, stars): a pipeline per blend
    /// mode and, per draw, a vertex/index/transform buffer and bind group. Built per frame and disposed with it.
    /// </summary>
    internal sealed class CelestialColorResources : IDisposable
    {
        private const ulong TransformUniformSize = 16;

        private readonly IRenderBackend _backend;
        private readonly BindGroupLayoutHandle _layout;
        private readonly PipelineLayoutHandle _pipelineLayout;
        private readonly PipelineHandle _translucentPipeline;
        private readonly PipelineHandle _overlayPipeline;
        private readonly PipelineHandle _opaquePipeline;
        private BufferHandle[] _vertexBuffers = [];
        private BufferHandle[] _indexBuffers = [];
        private BufferHandle[] _transformBuffers = [];

        private readonly byte[] _tintScratch = new byte[TransformUniformSize];
        private BindGroupHandle[] _bindGroups = [];
        private uint[] _indexCounts = [];
        private bool[] _overlay = [];
        private bool[] _opaque = [];
        private Vector4[] _tints = [];
        private Vector4[] _writtenTints = [];
        private readonly List<(BufferHandle Vertex, BufferHandle Index, BufferHandle Transform, BindGroupHandle BindGroup)> _retired = new();
        private readonly BufferHandle _cameraBuffer;
        private readonly ulong _cameraSize;
        private long[] _vertexCapacity = [];
        private long[] _indexCapacity = [];
        private float[][] _sourceVertices = [];
        private uint[][] _sourceIndices = [];

        public CelestialColorResources(IRenderBackend backend, FrameRequest request, BufferHandle cameraBuffer, ulong cameraSize, TextureFormat format = TextureFormat.Rgba8Unorm, WGPUCompareFunction depthCompare = WGPUCompareFunction.Always)
        {
            var draws = request.CelestialColorDraws ?? [];
            _backend = backend;
            _cameraBuffer = cameraBuffer;
            _cameraSize = cameraSize;
            _layout = backend.CreateBindGroupLayout(
            [
                BindGroupLayoutEntryDesc.UniformBuffer(0, ShaderStages.Vertex) with { MinBindingSize = cameraSize },
                BindGroupLayoutEntryDesc.UniformBuffer(1, ShaderStages.Vertex | ShaderStages.Fragment) with { MinBindingSize = TransformUniformSize }
            ]);

            _pipelineLayout = backend.CreatePipelineLayout([_layout]);

            // The seam takes the source rather than a module, so this builds the pipeline in one place like every other.
            var source = System.Text.Encoding.UTF8.GetBytes(Shaders.ShaderLibrary.Load("position_color"));
            _translucentPipeline = CreatePositionColorPipeline(backend, source, _pipelineLayout, overlay: false, format, depthCompare);
            _overlayPipeline = CreatePositionColorPipeline(backend, source, _pipelineLayout, overlay: true, format, depthCompare);
            _opaquePipeline = CreatePositionColorPipeline(backend, source, _pipelineLayout, overlay: false, format, depthCompare, opaque: true);

            SetDraws(draws);
        }

        /// <summary>
        /// Replaces the draw set (used every frame for clouds). The previous buffers are retired and released on the next
        /// update, so a buffer is not destroyed while the frame that referenced it may still be in flight.
        /// </summary>
        public void SetDraws(IReadOnlyList<CelestialColorDraw> draws)
        {
            var reuse = _bindGroups.Length == draws.Count && _vertexCapacity.Length == draws.Count;
            if (reuse)
            {
                for (var i = 0; i < draws.Count; i++)
                {
                    var draw = draws[i];
                    if ((long)draw.Vertices.Length * sizeof(float) > _vertexCapacity[i]
                        || (long)draw.Indices.Length * sizeof(uint) > _indexCapacity[i])
                    {
                        reuse = false;
                        break;
                    }
                }
            }

            if (reuse)
            {
                for (var i = 0; i < draws.Count; i++)
                {
                    WriteDraw(i, draws[i]);
                }

                return;
            }

            // Capacity/structure changed: recreate buffers (rare - only at start or when the cloud face count grows).
            foreach (var (vertex, index, transform, bindGroup) in _retired)
            {
                _backend.Destroy(bindGroup);
                _backend.Destroy(transform);
                _backend.Destroy(index);
                _backend.Destroy(vertex);
            }

            _retired.Clear();
            for (var i = 0; i < _bindGroups.Length; i++)
            {
                _retired.Add((_vertexBuffers[i], _indexBuffers[i], _transformBuffers[i], _bindGroups[i]));
            }

            _vertexBuffers = new BufferHandle[draws.Count];
            _indexBuffers = new BufferHandle[draws.Count];
            _transformBuffers = new BufferHandle[draws.Count];
            _bindGroups = new BindGroupHandle[draws.Count];
            _indexCounts = new uint[draws.Count];
            _overlay = new bool[draws.Count];
            _opaque = new bool[draws.Count];
            _tints = new Vector4[draws.Count];
            _writtenTints = new Vector4[draws.Count];
            Array.Fill(_writtenTints, new Vector4(float.NaN, float.NaN, float.NaN, float.NaN));
            _vertexCapacity = new long[draws.Count];
            _indexCapacity = new long[draws.Count];
            _sourceVertices = new float[draws.Count][];
            _sourceIndices = new uint[draws.Count][];

            for (var index = 0; index < draws.Count; index++)
            {
                var draw = draws[index];
                _vertexCapacity[index] = Math.Max((long)draw.Vertices.Length * sizeof(float), 4096);
                _indexCapacity[index] = Math.Max((long)draw.Indices.Length * sizeof(uint), 4096);
                _vertexBuffers[index] = _backend.CreateBuffer(
                    new BufferDesc((ulong)_vertexCapacity[index], BufferUsages.Vertex | BufferUsages.CopyDst));
                _indexBuffers[index] = _backend.CreateBuffer(
                    new BufferDesc((ulong)_indexCapacity[index], BufferUsages.Index | BufferUsages.CopyDst));
                _transformBuffers[index] = _backend.CreateBuffer(
                    new BufferDesc(TransformUniformSize, BufferUsages.Uniform | BufferUsages.CopyDst));

                _bindGroups[index] = _backend.CreateBindGroup(
                    _layout,
                    [
                        BindGroupEntryDesc.Uniform(0, _cameraBuffer, 0, _cameraSize),
                        BindGroupEntryDesc.Uniform(1, _transformBuffers[index], 0, TransformUniformSize)
                    ]);

                WriteDraw(index, draw);
            }
        }

        private void WriteDraw(int index, CelestialColorDraw draw)
        {
            _tints[index] = draw.Tint;
            _indexCounts[index] = (uint)draw.Indices.Length;
            _overlay[index] = draw.Blend == CelestialColorBlend.Overlay;
            _opaque[index] = draw.Blend == CelestialColorBlend.Opaque;

            // The cloud mesh is cached and only its translation changes per frame; skip the (multi-megabyte) upload
            // unless the caller actually provided new vertex/index arrays.
            var unchanged = index < _sourceVertices.Length
                && ReferenceEquals(_sourceVertices[index], draw.Vertices)
                && ReferenceEquals(_sourceIndices[index], draw.Indices);
            if (!unchanged)
            {
                _backend.WriteBuffer(
                    _vertexBuffers[index],
                    System.Runtime.InteropServices.MemoryMarshal.AsBytes(draw.Vertices.AsSpan()),
                    0);
                _backend.WriteBuffer(
                    _indexBuffers[index],
                    System.Runtime.InteropServices.MemoryMarshal.AsBytes(draw.Indices.AsSpan()),
                    0);
                _sourceVertices[index] = draw.Vertices;
                _sourceIndices[index] = draw.Indices;
            }

            // The model matrix is deliberately not applied: it only carried the camera translation, which the sky matrix
            // now supplies by not being applied at all (T-811). Only the tint remains per draw, so the uniform is written
            // when the tint changes instead of every frame (T-810).
            if (index < _writtenTints.Length && _writtenTints[index] == draw.Tint)
            {
                TintSkips++;
                return;
            }

            _tintScratch[0] = (byte)(draw.Tint.X * 255f);
            BitConverter.TryWriteBytes(_tintScratch.AsSpan(0), draw.Tint.X);
            BitConverter.TryWriteBytes(_tintScratch.AsSpan(4), draw.Tint.Y);
            BitConverter.TryWriteBytes(_tintScratch.AsSpan(8), draw.Tint.Z);
            BitConverter.TryWriteBytes(_tintScratch.AsSpan(12), draw.Tint.W);
            _backend.WriteBuffer(_transformBuffers[index], _tintScratch, 0);
            _writtenTints[index] = draw.Tint;
            TintWrites++;
        }

        /// <summary>Draws every colour body, picking the pipeline that matches its blend mode.</summary>
        public void Draw(IRenderBackend backend, RenderPassHandle pass)
        {
            if (_bindGroups.Length == 0)
            {
                return;
            }

            for (var index = 0; index < _bindGroups.Length; index++)
            {
                backend.SetPipeline(pass, _opaque[index] ? _opaquePipeline : (_overlay[index] ? _overlayPipeline : _translucentPipeline));
                backend.SetBindGroup(pass, 0, _bindGroups[index]);
                backend.SetVertexBuffer(pass, 0, _vertexBuffers[index]);
                backend.SetIndexBuffer(pass, _indexBuffers[index], IndexFormat.Uint32);
                backend.DrawIndexed(pass, _indexCounts[index], 1, 0, 0, 0);
            }
        }

        /// <summary>
        /// Kept for the call sites: the dome is re-centred by the shared sky matrix, so a camera move uploads nothing.
        /// </summary>
        /// <param name="eyeDelta">Unused; the bodies follow the camera through the camera uniform (T-810).</param>
        public void UpdateCamera(Vector3 eyeDelta)
        {
            _ = eyeDelta;
            NoOpCameraUpdates++;
        }

        /// <summary>Camera updates that still touched a buffer; zero now that the sky matrix carries the camera (T-810).</summary>
        public long AppliedCameraUpdates { get; private set; }

        /// <summary>Camera updates skipped by the old threshold; kept so the existing diagnostics still compile.</summary>
        public long SkippedCameraUpdates => 0;

        /// <summary>How often the call site asked the bodies to follow the camera; every one is a no-op now.</summary>
        public long NoOpCameraUpdates { get; private set; }

        /// <summary>Per-draw tint uploads actually issued (diagnostic for T-810).</summary>
        public long TintWrites { get; private set; }

        /// <summary>Frames where the tint was unchanged, so the upload was skipped (diagnostic for T-810).</summary>
        public long TintSkips { get; private set; }

        public void Dispose()
        {
            foreach (var bindGroup in _bindGroups)
            {
                _backend.Destroy(bindGroup);
            }

            foreach (var buffer in _transformBuffers)
            {
                _backend.Destroy(buffer);
            }

            foreach (var buffer in _indexBuffers)
            {
                _backend.Destroy(buffer);
            }

            foreach (var buffer in _vertexBuffers)
            {
                _backend.Destroy(buffer);
            }

            foreach (var (vertex, index, transform, bindGroup) in _retired)
            {
                _backend.Destroy(bindGroup);
                _backend.Destroy(transform);
                _backend.Destroy(index);
                _backend.Destroy(vertex);
            }

            _backend.Destroy(_translucentPipeline);
            _backend.Destroy(_overlayPipeline);
            _backend.Destroy(_opaquePipeline);
            _backend.Destroy(_pipelineLayout);
            _backend.Destroy(_layout);
        }
    }
}
