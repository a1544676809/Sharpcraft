// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Framework.Rendering;

/// <summary>
/// CPU reference of the reference-transparency transmittance encoding (docs/specs/render/oit-reference.md).
/// </summary>
/// <remarks>
/// The reference pipeline compresses the accumulated absorbance of translucent layers along the view ray into eight
/// wavelet coefficients (rank two), which lets the accumulate pass weight each layer by the transmittance of everything
/// in front of it without sorting. This type reimplements that encoding and decoding so the GPU shader can be checked
/// against a shared ground truth, and so the scheme's observable invariants (order independence, total transmittance
/// being the product of one minus alpha) are pinned by tests without a window. Behaviour mirrors
/// ref://mc-26.3-analysis/src/assets/minecraft/shaders/include/oit_add_transmittance.glsl and oit_sample.glsl; it is an
/// independent implementation, not a translation.
/// </remarks>
public static class OitWavelet
{
    /// <summary>Number of wavelet coefficients (and depth bins).</summary>
    public const int CoefficientCount = 8;

    /// <summary>Wavelet tree rank: with rank two, one + two + four coefficients describe 2, 4 and 8 depth bins.</summary>
    public const int WaveletRank = 2;

    /// <summary>Depth bins the range is split into; the reference uses one bin per coefficient.</summary>
    public const int DepthBins = CoefficientCount;

    /// <summary>Transmittance below this counts as fully opaque when resolving coverage.</summary>
    public const float FullyOpaqueTotalTransmittance = 0.02f;

    /// <summary>Converts transmittance to absorbance, clamped to the range the coefficients can hold.</summary>
    public static float ToAbsorbance(float transmittance) =>
        Math.Clamp(-MathF.Log(MathF.Max(transmittance, 0.0001f)), 0f, 4f);

    /// <summary>Converts absorbance back to transmittance.</summary>
    public static float ToTransmittance(float absorbance) =>
        Math.Clamp(MathF.Exp(-absorbance), 0.0001f, 1f);

    /// <summary>
    /// Adds one translucent layer's absorbance at the given normalized depth to the coefficient set. Because the target
    /// blend is additive, calling this in any order gives the same coefficients.
    /// </summary>
    /// <param name="coefficients">Coefficient set, length <see cref="CoefficientCount"/>.</param>
    /// <param name="absorbance">Absorbance of this layer (<see cref="ToAbsorbance"/> of one minus its alpha).</param>
    /// <param name="originalDepth">Layer depth normalized into the depth bounds, in [0, 1].</param>
    public static void Accumulate(Span<float> coefficients, float absorbance, float originalDepth)
    {
        var depth = originalDepth * (DepthBins - 1) / DepthBins;

        // The zeroth coefficient is the average absorbance across the whole range.
        coefficients[0] += absorbance * (1f - depth);

        // The rest form a binary tree of difference coefficients, one level per rank step, that record where the
        // absorbance sits so any depth can be reconstructed.
        var depthBinIndex = (int)MathF.Floor(originalDepth * (DepthBins - 1));
        var treeIndex = depthBinIndex + CoefficientCount;

        for (var waveletLevel = 0; waveletLevel <= WaveletRank; waveletLevel++)
        {
            var power = WaveletRank - waveletLevel;
            var parentIndex = treeIndex >> 1;
            var indexAtParentLevel = parentIndex & ((1 << power) - 1);
            var isRightHalf = treeIndex & 1;
            var waveletSign = 1 - (isRightHalf << 1);
            var waveletWidth = MathF.Pow(2f, -power);
            var waveletHeightScale = MathF.Pow(2f, power * 0.5f);
            var depthOffset = depth - waveletWidth * indexAtParentLevel;
            var distanceFromEdge = isRightHalf * waveletWidth + waveletSign * depthOffset;

            coefficients[parentIndex] += distanceFromEdge * waveletHeightScale * absorbance;
            treeIndex = parentIndex;
        }
    }

    /// <summary>
    /// Evaluates the encoded absorbance at the given normalized depth, as seen through <paramref name="currentAbsorbance"/>
    /// that sits in front of it.
    /// </summary>
    public static float SampleAbsorbance(ReadOnlySpan<float> coefficients, float originalDepth, float currentAbsorbance)
    {
        var averageAbsorbance = coefficients[0];

        if (averageAbsorbance == 0f)
        {
            return 0f;
        }

        var depthMeasuredInBins = originalDepth * (DepthBins - 1);
        var depth = depthMeasuredInBins / DepthBins;

        averageAbsorbance -= currentAbsorbance * (1f - depth);

        var treeIndexB = Math.Clamp((int)MathF.Floor(depthMeasuredInBins), 0, DepthBins - 1);
        var shouldSampleA = treeIndexB >= 1;
        var treeIndexA = shouldSampleA ? treeIndexB - 1 : treeIndexB;

        treeIndexB += CoefficientCount;
        treeIndexA += CoefficientCount;

        var sampleB = averageAbsorbance;
        var sampleA = shouldSampleA ? averageAbsorbance : 0f;

        for (var waveletLevel = 0; waveletLevel <= WaveletRank; waveletLevel++)
        {
            var power = WaveletRank - waveletLevel;
            var waveletWidth = MathF.Pow(2f, -power);
            var waveletHeightScale = MathF.Pow(2f, power * 0.5f);

            var parentIndexB = treeIndexB >> 1;
            var isRightHalfB = treeIndexB & 1;
            var waveletSignB = 1 - (isRightHalfB << 1);
            var indexAtParentLevel = parentIndexB & ((1 << power) - 1);
            var depthOffset = depth - waveletWidth * indexAtParentLevel;
            var currentDifference = (isRightHalfB * waveletWidth + waveletSignB * depthOffset) * waveletHeightScale * currentAbsorbance;

            var differenceCoefficientB = coefficients[parentIndexB] - currentDifference;
            sampleB -= waveletHeightScale * differenceCoefficientB * waveletSignB;
            treeIndexB = parentIndexB;

            if (shouldSampleA)
            {
                var parentIndexA = treeIndexA >> 1;
                var differenceCoefficientA = parentIndexA == parentIndexB
                    ? differenceCoefficientB
                    : coefficients[parentIndexA];
                var isRightHalfA = treeIndexA & 1;
                var waveletSignA = 1 - (isRightHalfA << 1);

                sampleA -= waveletHeightScale * differenceCoefficientA * waveletSignA;
                treeIndexA = parentIndexA;
            }
        }

        var lerpAlpha = depthMeasuredInBins >= DepthBins
            ? 1f
            : depthMeasuredInBins - MathF.Floor(depthMeasuredInBins);

        return sampleA + (sampleB - sampleA) * lerpAlpha;
    }

    /// <summary>Evaluates the encoded transmittance at a normalized depth through <paramref name="currentTransmittance"/>.</summary>
    public static float SampleTransmittance(ReadOnlySpan<float> coefficients, float originalDepth, float currentTransmittance) =>
        ToTransmittance(SampleAbsorbance(coefficients, originalDepth, ToAbsorbance(currentTransmittance)));

    /// <summary>
    /// The overall coverage the composite pass derives from the coefficients: one minus the total transmittance at the
    /// far end of the range, with a floor of 0.02 so fully opaque stacks resolve to exactly one.
    /// </summary>
    public static float Coverage(ReadOnlySpan<float> coefficients)
    {
        var totalTransmittance = SampleTransmittance(coefficients, 100000f, 1f);
        return 1f - (totalTransmittance < FullyOpaqueTotalTransmittance ? 0f : totalTransmittance);
    }
}
