// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

struct Camera {
    viewProjection : mat4x4<f32>,
};
@group(0) @binding(0) var<uniform> camera : Camera;
@group(0) @binding(1) var glyphTexture : texture_2d<f32>;
@group(0) @binding(2) var glyphSampler : sampler;

struct VertexInput {
    @location(0) position : vec3<f32>,
    @location(1) uv : vec2<f32>,
    @location(2) colour : vec4<f32>,
};

struct VertexOutput {
    @builtin(position) position : vec4<f32>,
    @location(0) uv : vec2<f32>,
    @location(1) colour : vec4<f32>,
};

@vertex
fn vs_main(input : VertexInput) -> VertexOutput {
    var output : VertexOutput;
    output.position = camera.viewProjection * vec4<f32>(input.position, 1.0);
    output.uv = input.uv;
    output.colour = input.colour;
    return output;
}

@fragment
fn fs_main(input : VertexOutput) -> @location(0) vec4<f32> {
    if (input.uv.x < 0.0) {
        return input.colour;
    }

    return textureSample(glyphTexture, glyphSampler, input.uv) * input.colour;
}
