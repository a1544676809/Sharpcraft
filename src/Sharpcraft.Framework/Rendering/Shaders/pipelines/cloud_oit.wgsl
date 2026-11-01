// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

// Cloud reference-OIT shader. The cloud mesh is position(3)+colour(4): colour.rgb carries face brightness, colour.a the
// cloud fog alpha. A transform uniform places the camera-relative mesh and supplies the CLOUD_COLOR tint, so the per-frame
// cost is one small uniform write instead of re-expanding and re-uploading the whole mesh.

struct Camera {
    viewProjection : mat4x4<f32>,
};
struct Transform {
    model : mat4x4<f32>,
    tint : vec4<f32>,
};
@group(0) @binding(0) var<uniform> camera : Camera;
@group(0) @binding(1) var<uniform> transform : Transform;

@group(1) @binding(0) var depthBoundsTexture : texture_2d<f32>;
@group(1) @binding(1) var coeff0Texture : texture_2d<f32>;
@group(1) @binding(2) var coeff1Texture : texture_2d<f32>;

const OIT_COEFF_COUNT : i32 = 8;
const OIT_WAVELET_RANK : i32 = 2;
const OIT_DEPTH_BINS : i32 = 8;
const OIT_HIGH_PRECISION_DEPTH_THRESHOLD : f32 = 10.0;
const OIT_LOW_PRECISION_DEPTH_BINS : i32 = 2;

struct VertexOutput {
    @builtin(position) clipPosition : vec4<f32>,
    @location(0) colour : vec3<f32>,
    @location(1) alpha : f32,
    @location(2) fogDistance : f32,
    @location(3) deviceDepth : f32,
    @location(4) cloudAlpha : f32,
};

@vertex
fn vs_main(
    @location(0) position : vec3<f32>,
    @location(1) colour : vec3<f32>,
    @location(2) uv : vec2<f32>,
    @location(3) spriteRect : vec4<f32>,
    @location(4) light : vec2<f32>,
) -> VertexOutput {
    _ = spriteRect;
    _ = light;
    var output : VertexOutput;
    let world = transform.model * vec4<f32>(position, 1.0);
    let clip = camera.viewProjection * world;
    output.clipPosition = clip;
    output.colour = colour * transform.tint.rgb;
    output.alpha = uv.x;
    output.cloudAlpha = transform.tint.a;
    output.fogDistance = clip.w;
    output.deviceDepth = clip.z / max(clip.w, 0.00001);
    return output;
}

fn toAbsorbance(transmittance : f32) -> f32 {
    return clamp(-log(max(transmittance, 0.0001)), 0.0, 4.0);
}

fn toTransmittance(absorbance : f32) -> f32 {
    return clamp(exp(-absorbance), 0.0001, 1.0);
}

fn normalizeDepth(fragmentLinearDepth : f32, closestLinearDepth : f32, furthestLinearDepth : f32) -> f32 {
    let range = max(furthestLinearDepth - closestLinearDepth, 0.0001);
    let depthWithinBounds = clamp(fragmentLinearDepth - closestLinearDepth, 0.0, range);
    let depthFractionWithHighPrecision = f32(OIT_DEPTH_BINS - OIT_LOW_PRECISION_DEPTH_BINS) / f32(OIT_DEPTH_BINS);
    let depthWithHighPrecision = depthFractionWithHighPrecision * range;

    if (depthWithHighPrecision <= OIT_HIGH_PRECISION_DEPTH_THRESHOLD) {
        return depthWithinBounds / range;
    } else if (depthWithinBounds <= OIT_HIGH_PRECISION_DEPTH_THRESHOLD) {
        return (depthWithinBounds / OIT_HIGH_PRECISION_DEPTH_THRESHOLD) * depthFractionWithHighPrecision;
    }
    return depthFractionWithHighPrecision
        + ((depthWithinBounds - OIT_HIGH_PRECISION_DEPTH_THRESHOLD)
            / (range - OIT_HIGH_PRECISION_DEPTH_THRESHOLD)) * (1.0 - depthFractionWithHighPrecision);
}

fn encodeTransmittance(absorbance : f32, originalDepth : f32) -> array<f32, 8> {
    var coefficients : array<f32, 8>;
    for (var i = 0; i < OIT_COEFF_COUNT; i = i + 1) {
        coefficients[i] = 0.0;
    }

    let depth = originalDepth * f32(OIT_DEPTH_BINS - 1) / f32(OIT_DEPTH_BINS);
    coefficients[0] = absorbance * (1.0 - depth);

    let depthBinIndex = i32(floor(originalDepth * f32(OIT_DEPTH_BINS - 1)));
    var treeIndex = depthBinIndex + OIT_COEFF_COUNT;

    for (var waveletLevel = 0; waveletLevel <= OIT_WAVELET_RANK; waveletLevel = waveletLevel + 1) {
        let power = OIT_WAVELET_RANK - waveletLevel;
        let parentIndex = treeIndex >> 1;
        let indexAtParentLevel = parentIndex & ((1i << u32(power)) - 1);
        let isRightHalf = treeIndex & 1;
        let waveletSign = 1 - (isRightHalf << 1);
        let waveletWidth = exp2(-f32(power));
        let waveletHeightScale = exp2(f32(power) * 0.5);
        let depthOffset = depth - waveletWidth * f32(indexAtParentLevel);
        let distanceFromEdge = f32(isRightHalf) * waveletWidth + f32(waveletSign) * depthOffset;
        coefficients[parentIndex] = distanceFromEdge * waveletHeightScale * absorbance;
        treeIndex = parentIndex;
    }
    return coefficients;
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

struct DepthBoundsOutput {
    @location(0) bounds : vec4<f32>,
};

@fragment
fn fs_depth_bounds(input : VertexOutput) -> DepthBoundsOutput {
    if (input.cloudAlpha < 0.01) {
        discard;
    }
    let linear = input.fogDistance;
    let device = input.deviceDepth;
    let opaqueLinear = select(-65504.0, -linear, input.cloudAlpha > 0.99);
    var output : DepthBoundsOutput;
    output.bounds = vec4<f32>(-linear, linear, -device, opaqueLinear);
    return output;
}

struct TransmittanceOutput {
    @location(0) coeff0 : vec4<f32>,
    @location(1) coeff1 : vec4<f32>,
};

@fragment
fn fs_transmittance(input : VertexOutput) -> TransmittanceOutput {
    let fragmentAlpha = input.cloudAlpha * input.alpha;
    if (fragmentAlpha < 0.1) {
        discard;
    }
    let bounds = textureLoad(depthBoundsTexture, vec2<i32>(input.clipPosition.xy), 0);
    let closestLinear = -bounds.r;
    let furthestLinear = bounds.g;
    let normalizedDepth = normalizeDepth(input.fogDistance, closestLinear, furthestLinear);
    let absorbance = toAbsorbance(1.0 - fragmentAlpha);
    let coefficients = encodeTransmittance(absorbance, normalizedDepth);
    var output : TransmittanceOutput;
    output.coeff0 = vec4<f32>(coefficients[0], coefficients[1], coefficients[2], coefficients[3]);
    output.coeff1 = vec4<f32>(coefficients[4], coefficients[5], coefficients[6], coefficients[7]);
    return output;
}

struct AccumulateOutput {
    @location(0) accum : vec4<f32>,
};

@fragment
fn fs_accumulate(input : VertexOutput) -> AccumulateOutput {
    let fragmentAlpha = input.cloudAlpha * input.alpha;
    if (fragmentAlpha < 0.1) {
        discard;
    }
    let pixel = vec2<i32>(input.clipPosition.xy);
    let bounds = textureLoad(depthBoundsTexture, pixel, 0);
    let closestLinear = -bounds.r;
    let furthestLinear = bounds.g;
    let normalizedDepth = normalizeDepth(input.fogDistance, closestLinear, furthestLinear);
    let coefficients = loadCoefficients(pixel);
    let sampledTransmittance = sampleTransmittance(coefficients, normalizedDepth, 1.0 - fragmentAlpha);
    var output : AccumulateOutput;
    output.accum = vec4<f32>(input.colour * fragmentAlpha, fragmentAlpha) * sampledTransmittance;
    return output;
}
