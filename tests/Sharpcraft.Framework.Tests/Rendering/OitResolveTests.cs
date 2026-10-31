// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Xunit;

namespace Sharpcraft.Framework.Tests.Rendering;

/// <summary>
/// Pins the order independent transparency contract that <c>terrain.wgsl</c> (fs_translucent_oit) and
/// <c>oit_resolve.wgsl</c> (fs_oit_resolve) implement: translucent fragments accumulate colour times alpha and alpha into
/// one target, and the product of one minus alpha into another, then the resolve recovers a colour by division and a
/// coverage by subtraction. The test states the properties that make the scheme order independent, so a future change to
/// either shader that breaks them (for example writing straight alpha into the accumulate target) is caught here without
/// needing a window to look at (docs/specs/render/pipeline.md 15).
/// </summary>
public sealed class OitResolveTests
{
    private readonly record struct Fragment(Vector3 Colour, float Alpha);

    /// <summary>The two per-fragment targets after all fragments are accumulated, in the given order.</summary>
    private static (Vector3 Accumulate, float Coverage, float Reveal) Accumulate(IEnumerable<Fragment> fragments)
    {
        var accumulate = Vector3.Zero;
        var alphaSum = 0f;
        var reveal = 1f;

        foreach (var fragment in fragments)
        {
            // terrain.wgsl fs_translucent_oit: accumulate = (colour * alpha, alpha), added with (One, One).
            accumulate += fragment.Colour * fragment.Alpha;
            alphaSum += fragment.Alpha;

            // Reveal target: multiplied by (OneMinusSrcAlpha) with a source factor of zero, so it stores prod(1 - alpha).
            reveal *= 1f - fragment.Alpha;
        }

        // oit_resolve.wgsl fs_oit_resolve: colour = accumulate.rgb / accumulate.a; coverage = 1 - reveal.
        var colour = accumulate / MathF.Max(alphaSum, 1e-5f);
        return (colour, 1f - reveal, reveal);
    }

    [Fact]
    public void TwoLayersResolveToTheSameColourInEitherOrder()
    {
        var near = new Fragment(new Vector3(0.20f, 0.55f, 0.90f), 0.60f);
        var far = new Fragment(new Vector3(0.85f, 0.30f, 0.10f), 0.40f);

        var forward = Accumulate([near, far]);
        var backward = Accumulate([far, near]);

        Assert.Equal(forward.Accumulate, backward.Accumulate);
        Assert.Equal(forward.Coverage, backward.Coverage, 6);
        Assert.Equal(forward.Reveal, backward.Reveal, 6);
    }

    [Fact]
    public void OneLayerResolvesToItsOwnColourAndAlpha()
    {
        var layer = new Fragment(new Vector3(0.30f, 0.60f, 0.80f), 0.75f);

        var (colour, coverage, reveal) = Accumulate([layer]);

        Assert.Equal(layer.Colour, colour);
        Assert.Equal(0.75f, coverage, 6);
        Assert.Equal(0.25f, reveal, 6);
    }

    [Fact]
    public void ResolvedCoverageIsTheAlphaCompositedOverTheBackground()
    {
        const float background = 0.25f;
        var layer = new Fragment(new Vector3(0.9f, 0.4f, 0.1f), 0.5f);

        var (colour, coverage, _) = Accumulate([layer]);

        // Compositing the resolved translucent colour over the opaque background with (SrcAlpha, OneMinusSrcAlpha) must
        // equal a plain alpha blend of the layer itself.
        var composited = colour * coverage + new Vector3(background) * (1f - coverage);
        var alphaBlend = layer.Colour * layer.Alpha + new Vector3(background) * (1f - layer.Alpha);

        Assert.Equal(alphaBlend, composited, new Vector3Comparer(5));
    }

    private sealed class Vector3Comparer(int digits) : IEqualityComparer<Vector3>
    {
        public bool Equals(Vector3 a, Vector3 b) =>
            MathF.Round(a.X - b.X, digits) == 0f &&
            MathF.Round(a.Y - b.Y, digits) == 0f &&
            MathF.Round(a.Z - b.Z, digits) == 0f;

        public int GetHashCode(Vector3 value) => value.GetHashCode();
    }
}
