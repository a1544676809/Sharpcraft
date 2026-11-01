// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

// Copies the main opaque depth into the cloud depth attachment. This mirrors the reference
// BLIT_DEPTH_DURING_DEPTH_BOUNDS shader: a fullscreen triangle samples the main depth and writes it as gl_FragDepth.

struct VertexOutput {
    @builtin(position) position : vec4<f32>,
    @location(0) uv : vec2<f32>,
};

@group(0) @binding(0) var depthTexture : texture_depth_2d;
@group(0) @binding(1) var depthSampler : sampler;

@vertex
fn vs_fullscreen(@builtin(vertex_index) index : u32) -> VertexOutput {
    var positions = array<vec2<f32>, 3>(
        vec2<f32>(-1.0, -3.0),
        vec2<f32>(-1.0, 1.0),
        vec2<f32>(3.0, 1.0));
    let position = positions[index];
    var output : VertexOutput;
    output.position = vec4<f32>(position, 0.0, 1.0);
    output.uv = vec2<f32>((position.x + 1.0) * 0.5, (1.0 - position.y) * 0.5);
    return output;
}

@fragment
fn fs_blit(input : VertexOutput) -> @builtin(frag_depth) f32 {
    return textureSample(depthTexture, depthSampler, input.uv);
}
