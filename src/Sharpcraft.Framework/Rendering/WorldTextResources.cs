// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Runtime.InteropServices;
using Brew.Wgpu;

namespace Sharpcraft.Framework.Rendering;

/// <summary>
/// World-space text pass: glyph quads sample the font sheet and are blended over the resolved world with depth testing.
/// Background quads use uv.x &lt; 0 to take the solid-colour path.
/// </summary>
/// <remarks>
/// Every resource is a seam handle, so the pass records itself through the backend and the renderer hands it the same
/// handle it began the pass with.
/// </remarks>
internal sealed class WorldTextResources : IDisposable
{
    private readonly IRenderBackend _backend;
    private readonly BindGroupLayoutHandle _layout;
    private readonly PipelineLayoutHandle _pipelineLayout;
    private readonly PipelineHandle _pipeline;
    private readonly TextureHandle _texture;
    private readonly TextureViewHandle _view;
    private readonly SamplerHandle _samplerHandle;
    private readonly BindGroupHandle _bindGroup;
    private BufferHandle _vertex = default;
    private BufferHandle _index = default;
    private uint _indexCount;

    public WorldTextResources(
        IRenderBackend backend,
        Device device,
        TextureData glyphSheet,
        BufferHandle cameraBuffer,
        ulong cameraSize,
        TextureFormat format)
    {
        _backend = backend;
        _texture = backend.CreateTexture(new TextureDesc(
            (uint)glyphSheet.Width,
            (uint)glyphSheet.Height,
            TextureFormat.Rgba8Unorm,
            1,
            TextureUsages.TextureBinding | TextureUsages.CopyDst));
        backend.WriteTexture(
            _texture,
            glyphSheet.Rgba,
            (uint)(glyphSheet.Width * 4),
            (uint)glyphSheet.Width,
            (uint)glyphSheet.Height,
            0);
        _view = backend.CreateTextureView(_texture);
        // Nearest clamp sampling: the seam creates the sampler itself (the same descriptor the shared cache would use),
        // so this resource owns it and releases it with the rest.
        _samplerHandle = backend.CreateSampler(AddressMode.ClampToEdge, FilterMode.Nearest, FilterMode.Nearest, useMipmaps: false);

        _layout = backend.CreateBindGroupLayout(
        [
            BindGroupLayoutEntryDesc.UniformBuffer(0, ShaderStages.Vertex) with { MinBindingSize = cameraSize },
            BindGroupLayoutEntryDesc.Texture(1, ShaderStages.Fragment),
            BindGroupLayoutEntryDesc.Sampler(2, ShaderStages.Fragment)
        ]);
        _pipelineLayout = backend.CreatePipelineLayout([_layout]);
        _bindGroup = backend.CreateBindGroup(
            _layout,
            [
                BindGroupEntryDesc.Uniform(0, cameraBuffer, 0, cameraSize),
                BindGroupEntryDesc.Texture(1, _view),
                BindGroupEntryDesc.Sampler(2, _samplerHandle)
            ]);

        // Described to the backend rather than built here, matching how the frame records its passes.
        var source = System.Text.Encoding.UTF8.GetBytes(Shaders.ShaderLibrary.Load("world_text"));
        _pipeline = backend.CreateRenderPipeline(new PipelineDesc(
            source,
            "vs_main",
            source,
            "fs_main",
            _pipelineLayout,
            [new ColorTargetDesc(
                format,
                new BlendStateDesc(
                    new BlendComponentDesc(BlendOperation.Add, BlendFactor.SrcAlpha, BlendFactor.OneMinusSrcAlpha),
                    new BlendComponentDesc(BlendOperation.Add, BlendFactor.One, BlendFactor.OneMinusSrcAlpha)))],
            new DepthStateDesc(TextureFormat.Depth32Float, CompareFunction.GreaterEqual, false),
            CullMode.None,
            FrontFace.Ccw,
            (ulong)VertexLayout.WorldTextStride,
            VertexStepMode.Vertex,
            LayoutAttributes.From(VertexLayout.WorldTextAttributes())));
    }

    public void SetDraws(IReadOnlyList<WorldTextDraw> draws)
    {
        if (draws.Count == 0)
        {
            _indexCount = 0;
            return;
        }

        var vertices = new List<float>();
        var indices = new List<uint>();
        foreach (var draw in draws)
        {
            var offset = (uint)(vertices.Count / 9);
            vertices.AddRange(draw.Vertices);
            foreach (var index in draw.Indices)
            {
                indices.Add(offset + index);
            }
        }

        DestroyGeometry();
        var vertexArray = vertices.ToArray();
        var indexArray = indices.ToArray();
        _vertex = _backend.CreateBufferInit(
            new BufferDesc((ulong)vertexArray.Length * sizeof(float), BufferUsages.Vertex),
            MemoryMarshal.AsBytes(vertexArray.AsSpan()));
        _index = _backend.CreateBufferInit(
            new BufferDesc((ulong)indexArray.Length * sizeof(uint), BufferUsages.Index),
            MemoryMarshal.AsBytes(indexArray.AsSpan()));
        _indexCount = (uint)indices.Count;
    }

    public void Draw(IRenderBackend backend, RenderPassHandle pass)
    {
        if (_indexCount == 0)
        {
            return;
        }

        backend.SetPipeline(pass, _pipeline);
        backend.SetBindGroup(pass, 0, _bindGroup);
        backend.SetVertexBuffer(pass, 0, _vertex);
        backend.SetIndexBuffer(pass, _index, IndexFormat.Uint32);
        backend.DrawIndexed(pass, _indexCount, 1, 0, 0, 0);
    }

    public void Dispose()
    {
        DestroyGeometry();
        _backend.Destroy(_bindGroup);
        _backend.Destroy(_pipeline);
        _backend.Destroy(_pipelineLayout);
        _backend.Destroy(_layout);
        _backend.Destroy(_samplerHandle);
        _backend.Destroy(_view);
        _backend.Destroy(_texture);
    }

    private void DestroyGeometry()
    {
        if (_vertex.IsValid)
        {
            _backend.Destroy(_vertex);
            _vertex = default;
        }

        if (_index.IsValid)
        {
            _backend.Destroy(_index);
            _index = default;
        }
    }
}
