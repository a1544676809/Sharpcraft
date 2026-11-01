// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>
/// The reference's block-light flicker: a small random walk added to the lightmap block factor each tick, so torches,
/// lava and other block-light sources shimmer subtly (docs/analysis/render/lighting-color-space.md #8).
/// </summary>
/// <remarks>
/// Reference <c>LightmapRenderStateExtractor.tick</c>:
/// <c>flicker += (r1 - r2) * r3 * r4 * 0.1F; flicker *= 0.9F;</c> and <c>blockFactor = flicker + 1.4F</c>. The random
/// draws are not reproduced bit-for-bit (the reference seeds its own {@code RandomSource}); the distribution and the
/// decay are what the result needs, which is what this port matches. <see cref="Func{TResult}"/> is injectable so the
/// arithmetic can be tested deterministically.
/// </remarks>
public sealed class BlockLightFlicker
{
    private readonly Func<float> _nextFloat;
    private float _value;

    /// <summary>Creates a flicker driven by <paramref name="random"/> (or <see cref="Random.Shared"/> when null).</summary>
    public BlockLightFlicker(Random? random = null)
        : this((random ?? Random.Shared).NextSingle)
    {
    }

    /// <summary>Creates a flicker from a <c>[0,1)</c> float source (tests pass a scripted sequence).</summary>
    public BlockLightFlicker(Func<float> nextFloat)
    {
        _nextFloat = nextFloat ?? throw new ArgumentNullException(nameof(nextFloat));
    }

    /// <summary>Current flicker offset (around zero, decaying).</summary>
    public float Value => _value;

    /// <summary>The lightmap block factor, <c>1.4 + flicker</c> like the reference.</summary>
    public float BlockFactor => 1.4f + _value;

    /// <summary>Advances one tick: one random-walk step then decay by 0.9.</summary>
    public void Tick()
    {
        _value += (_nextFloat() - _nextFloat()) * _nextFloat() * _nextFloat() * 0.1f;
        _value *= 0.9f;
    }
}
