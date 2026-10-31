// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

// Resolves the two order independent targets into the frame. The accumulate target holds colour times alpha and alpha
// itself; the reveal target holds the product of one minus alpha. Dividing one by the other recovers the blended colour,
// and one minus reveal is the coverage. This is the classic weighted blended scheme: no sorting is needed, which is why
// the reference does not sort translucent terrain either.

struct FullscreenOutput
{
    @builtin(position) position : vec4<f32>,
    @location(0) uv : vec2<f32>,
};

@group(0) @binding(0) var accumulateTexture : texture_2d<f32>;
@group(0) @binding(1) accumulateSampler : sampler;
@group(0) @binding(2) var revealTexture : texture_2d<f32>;

@vertex
fn vs_fullscreen(@builtin(vertex_index) index : u32) -> FullscreenOutput
{
    // One oversized triangle covers the viewport: three vertices, no vertex buffer.
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
fn fs_oit_resolve(input : FullscreenOutput) -> @location(0) vec4<f32>
{
    let accumulated = textureSample(accumulateTexture, accumulateSampler, input.uv);
    let reveal = textureSample(revealTexture, accumulateSampler, input.uv);

    let coverage = 1.0 - reveal.r;
    let colour = accumulated.rgb / max(accumulated.a, 1e-5);

    return vec4<f32>(colour, coverage);
}
