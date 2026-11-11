// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

struct Camera {
    viewProjection : mat4x4<f32>,
    // The same view with the camera at the origin: the disc geometry lives on a dome around the origin, so the camera
    // offset it used to add by hand is exactly what this matrix drops (T-816).
    skyViewProjection : mat4x4<f32>,
};
struct Sky {
    skyColor : vec4<f32>,
    fogColor : vec4<f32>,
    params : vec4<f32>, // x = FogSkyEnd
};
@group(0) @binding(0) var<uniform> camera : Camera;
@group(0) @binding(1) var<uniform> sky : Sky;

struct VertexOutput {
    @builtin(position) position : vec4<f32>,
    @location(0) sphericalDistance : f32,
    @location(1) cylindricalDistance : f32,
};

@vertex
fn vs_main(@location(0) position : vec3<f32>) -> VertexOutput {
    var output : VertexOutput;
    output.position = camera.skyViewProjection * vec4<f32>(position, 1.0);
    output.sphericalDistance = length(position);
    output.cylindricalDistance = max(length(position.xz), abs(position.y));
    return output;
}

fn linearFog(distance : f32, start : f32, end : f32) -> f32 {
    if (distance <= start) { return 0.0; }
    if (distance >= end) { return 1.0; }
    return (distance - start) / (end - start);
}

@fragment
fn fs_main(input : VertexOutput) -> @location(0) vec4<f32> {
    let skyFogEnd = sky.params.x;
    let fogValue = max(
        linearFog(input.sphericalDistance, 0.0, skyFogEnd),
        linearFog(input.cylindricalDistance, skyFogEnd, skyFogEnd));
    return vec4<f32>(mix(sky.skyColor.rgb, sky.fogColor.rgb, fogValue), 1.0);
}
