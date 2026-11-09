// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Brew.Wgpu;
using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Framework.Tests.Rendering;

/// <summary>
/// S1 of T-784: the sampler table keys every address/filter/mipmap combination and hands back the same object.
/// </summary>
/// <remarks>
/// The factory is injected so the keying can be tested without a GPU device; the device path differs only in how each
/// sampler is created.
/// </remarks>
public sealed class SamplerCacheTests
{
    [Fact]
    public void EveryCombinationIsBuiltOnceAndReused()
    {
        var cache = new SamplerCache();
        var created = 0;
        cache.Initialize((_, _, _, _, _) =>
        {
            created++;
            return default;
        });

        // Two address modes, two filters on each axis and the mipmap flag: 2 * 2 * 2 * 2 * 2 = 32 slots, matching the
        // reference cache size.
        Assert.Equal(32, created);

        var first = cache.GetSampler(AddressMode.Repeat, AddressMode.ClampToEdge, FilterMode.Nearest, FilterMode.Linear, useMipmaps: true);
        var second = cache.GetSampler(AddressMode.Repeat, AddressMode.ClampToEdge, FilterMode.Nearest, FilterMode.Linear, useMipmaps: true);
        Assert.Equal(first, second);
        Assert.Equal(32, created);
    }

    [Fact]
    public void DifferentStatesLandInDifferentSlots()
    {
        var cache = new SamplerCache();
        cache.Initialize((_, _, _, _, _) => default);

        var repeat = SamplerCache.Encode(AddressMode.Repeat, AddressMode.Repeat, FilterMode.Nearest, FilterMode.Nearest, useMipmaps: false);
        var clamp = SamplerCache.Encode(AddressMode.ClampToEdge, AddressMode.Repeat, FilterMode.Nearest, FilterMode.Nearest, useMipmaps: false);
        var linear = SamplerCache.Encode(AddressMode.Repeat, AddressMode.Repeat, FilterMode.Linear, FilterMode.Nearest, useMipmaps: false);
        var mipmapped = SamplerCache.Encode(AddressMode.Repeat, AddressMode.Repeat, FilterMode.Nearest, FilterMode.Nearest, useMipmaps: true);

        var slots = new[] { repeat, clamp, linear, mipmapped };
        Assert.Equal(4, slots.Distinct().Count());
        Assert.All(slots, slot => Assert.InRange(slot, 0, 31));
    }

    [Fact]
    public void TheConvenienceHelpersPickTheMatchingAddressMode()
    {
        var cache = new SamplerCache();
        var seen = new List<(AddressMode U, AddressMode V, FilterMode Min, FilterMode Mag, bool Mipmaps)>();
        cache.Initialize((u, v, min, mag, mipmaps) =>
        {
            seen.Add((u, v, min, mag, mipmaps));
            return default;
        });

        _ = cache.GetRepeat(FilterMode.Nearest);
        _ = cache.GetClampToEdge(FilterMode.Linear, mipmaps: true);

        Assert.Contains((AddressMode.Repeat, AddressMode.Repeat, FilterMode.Nearest, FilterMode.Nearest, false), seen);
        Assert.Contains((AddressMode.ClampToEdge, AddressMode.ClampToEdge, FilterMode.Linear, FilterMode.Linear, true), seen);
    }

    [Fact]
    public void SamplingBeforeInitializeFailsLoudly()
    {
        var cache = new SamplerCache();

        // The reference throws when a texture is read before something initialised it; the same fail-fast contract keeps
        // a missing sampler from turning into a silent black texture.
        Assert.Throws<InvalidOperationException>(() =>
            cache.GetSampler(AddressMode.Repeat, AddressMode.Repeat, FilterMode.Nearest, FilterMode.Nearest, useMipmaps: false));
    }
}
