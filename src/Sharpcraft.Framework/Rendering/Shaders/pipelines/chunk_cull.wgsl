// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

// Frustum cull for the chunk draw list. One invocation per draw slot: if the chunk's world bounds fall entirely
// outside the view, the slot's index count is zeroed so the indirect draw becomes a no op. The bounds test matches
// the CPU reference (FrustumPlanes.IsVisible) corner for corner, so the two can be compared directly.

struct Bounds
{
    minimum : vec4<f32>,
    maximum : vec4<f32>,
};

struct DrawArgs
{
    indexCount : u32,
    instanceCount : u32,
    firstIndex : u32,
    baseVertex : i32,
    firstInstance : u32,
};

struct Planes
{
    plane : array<vec4<f32>, 6>,
};

@group(0) @binding(0) var<storage, read> bounds : array<Bounds>;
@group(0) @binding(1) var<storage, read> base : array<DrawArgs>;
@group(0) @binding(2) var<storage, read_write> args : array<DrawArgs>;
@group(0) @binding(3) var<uniform> planes : Planes;

@compute @workgroup_size(64)
fn cs_main(@builtin(global_invocation_id) invocation : vec3<u32>)
{
    let index = invocation.x;

    if (index >= arrayLength(&bounds))
    {
        return;
    }

    let box = bounds[index];
    var visible = true;

    for (var planeIndex : u32 = 0u; planeIndex < 6u; planeIndex = planeIndex + 1u)
    {
        let plane = planes.plane[planeIndex];

        // The corner furthest along the plane normal decides, exactly as the CPU test does.
        let corner = vec3<f32>(
            select(box.minimum.x, box.maximum.x, plane.x >= 0.0),
            select(box.minimum.y, box.maximum.y, plane.y >= 0.0),
            select(box.minimum.z, box.maximum.z, plane.z >= 0.0));

        if (dot(plane.xyz, corner) + plane.w < 0.0)
        {
            visible = false;
            break;
        }
    }

    var record = base[index];

    if (!visible)
    {
        record.indexCount = 0u;
    }

    args[index] = record;
}
