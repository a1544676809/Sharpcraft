// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

// No [ReferenceClass] annotation: Sharpcraft.Framework must not reference any Sharpcraft.* assembly
// (tests/Sharpcraft.Architecture.Tests/AssemblyBoundaryTests.cs), so the attribute in Sharpcraft.Common.Reference cannot
// resolve here, and tools/Sharpcraft.ClassMap does not scan this project.

using Brew.Wgpu;
using Brew.Wgpu.Native;

namespace Sharpcraft.Framework.Rendering;

/// <summary>An opaque texture owned by a render backend.</summary>
/// <remarks>
/// Handles carry no usable surface: a caller can pass one back to the backend that created it and nothing else. That is
/// what lets a second backend exist later without every call site changing.
/// </remarks>
public readonly struct TextureHandle
{
    internal TextureHandle(object value) => Value = value;

    internal object Value { get; }
}

/// <summary>An opaque texture view owned by a render backend.</summary>
public readonly struct TextureViewHandle
{
    internal TextureViewHandle(object value) => Value = value;

    internal object Value { get; }
}

/// <summary>An opaque buffer owned by a render backend.</summary>
public readonly struct BufferHandle
{
    internal BufferHandle(object value) => Value = value;

    internal object Value { get; }
}

/// <summary>An opaque sampler owned by a render backend.</summary>
public readonly struct SamplerHandle
{
    internal SamplerHandle(object value) => Value = value;

    internal object Value { get; }
}

/// <summary>An opaque render pipeline owned by a render backend.</summary>
public readonly struct PipelineHandle
{
    internal PipelineHandle(object value) => Value = value;

    internal object Value { get; }
}

/// <summary>How a texture is created.</summary>
/// <param name="Width">Width in texels.</param>
/// <param name="Height">Height in texels.</param>
/// <param name="Format">Colour or depth format.</param>
/// <param name="MipLevelCount">Levels including level zero.</param>
/// <param name="Usage">What the texture may be used for.</param>
public readonly record struct TextureDesc(uint Width, uint Height, TextureFormat Format, uint MipLevelCount, TextureUsages Usage);

/// <summary>What a texture may be used for.</summary>
[Flags]
public enum TextureUsages
{
    /// <summary>Nothing yet; a texture must declare at least one use.</summary>
    None = 0,

    /// <summary>Rendered into as a colour or depth attachment.</summary>
    RenderAttachment = 1,

    /// <summary>Sampled in a shader.</summary>
    TextureBinding = 2,

    /// <summary>Source of a copy.</summary>
    CopySrc = 4,

    /// <summary>Destination of a copy or upload.</summary>
    CopyDst = 8
}

/// <summary>
/// The seam a rendering backend implements.
/// </summary>
/// <remarks>
/// Principle 6 asks for every graphics call to converge in one abstraction layer so the backend can be replaced; this is
/// that layer's contract (docs/analysis/architecture/render-backend-seam.md). The interface deliberately covers only what
/// this renderer actually uses: copying wgpu's whole model would make a second backend write a wrapper rather than an
/// implementation. Nothing here may name a backend type.
/// </remarks>
public interface IRenderBackend
{
    /// <summary>Backend identifier, used in diagnostics.</summary>
    string Name { get; }

    /// <summary>Creates a texture.</summary>
    TextureHandle CreateTexture(in TextureDesc desc);

    /// <summary>Uploads pixels into one mip level.</summary>
    void WriteTexture(TextureHandle texture, ReadOnlySpan<byte> pixels, uint bytesPerRow, uint width, uint height, uint mipLevel);

    /// <summary>Creates a view over the whole texture.</summary>
    TextureViewHandle CreateTextureView(TextureHandle texture);

    /// <summary>Creates a sampler from the state the caller asked for.</summary>
    SamplerHandle CreateSampler(AddressMode addressMode, FilterMode minFilter, FilterMode magFilter, bool useMipmaps);

    /// <summary>Destroys a texture.</summary>
    void Destroy(TextureHandle texture);

    /// <summary>Destroys a texture view.</summary>
    void Destroy(TextureViewHandle view);

    /// <summary>Destroys a sampler.</summary>
    void Destroy(SamplerHandle sampler);
}

/// <summary>
/// The wgpu implementation of <see cref="IRenderBackend"/>.
/// </summary>
/// <remarks>
/// This type is the only place where the seam meets the wgpu bindings, so a second backend would sit beside it rather
/// than replacing it.
/// </remarks>
public sealed class WgpuRenderBackend : IRenderBackend
{
    private readonly Device _device;

    /// <summary>Wraps an existing device.</summary>
    /// <param name="device">Device that owns every resource this backend creates.</param>
    public WgpuRenderBackend(Device device)
    {
        ArgumentNullException.ThrowIfNull(device);
        _device = device;
    }

    /// <inheritdoc/>
    public string Name => "wgpu";

    /// <summary>The wrapped device, for the code that has not moved behind the seam yet.</summary>
    public Device Device => _device;

    /// <inheritdoc/>
    public TextureHandle CreateTexture(in TextureDesc desc) =>
        new(_device.CreateTexture(new TextureDescriptor
        {
            Size = new WGPUExtent3D { width = desc.Width, height = desc.Height, depthOrArrayLayers = 1 },
            MipLevelCount = desc.MipLevelCount,
            SampleCount = 1,
            Dimension = WGPUTextureDimension._2D,
            Format = BackendConvert.ToWgpu(desc.Format),
            Usage = ToWgpu(desc.Usage)
        }));

    /// <inheritdoc/>
    public void WriteTexture(TextureHandle texture, ReadOnlySpan<byte> pixels, uint bytesPerRow, uint width, uint height, uint mipLevel)
    {
        var target = (Texture)texture.Value;

        // The extent is stated by the caller rather than derived from the row pitch: four bytes per texel only holds for
        // the 8-bit RGBA formats, and deriving it would write a quarter of a half-float texture.
        _device.Queue.WriteTexture(
            target,
            pixels.ToArray(),
            bytesPerRow,
            height,
            new WGPUExtent3D { width = width, height = height, depthOrArrayLayers = 1 },
            mipLevel,
            new WGPUOrigin3D { x = 0, y = 0, z = 0 });
    }

    /// <summary>Unwraps a view handle for code that has not moved behind the seam yet.</summary>
    /// <remarks>
    /// The seam forbids backend types in its own signatures, so this lives on the implementation and disappears with the
    /// device escape hatch once every consumer is wired.
    /// </remarks>
    /// <param name="view">Handle from this backend.</param>
    internal TextureView AsTextureView(TextureViewHandle view) => (TextureView)view.Value;

    /// <inheritdoc/>
    public TextureViewHandle CreateTextureView(TextureHandle texture) => new(((Texture)texture.Value).CreateView());

    /// <inheritdoc/>
    public SamplerHandle CreateSampler(AddressMode addressMode, FilterMode minFilter, FilterMode magFilter, bool useMipmaps) =>
        new(SamplerCache.Create(_device, addressMode, minFilter, magFilter, useMipmaps));

    /// <inheritdoc/>
    public void Destroy(TextureHandle texture) => ((Texture)texture.Value).Dispose();

    /// <inheritdoc/>
    public void Destroy(TextureViewHandle view) => ((TextureView)view.Value).Dispose();

    /// <inheritdoc/>
    public void Destroy(SamplerHandle sampler) => ((Sampler)sampler.Value).Dispose();

    private static Brew.Wgpu.TextureUsage ToWgpu(TextureUsages usage)
    {
        var result = (Brew.Wgpu.TextureUsage)0;
        if (usage.HasFlag(TextureUsages.RenderAttachment))
        {
            result |= Brew.Wgpu.TextureUsage.RenderAttachment;
        }

        if (usage.HasFlag(TextureUsages.TextureBinding))
        {
            result |= Brew.Wgpu.TextureUsage.TextureBinding;
        }

        if (usage.HasFlag(TextureUsages.CopySrc))
        {
            result |= Brew.Wgpu.TextureUsage.CopySrc;
        }

        if (usage.HasFlag(TextureUsages.CopyDst))
        {
            result |= Brew.Wgpu.TextureUsage.CopyDst;
        }

        return result;
    }
}
