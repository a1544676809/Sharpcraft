// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

// One level of the occlusion pyramid: each output texel keeps the farthest of its four children.
// Farthest, not nearest, because the covering test must stay conservative: a region that is partly nearer reports itself
// nearer and the box in front of it is kept rather than dropped.

struct Params {
    width : u32,
    height : u32,
    pad0 : u32,
    pad1 : u32,
};

@group(0) @binding(4) var<storage, read> source : array<f32>;
@group(0) @binding(5) var<storage, read_write> target : array<f32>;
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
            farthest = max(farthest, source[sourceY * params.width + sourceX]);
        }
    }

    target[id.y * outWidth + id.x] = farthest;
}
