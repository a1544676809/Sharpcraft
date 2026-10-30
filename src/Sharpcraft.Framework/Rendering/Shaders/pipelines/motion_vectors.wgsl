// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

// Motion vectors: for every pixel, where it was last frame, in pixels.
// The world point is rebuilt from the pixel and its depth, then projected with the previous camera; the difference is the
// displacement a temporal method reprojects with. Sky pixels (depth at the far plane) report no motion.

struct Params {
    width : u32,
    height : u32,
    stride : u32,
    pad0 : u32,
};

struct Matrices {
    inverseViewProjection : mat4x4<f32>,
    previousViewProjection : mat4x4<f32>,
};

@group(0) @binding(4) var<storage, read> source : array<f32>;
@group(0) @binding(5) var<storage, read_write> motion : array<vec2<f32>>;
@group(0) @binding(6) var<uniform> params : Params;
@group(0) @binding(7) var<uniform> matrices : Matrices;

@compute @workgroup_size(8, 8)
fn cs_motion(@builtin(global_invocation_id) id : vec3<u32>) {
    if (id.x >= params.width || id.y >= params.height) {
        return;
    }

    let index = id.y * params.width + id.x;
    let window = source[id.y * params.stride + id.x];

    if (window >= 1.0) {
        motion[index] = vec2<f32>(0.0, 0.0);
        return;
    }

    let uv = vec2<f32>(
        (f32(id.x) + 0.5) / f32(params.width),
        (f32(id.y) + 0.5) / f32(params.height));

    let clip = vec4<f32>((uv.x * 2.0) - 1.0, 1.0 - (uv.y * 2.0), window, 1.0);
    let world4 = matrices.inverseViewProjection * clip;
    let world = world4.xyz / world4.w;

    let previous = matrices.previousViewProjection * vec4<f32>(world, 1.0);

    if (previous.w <= 0.0) {
        motion[index] = vec2<f32>(0.0, 0.0);
        return;
    }

    let previousUv = vec2<f32>(
        ((previous.x / previous.w) * 0.5) + 0.5,
        0.5 - ((previous.y / previous.w) * 0.5));

    motion[index] = (previousUv - uv) * vec2<f32>(f32(params.width), f32(params.height));
}
