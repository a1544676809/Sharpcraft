// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

// One level of the occlusion pyramid: each output texel keeps the farthest of its four children.
// Farthest, not nearest, because the covering test must stay conservative: a region that is partly nearer reports itself
// nearer and the box in front of it is kept rather than dropped.

struct Params {
    width : u32,
    height : u32,
    // Rows of the source are padded for the texture to buffer copy, so the shader must skip the padding.
    stride : u32,
    pad0 : u32,
    near : f32,
    far : f32,
    pad1 : vec2<u32>,
};

@group(0) @binding(4) var<storage, read> source : array<f32>;
@group(0) @binding(5) var<storage, read_write> destination : array<f32>;
@group(0) @binding(6) var<uniform> params : Params;

@compute @workgroup_size(8, 8)
fn cs_downsample(@builtin(global_invocation_id) id : vec3<u32>) {
    let outWidth = max(params.width / 2u, 1u);
    let outHeight = max(params.height / 2u, 1u);

    if (id.x >= outWidth || id.y >= outHeight) {
        return;
    }

    var farthest = 0.0;
    for (var dy = 0u; dy < 2u; dy = dy + 1u) {
        for (var dx = 0u; dx < 2u; dx = dx + 1u) {
            let sourceX = min(params.width - 1u, id.x * 2u + dx);
            let sourceY = min(params.height - 1u, id.y * 2u + dy);
                        // The pyramid is defined over distances, not window depth, which saturates in the far field.
            let window = source[sourceY * params.stride + sourceX];
            let distance = select(
                params.near * params.far / (params.far - window * (params.far - params.near)),
                params.far,
                window >= 1.0);
            farthest = max(farthest, distance);
        }
    }

    destination[id.y * outWidth + id.x] = farthest;
}
