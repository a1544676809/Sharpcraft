// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

// Presents the frame target on the swapchain. The windowed path renders every world and interface pass into an
// intermediate colour texture (docs/specs/render/screenshot.md 2.1) so the presented image and an F2 screenshot are the
// same pixels, and so the copy the screenshot needs is legal (swapchain textures are not guaranteed copyable).

struct VertexOutput {
    @builtin(position) position : vec4<f32>,
    @location(0) uv : vec2<f32>,
};

@group(0) @binding(0) var colorTexture : texture_2d<f32>;
@group(0) @binding(1) var colorSampler : sampler;

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
fn fs_blit(input : VertexOutput) -> @location(0) vec4<f32> {
    return textureSample(colorTexture, colorSampler, input.uv);
}
