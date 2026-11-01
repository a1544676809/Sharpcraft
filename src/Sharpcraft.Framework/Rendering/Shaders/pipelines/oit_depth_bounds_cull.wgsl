// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

// Reference-transparency depth-bounds cull (docs/specs/render/oit-reference.md 4). Full screen pass that clips the far
// end of the translucent depth range to the nearest opaque surface recorded in the depth-bounds target, so the
// transmittance and accumulate passes never see layers that are actually hidden behind opaque terrain.

struct FullscreenOutput {
    @builtin(position) position : vec4<f32>,
    @location(0) uv : vec2<f32>,
};

@group(0) @binding(0) var depthBoundsTexture : texture_2d<f32>;

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
fn fs_cull(input : FullscreenOutput) -> @location(0) vec4<f32> {
    let bounds = textureLoad(depthBoundsTexture, vec2<i32>(input.position.xy), 0);
    // a stores -closestOpaqueLinear, or a large negative sentinel when nothing in the pixel is fully opaque.
    let closestOpaqueLinear = -bounds.a;
    return vec4<f32>(bounds.r, min(bounds.g, closestOpaqueLinear), bounds.b, bounds.a);
}
