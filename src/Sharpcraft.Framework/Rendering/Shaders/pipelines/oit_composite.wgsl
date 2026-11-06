// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

// Reference-transparency composite (docs/specs/render/oit-reference.md 3.4). Full screen pass that turns the accumulate
// and wavelet-coefficient targets into one premultiplied colour with a coverage alpha, blended over the frame, and
// writes the nearest translucent device depth so later geometry is occluded correctly. The transmittance evaluation
// matches Sharpcraft.Framework.Rendering.OitWavelet.

const OIT_COEFF_COUNT : i32 = 8;
const OIT_WAVELET_RANK : i32 = 2;
const OIT_DEPTH_BINS : i32 = 8;
const OIT_FULLY_OPAQUE_TOTAL_TRANSMITTANCE : f32 = 0.02;

struct FullscreenOutput {
    @builtin(position) position : vec4<f32>,
    @location(0) uv : vec2<f32>,
};

@group(0) @binding(0) var accumulateTexture : texture_2d<f32>;
@group(0) @binding(1) var coeff0Texture : texture_2d<f32>;
@group(0) @binding(2) var coeff1Texture : texture_2d<f32>;
@group(0) @binding(3) var depthBoundsTexture : texture_2d<f32>;

fn toAbsorbance(transmittance : f32) -> f32 {
    return clamp(-log(max(transmittance, 0.0001)), 0.0, 4.0);
}

fn toTransmittance(absorbance : f32) -> f32 {
    return clamp(exp(-absorbance), 0.0001, 1.0);
}

fn sampleAbsorbance(coefficients : array<f32, 8>, originalDepth : f32, currentAbsorbance : f32) -> f32 {
    var averageAbsorbance = coefficients[0];

    if (averageAbsorbance == 0.0) {
        return 0.0;
    }

    let depthMeasuredInBins = originalDepth * f32(OIT_DEPTH_BINS - 1);
    let depth = depthMeasuredInBins / f32(OIT_DEPTH_BINS);
    averageAbsorbance = averageAbsorbance - currentAbsorbance * (1.0 - depth);

    var treeIndexB = clamp(i32(floor(depthMeasuredInBins)), 0, OIT_DEPTH_BINS - 1);
    let shouldSampleA = treeIndexB >= 1;
    var treeIndexA = select(treeIndexB, treeIndexB - 1, shouldSampleA);
    treeIndexB = treeIndexB + OIT_COEFF_COUNT;
    treeIndexA = treeIndexA + OIT_COEFF_COUNT;

    var sampleB = averageAbsorbance;
    var sampleA = select(0.0, averageAbsorbance, shouldSampleA);

    for (var waveletLevel = 0; waveletLevel <= OIT_WAVELET_RANK; waveletLevel = waveletLevel + 1) {
        let power = OIT_WAVELET_RANK - waveletLevel;
        let waveletWidth = exp2(-f32(power));
        let waveletHeightScale = exp2(f32(power) * 0.5);

        let parentIndexB = treeIndexB >> 1;
        let isRightHalfB = treeIndexB & 1;
        let waveletSignB = 1 - (isRightHalfB << 1);
        let indexAtParentLevel = parentIndexB & ((1i << u32(power)) - 1);
        let depthOffset = depth - waveletWidth * f32(indexAtParentLevel);
        let currentDifference = (f32(isRightHalfB) * waveletWidth + f32(waveletSignB) * depthOffset)
            * waveletHeightScale * currentAbsorbance;

        let differenceCoefficientB = coefficients[parentIndexB] - currentDifference;
        sampleB = sampleB - waveletHeightScale * differenceCoefficientB * f32(waveletSignB);
        treeIndexB = parentIndexB;

        if (shouldSampleA) {
            let parentIndexA = treeIndexA >> 1;
            var differenceCoefficientA = 0.0;
            if (parentIndexA == parentIndexB) {
                differenceCoefficientA = differenceCoefficientB;
            } else {
                differenceCoefficientA = coefficients[parentIndexA];
            }
            let isRightHalfA = treeIndexA & 1;
            let waveletSignA = 1 - (isRightHalfA << 1);
            sampleA = sampleA - waveletHeightScale * differenceCoefficientA * f32(waveletSignA);
            treeIndexA = parentIndexA;
        }
    }

    let lerpAlpha = select(fract(depthMeasuredInBins), 1.0, depthMeasuredInBins >= f32(OIT_DEPTH_BINS));
    return mix(sampleA, sampleB, lerpAlpha);
}

fn sampleTransmittance(coefficients : array<f32, 8>, originalDepth : f32, currentTransmittance : f32) -> f32 {
    return toTransmittance(sampleAbsorbance(coefficients, originalDepth, toAbsorbance(currentTransmittance)));
}

fn loadCoefficients(pixel : vec2<i32>) -> array<f32, 8> {
    let c0 = textureLoad(coeff0Texture, pixel, 0);
    let c1 = textureLoad(coeff1Texture, pixel, 0);
    var coefficients : array<f32, 8>;
    coefficients[0] = c0.x;
    coefficients[1] = c0.y;
    coefficients[2] = c0.z;
    coefficients[3] = c0.w;
    coefficients[4] = c1.x;
    coefficients[5] = c1.y;
    coefficients[6] = c1.z;
    coefficients[7] = c1.w;
    return coefficients;
}

struct CompositeOutput {
    @location(0) colour : vec4<f32>,
    @builtin(frag_depth) depth : f32,
};

@vertex
fn vs_fullscreen(@builtin(vertex_index) index : u32) -> FullscreenOutput {
    var positions = array<vec2<f32>, 3>(
        vec2<f32>(-1.0, -3.0),
        vec2<f32>(-1.0, 1.0),
        vec2<f32>(3.0, 1.0));

    let position = positions[index];

    var output : FullscreenOutput;
    output.position = vec4<f32>(position, 0.0, 1.0);
    output.uv = vec2<f32>((position.x + 1.0) * 0.5, (1.0 - position.y) * 0.5);
    return output;
}

@fragment
fn fs_composite(input : FullscreenOutput) -> CompositeOutput {
    let pixel = vec2<i32>(input.position.xy);
    let accumulated = textureLoad(accumulateTexture, pixel, 0);
    let coefficients = loadCoefficients(pixel);

    // Total transmittance of the whole layer, sampled at the far end of the range.
    let totalTransmittance = sampleTransmittance(coefficients, 100000.0, 1.0);
    let coverage = 1.0 - select(totalTransmittance, 0.0, totalTransmittance < OIT_FULLY_OPAQUE_TOTAL_TRANSMITTANCE);

    var output : CompositeOutput;

    // Pixels with neither coverage nor additive light contribute nothing.
    if (coverage < 0.00001 && (accumulated.r + accumulated.g + accumulated.b) < 0.00001) {
        discard;
    }

    // accum.rgb is colour*alpha*transmittance; dividing by accum.a gives the average colour, and multiplying by the
    // coverage premultiplies it for the One / OneMinusSrcAlpha blend onto the frame.
    let normalization = select(1.0, coverage / accumulated.a, accumulated.a > 0.00001);
    output.colour = vec4<f32>(accumulated.rgb * normalization, coverage);

    // b stores the closest device depth directly; the scene is reversed-Z so the nearest surface is the LARGEST device
    // depth, which the depth-bounds stage's MAX blend keeps.
    output.depth = textureLoad(depthBoundsTexture, pixel, 0).b;
    return output;
}
