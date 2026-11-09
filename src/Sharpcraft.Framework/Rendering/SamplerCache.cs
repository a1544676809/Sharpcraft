// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Brew.Wgpu;
using Brew.Wgpu.Native;

namespace Sharpcraft.Framework.Rendering;

/// <summary>How a sampler treats texture coordinates outside [0, 1].</summary>
/// <remarks>
/// Mirrors the reference <c>AddressMode</c>, which has exactly these two values; the reference sampler cache throws when
/// a third appears, and <see cref="SamplerCache"/> keeps that constraint.
/// </remarks>
public enum AddressMode
{
    /// <summary>Coordinates repeat, which the reference uses as its default.</summary>
    Repeat,

    /// <summary>Coordinates stick to the edge texel; the reference switches to this for textures that ask for it.</summary>
    ClampToEdge
}

/// <summary>How a sampler interpolates between texels.</summary>
/// <remarks>Mirrors the reference <c>FilterMode</c>, which has exactly two values.</remarks>
public enum FilterMode
{
    /// <summary>Nearest texel, the reference default.</summary>
    Nearest,

    /// <summary>Linear blend between texels, used when a texture asks to be blurred.</summary>
    Linear
}

/// <summary>
/// One sampler per address/filter/mipmap combination, built once and shared.
/// </summary>
/// <remarks>
/// The reference builds every combination up front (<c>SamplerCache</c> holds a 32-entry array) so a texture switch is a
/// lookup rather than a device call, and so the same state always resolves to the same sampler object. That matters for
/// software quality as much as speed: this repository previously created samplers ad hoc, which is why a texture could
/// not carry its own address mode and UV overflow had to be patched geometrically (docs/specs/debug-control.md,
/// docs/analysis/architecture/texture-sampler-pipeline-design.md).
/// </remarks>
public sealed class SamplerCache : IDisposable
{
    private readonly Sampler?[] _samplers = new Sampler?[32];
    private bool _disposed;

    /// <summary>Builds every combination for <paramref name="device"/>.</summary>
    /// <param name="device">Device the samplers belong to.</param>
    public void Initialize(Device device)
    {
        ArgumentNullException.ThrowIfNull(device);
        Initialize((addressModeU, addressModeV, minFilter, magFilter, useMipmaps) => device.CreateSampler(new SamplerDescriptor
        {
            AddressModeU = ToWgpu(addressModeU),
            AddressModeV = ToWgpu(addressModeV),
            AddressModeW = ToWgpu(addressModeU),
            MagFilter = ToWgpu(magFilter),
            MinFilter = ToWgpu(minFilter),
            MipmapFilter = useMipmaps ? WGPUMipmapFilterMode.Linear : WGPUMipmapFilterMode.Nearest,
            LodMinClamp = 0f,
            LodMaxClamp = useMipmaps ? 32f : 0f,
            MaxAnisotropy = 1
        }));
    }

    /// <summary>Builds every combination through a caller-supplied factory; used by tests.</summary>
    /// <param name="create">Factory invoked once per combination.</param>
    internal void Initialize(Func<AddressMode, AddressMode, FilterMode, FilterMode, bool, Sampler> create)
    {
        ArgumentNullException.ThrowIfNull(create);

        foreach (var addressModeU in Enum.GetValues<AddressMode>())
        {
            foreach (var addressModeV in Enum.GetValues<AddressMode>())
            {
                foreach (var minFilter in Enum.GetValues<FilterMode>())
                {
                    foreach (var magFilter in Enum.GetValues<FilterMode>())
                    {
                        foreach (var useMipmaps in new[] { true, false })
                        {
                            _samplers[Encode(addressModeU, addressModeV, minFilter, magFilter, useMipmaps)] =
                                create(addressModeU, addressModeV, minFilter, magFilter, useMipmaps);
                        }
                    }
                }
            }
        }
    }

    /// <summary>Returns the sampler for the given state.</summary>
    public Sampler GetSampler(AddressMode addressModeU, AddressMode addressModeV, FilterMode minFilter, FilterMode magFilter, bool useMipmaps) =>
        _samplers[Encode(addressModeU, addressModeV, minFilter, magFilter, useMipmaps)]
        ?? throw new InvalidOperationException("SamplerCache.Initialize must be called before sampling");

    /// <summary>Returns a clamping sampler with the same filter for minification and magnification.</summary>
    public Sampler GetClampToEdge(FilterMode minMag) => GetClampToEdge(minMag, mipmaps: false);

    /// <summary>Returns a clamping sampler with an explicit mipmap choice.</summary>
    public Sampler GetClampToEdge(FilterMode minMag, bool mipmaps) =>
        GetSampler(AddressMode.ClampToEdge, AddressMode.ClampToEdge, minMag, minMag, mipmaps);

    /// <summary>Returns a repeating sampler with the same filter for minification and magnification.</summary>
    public Sampler GetRepeat(FilterMode minMag) => GetRepeat(minMag, mipmaps: false);

    /// <summary>Returns a repeating sampler with an explicit mipmap choice.</summary>
    public Sampler GetRepeat(FilterMode minMag, bool mipmaps) =>
        GetSampler(AddressMode.Repeat, AddressMode.Repeat, minMag, minMag, mipmaps);

    /// <summary>Encodes one state into the cache slot, matching the reference's five-part key.</summary>
    internal static int Encode(AddressMode addressModeU, AddressMode addressModeV, FilterMode minFilter, FilterMode magFilter, bool useMipmaps)
    {
        var value = ((int)addressModeU & 1)
            | (((int)addressModeV & 1) << 1)
            | (((int)minFilter & 1) << 2)
            | (((int)magFilter & 1) << 3)
            | ((useMipmaps ? 1 : 0) << 4);
        return value & 31;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var sampler in _samplers)
        {
            sampler?.Dispose();
        }

        Array.Clear(_samplers);
    }

    private static WGPUAddressMode ToWgpu(AddressMode mode) =>
        mode == AddressMode.ClampToEdge ? WGPUAddressMode.ClampToEdge : WGPUAddressMode.Repeat;

    private static WGPUFilterMode ToWgpu(FilterMode mode) =>
        mode == FilterMode.Linear ? WGPUFilterMode.Linear : WGPUFilterMode.Nearest;
}
