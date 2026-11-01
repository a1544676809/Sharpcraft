// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using System.Runtime.InteropServices;
using Brew.Wgpu;
using Brew.Wgpu.Native;

namespace Sharpcraft.Framework.Rendering;

/// <summary>
/// Cloud geometry in the terrain vertex format so the existing reference-OIT stages can render it: a 1x256 alpha ramp
/// supplies the per-vertex cloud alpha through the atlas sampler, while the vertex colour carries brightness * cloud tint.
/// This lets the fancy cloud path reuse the same depth-bounds/transmittance/accumulate/composite pipelines as translucent
/// terrain (docs/specs/render/oit-reference.md).
/// </summary>
internal sealed class CloudOitResources : IDisposable
{
    private readonly Device _device;
    private readonly Texture _alphaTexture;
    private readonly TextureView _alphaView;
    private readonly Sampler _sampler;
    private readonly Brew.Wgpu.Buffer _fog;
    private readonly Brew.Wgpu.Buffer _lightmap;
    private Brew.Wgpu.Buffer _vertex;
    private Brew.Wgpu.Buffer _index;
    private ulong _vertexCapacity;
    private ulong _indexCapacity;
    private uint _indexCount;

    public CloudOitResources(Device device, BindGroupLayout bindGroupLayout, Brew.Wgpu.Buffer cameraBuffer, ulong cameraSize)
    {
        _device = device;

        // 256 alpha steps: RGBA8 white with alpha = i/255, sampled nearest by the cloud OIT vertex uv.x.
        var pixels = new byte[256 * 4];
        for (var i = 0; i < 256; i++)
        {
            pixels[i * 4] = 255;
            pixels[i * 4 + 1] = 255;
            pixels[i * 4 + 2] = 255;
            pixels[i * 4 + 3] = (byte)i;
        }

        _alphaTexture = device.CreateTexture(new TextureDescriptor
        {
            Size = new WGPUExtent3D { width = 256, height = 1, depthOrArrayLayers = 1 },
            MipLevelCount = 1,
            SampleCount = 1,
            Dimension = WGPUTextureDimension._2D,
            Format = WGPUTextureFormat.RGBA8Unorm,
            Usage = TextureUsage.TextureBinding | TextureUsage.CopyDst
        });
        device.Queue.WriteTexture(
            _alphaTexture,
            pixels,
            256 * 4,
            1,
            new WGPUExtent3D { width = 256, height = 1, depthOrArrayLayers = 1 },
            0,
            new WGPUOrigin3D { x = 0, y = 0, z = 0 });
        _alphaView = _alphaTexture.CreateView();
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

        // Identity fog (range far apart) and identity lightmap: cloud colour already includes its tint and fog alpha.
        var fogValues = new float[8];
        fogValues[5] = 1.0e9f; // range.y
        var fogBytes = MemoryMarshal.AsBytes(fogValues.AsSpan()).ToArray();
        _fog = device.CreateBufferInit(new BufferInitDescriptor { Usage = BufferUsage.Uniform | BufferUsage.CopyDst }, fogBytes.AsSpan());

        var lightValues = new float[16];
        lightValues[0] = 1f; // skyFactor
        lightValues[1] = 0f; // blockFactor
        lightValues[2] = 0f; // brightnessFactor
        lightValues[4] = 1f; // skyLightColor r (struct after four floats)
        lightValues[5] = 1f;
        lightValues[6] = 1f;
        lightValues[7] = 1f;
        var lightBytes = MemoryMarshal.AsBytes(lightValues.AsSpan()).ToArray();
        _lightmap = device.CreateBufferInit(new BufferInitDescriptor { Usage = BufferUsage.Uniform | BufferUsage.CopyDst }, lightBytes.AsSpan());

        BindGroup = device.CreateBindGroup(
            bindGroupLayout,
            [
                BindGroupEntry.TextureView(Shaders.ShaderBindings.AtlasTexture, _alphaView),
                BindGroupEntry.Sampler(Shaders.ShaderBindings.AtlasSampler, _sampler),
                BindGroupEntry.Buffer(Shaders.ShaderBindings.Camera, cameraBuffer, 0, cameraSize),
                BindGroupEntry.Buffer(Shaders.ShaderBindings.Fog, _fog, 0, Shaders.ShaderBindings.FogBytes),
                BindGroupEntry.Buffer(Shaders.ShaderBindings.Lightmap, _lightmap, 0, Shaders.ShaderBindings.LightmapBytes)
            ]);

        _vertex = device.CreateBuffer(new BufferDescriptor { Size = 4096, Usage = BufferUsage.Vertex | BufferUsage.CopyDst });
        _index = device.CreateBuffer(new BufferDescriptor { Size = 4096, Usage = BufferUsage.Index | BufferUsage.CopyDst });
        _vertexCapacity = 4096;
        _indexCapacity = 4096;
    }

    public BindGroup BindGroup { get; }

    /// <summary>Uploads the cloud mesh (7-float position-colour vertices) as 14-float terrain vertices.</summary>
    public void Update(float[] vertices, uint[] indices, Matrix4x4 model, Vector4 tint)
    {
        var count = vertices.Length / 7;
        var packed = new float[count * 14];
        for (var i = 0; i < count; i++)
        {
            var s = i * 7;
            var d = i * 14;
            var position = Vector3.Transform(new Vector3(vertices[s], vertices[s + 1], vertices[s + 2]), model);
            packed[d] = position.X;
            packed[d + 1] = position.Y;
            packed[d + 2] = position.Z;
            packed[d + 3] = vertices[s + 3] * tint.X;
            packed[d + 4] = vertices[s + 4] * tint.Y;
            packed[d + 5] = vertices[s + 5] * tint.Z;
            var alpha = Math.Clamp(vertices[s + 6] * tint.W, 0f, 1f);
            // atlas uv.x selects the alpha ramp texel; uv.y stays in the middle of the 1px row.
            packed[d + 6] = ((alpha * 255f) + 0.5f) / 256f;
            packed[d + 7] = 0.5f;
            packed[d + 8] = 0f;  // spriteRect.xy
            packed[d + 9] = 0f;
            packed[d + 10] = 1f; // spriteRect.zw
            packed[d + 11] = 1f;
            packed[d + 12] = 1f; // light (sky, block) -> white lightmap
            packed[d + 13] = 1f;
        }

        var vertexBytes = (ulong)(packed.Length * sizeof(float));
        if (vertexBytes > _vertexCapacity)
        {
            _vertex.Dispose();
            _vertexCapacity = Math.Max(vertexBytes * 2, 4096);
            _vertex = _device.CreateBuffer(new BufferDescriptor { Size = _vertexCapacity, Usage = BufferUsage.Vertex | BufferUsage.CopyDst });
        }

        var indexBytes = (ulong)(indices.Length * sizeof(uint));
        if (indexBytes > _indexCapacity)
        {
            _index.Dispose();
            _indexCapacity = Math.Max(indexBytes * 2, 4096);
            _index = _device.CreateBuffer(new BufferDescriptor { Size = _indexCapacity, Usage = BufferUsage.Index | BufferUsage.CopyDst });
        }

        _device.Queue.WriteBuffer(_vertex, 0, MemoryMarshal.AsBytes(packed.AsSpan()));
        _device.Queue.WriteBuffer(_index, 0, MemoryMarshal.AsBytes(indices.AsSpan()));
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
        _fog.Dispose();
        _lightmap.Dispose();
        _sampler.Dispose();
        _alphaView.Dispose();
        _alphaTexture.Dispose();
    }
}
