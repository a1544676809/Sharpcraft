// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Brew.Wgpu;
using Brew.Wgpu.Native;

namespace Sharpcraft.Framework.Rendering;

/// <summary>
/// World-space text pass: glyph quads sample the font sheet and are blended over the resolved world with depth testing.
/// Background quads use uv.x &lt; 0 to take the solid-colour path.
/// </summary>
internal sealed class WorldTextResources : IDisposable
{
    private readonly Device _device;
    private readonly BindGroupLayout _layout;
    private readonly PipelineLayout _pipelineLayout;
    private readonly RenderPipeline _pipeline;
    private readonly Texture _texture;
    private readonly TextureView _view;
    private readonly Sampler _sampler;
    private readonly BindGroup _bindGroup;
    private Brew.Wgpu.Buffer _vertex = default;
    private Brew.Wgpu.Buffer _index = default;
    private uint _indexCount;

    public WorldTextResources(
        Device device,
        TextureData glyphSheet,
        Brew.Wgpu.Buffer cameraBuffer,
        ulong cameraSize,
        WGPUTextureFormat format)
    {
        _device = device;
        _texture = device.CreateTexture(new TextureDescriptor
        {
            Size = new WGPUExtent3D { width = (uint)glyphSheet.Width, height = (uint)glyphSheet.Height, depthOrArrayLayers = 1 },
            MipLevelCount = 1,
            SampleCount = 1,
            Dimension = WGPUTextureDimension._2D,
            Format = WGPUTextureFormat.RGBA8Unorm,
            Usage = TextureUsage.TextureBinding | TextureUsage.CopyDst
        });
        device.Queue.WriteTexture(
            _texture,
            glyphSheet.Rgba,
            (uint)(glyphSheet.Width * 4),
            (uint)glyphSheet.Height,
            new WGPUExtent3D { width = (uint)glyphSheet.Width, height = (uint)glyphSheet.Height, depthOrArrayLayers = 1 },
            0,
            new WGPUOrigin3D { x = 0, y = 0, z = 0 });
        _view = _texture.CreateView();
        _sampler = device.CreateSampler(new SamplerDescriptor
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

        _layout = device.CreateBindGroupLayout(
        [
            BindGroupLayoutEntry.Buffer(0, ShaderStage.Vertex, WGPUBufferBindingType.Uniform, false, cameraSize),
            BindGroupLayoutEntry.Texture(1, ShaderStage.Fragment, WGPUTextureSampleType.Float, WGPUTextureViewDimension._2D, false),
            BindGroupLayoutEntry.Sampler(2, ShaderStage.Fragment, WGPUSamplerBindingType.Filtering)
        ]);
        _pipelineLayout = device.CreatePipelineLayout([_layout]);
        _bindGroup = device.CreateBindGroup(
            _layout,
            [
                BindGroupEntry.Buffer(0, cameraBuffer, 0, cameraSize),
                BindGroupEntry.TextureView(1, _view),
                BindGroupEntry.Sampler(2, _sampler)
            ]);

        using var shader = device.CreateShaderModule(new ShaderModuleDescriptor
        {
            Source = ShaderSource.FromWgsl(System.Text.Encoding.UTF8.GetBytes(Shaders.ShaderLibrary.Load("world_text")))
        });

        _pipeline = device.CreateRenderPipeline(
            shader,
            "vs_main"u8,
            shader,
            "fs_main"u8,
            [
                new ColorTargetState
                {
                    Format = format,
                    WriteMask = ColorWriteMask.All,
                    Blend = new WGPUBlendState
                    {
                        color = new WGPUBlendComponent
                        {
                            operation = WGPUBlendOperation.Add,
                            srcFactor = WGPUBlendFactor.SrcAlpha,
                            dstFactor = WGPUBlendFactor.OneMinusSrcAlpha
                        },
                        alpha = new WGPUBlendComponent
                        {
                            operation = WGPUBlendOperation.Add,
                            srcFactor = WGPUBlendFactor.One,
                            dstFactor = WGPUBlendFactor.OneMinusSrcAlpha
                        }
                    }
                }
            ],
            layout: _pipelineLayout,
            primitive: PrimitiveState.Default with { CullMode = WGPUCullMode.None },
            multisample: MultisampleState.Default,
            depthStencil: DepthStencilState.Depth(WGPUTextureFormat.Depth32Float, WGPUCompareFunction.LessEqual, write: false),
            vertexStride: (ulong)VertexLayout.WorldTextStride,
            vertexStepMode: WGPUVertexStepMode.Vertex,
            vertexAttributes: VertexLayout.WorldTextAttributes());
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

        _vertex.Dispose();
        _index.Dispose();
        _vertex = _device.CreateBufferInit(
            new BufferInitDescriptor { Usage = BufferUsage.Vertex }, vertices.ToArray().AsSpan());
        _index = _device.CreateBufferInit(
            new BufferInitDescriptor { Usage = BufferUsage.Index }, indices.ToArray().AsSpan());
        _indexCount = (uint)indices.Count;
    }

    public void Draw(RenderPassEncoder pass)
    {
        if (_indexCount == 0)
        {
            return;
        }

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
        _bindGroup.Dispose();
        _pipeline.Dispose();
        _pipelineLayout.Dispose();
        _layout.Dispose();
        _sampler.Dispose();
        _view.Dispose();
        _texture.Dispose();
    }
}
