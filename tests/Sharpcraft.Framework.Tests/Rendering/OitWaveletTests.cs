// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Framework.Rendering;
using Xunit;

namespace Sharpcraft.Framework.Tests.Rendering;

/// <summary>
/// Pins the reference-transparency transmittance reference (docs/specs/render/oit-reference.md): the eight-coefficient
/// wavelet encoding must reproduce the analytic total transmittance of a set of translucent layers, independent of the
/// order they are encoded in. If the GPU shader later diverges from this CPU reference, these invariants say what broke.
/// </summary>
public sealed class OitWaveletTests
{
    private static float[] Encode(params (float Alpha, float Depth)[] layers)
    {
        var coefficients = new float[OitWavelet.CoefficientCount];

        foreach (var (alpha, depth) in layers)
        {
            OitWavelet.Accumulate(coefficients, OitWavelet.ToAbsorbance(1f - alpha), depth);
        }

        return coefficients;
    }

    [Fact]
    public void AbsorbanceAndTransmittanceRoundTrip()
    {
        foreach (var transmittance in new[] { 0.05f, 0.25f, 0.5f, 0.8f, 1.0f })
        {
            var recovered = OitWavelet.ToTransmittance(OitWavelet.ToAbsorbance(transmittance));
            Assert.Equal(transmittance, recovered, 4);
        }
    }

    [Fact]
    public void OneLayerResolvesToItsOwnCoverage()
    {
        foreach (var alpha in new[] { 0.1f, 0.3f, 0.5f, 0.9f })
        {
            foreach (var depth in new[] { 0.05f, 0.2f, 0.5f, 0.8f, 0.95f })
            {
                var coverage = OitWavelet.Coverage(Encode((alpha, depth)));
                Assert.True(
                    MathF.Abs(coverage - alpha) < 0.02f,
                    $"alpha={alpha} depth={depth} coverage={coverage}");
            }
        }
    }

    [Fact]
    public void TwoLayersResolveToTheProductOfTheirTransmittances()
    {
        const float firstAlpha = 0.5f;
        const float secondAlpha = 0.4f;
        var expected = 1f - (1f - firstAlpha) * (1f - secondAlpha);

        foreach (var (near, far) in new[] { (0.2f, 0.7f), (0.7f, 0.2f), (0.4f, 0.45f) })
        {
            var coverage = OitWavelet.Coverage(Encode((firstAlpha, near), (secondAlpha, far)));
            Assert.True(
                MathF.Abs(coverage - expected) < 0.03f,
                $"near={near} far={far} coverage={coverage} expected={expected}");
        }
    }

    [Fact]
    public void EncodingOrderDoesNotChangeTheCoefficients()
    {
        var forward = Encode((0.5f, 0.2f), (0.3f, 0.6f), (0.8f, 0.9f));
        var backward = Encode((0.8f, 0.9f), (0.3f, 0.6f), (0.5f, 0.2f));

        Assert.Equal(forward, backward);
    }

    [Fact]
    public void EmptyCoefficientsCarryNoAbsorbance()
    {
        var coefficients = new float[OitWavelet.CoefficientCount];

        Assert.Equal(0f, OitWavelet.SampleAbsorbance(coefficients, 0.5f, 0f));
        // Nothing encoded means nothing occludes: coverage is zero, not one.
        Assert.Equal(0f, OitWavelet.Coverage(coefficients), 5);
    }
}
