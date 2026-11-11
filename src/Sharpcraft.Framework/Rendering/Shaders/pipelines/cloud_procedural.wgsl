// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

// Procedural cloud geometry (T-827 S3).
//
// The reference sends no cloud vertices. It sends three integers per face and the vertex shader turns a vertex index
// into a corner of a cell; this shader does the same. Twelve bytes per face against the hundred and twelve the
// explicit geometry path uploads today. Behaviour is written out in docs/specs/render/cloud-geometry.md and the
// decoding below is the one tests/Sharpcraft.Client.Tests/Render/CloudProceduralGeometryTests.cs already checks
// against the current builder, corner for corner, so a difference seen later is shading rather than geometry.
//
// NOT WIRED YET. Nothing creates a pipeline from this file, and WGSL is not compiled at build time, so it is
// unvalidated until the first pipeline build succeeds. Wiring needs two new bindings on group 0: the cloud info
// uniform below and the face table storage buffer, both of which the cloud resource setup has to create.

struct CloudInfo {
    // Tint applied to every face, from the cloud colour timeline and the weather.
    colour : vec4<f32>,
    // Where the cloud plane sits: the reference keeps the plane at a fixed height and moves only horizontally.
    offset : vec3<f32>,
    // Size of one cloud cell across, and the thickness of the slab it makes.
    cellSize : vec2<f32>,
    thickness : f32,
};

@group(0) @binding(0) var<uniform> camera : Camera;
@group(0) @binding(1) var<uniform> transform : Transform;
@group(0) @binding(2) var<uniform> clouds : CloudInfo;

// Three words per face: cell X halved, cell Z halved, then the direction and its flags.
@group(0) @binding(3) var<storage, read> faces : array<i32>;

const DIRECTION_MASK : i32 = 7;
const FLAG_INSIDE_FACE : i32 = 16;
const FLAG_EXTRA_Z : i32 = 64;
const FLAG_EXTRA_X : i32 = 128;

// The six faces of a cell, four corners each, in the order the face table uses: down, up, north, south, west, east.
// Packing these into three constants is possible and the reference does it; the list is left readable because it is
// the thing the spec and the test state, and the shader is not on a budget where twenty four vec3 constants matter.
const CORNERS = array<vec3<f32>, 24>(
    vec3<f32>(1.0, 0.0, 0.0), vec3<f32>(1.0, 0.0, 1.0), vec3<f32>(0.0, 0.0, 1.0), vec3<f32>(0.0, 0.0, 0.0),
    vec3<f32>(0.0, 1.0, 0.0), vec3<f32>(0.0, 1.0, 1.0), vec3<f32>(1.0, 1.0, 1.0), vec3<f32>(1.0, 1.0, 0.0),
    vec3<f32>(0.0, 0.0, 0.0), vec3<f32>(0.0, 1.0, 0.0), vec3<f32>(1.0, 1.0, 0.0), vec3<f32>(1.0, 0.0, 0.0),
    vec3<f32>(1.0, 0.0, 1.0), vec3<f32>(1.0, 1.0, 1.0), vec3<f32>(0.0, 1.0, 1.0), vec3<f32>(0.0, 0.0, 1.0),
    vec3<f32>(0.0, 0.0, 1.0), vec3<f32>(0.0, 1.0, 1.0), vec3<f32>(0.0, 1.0, 0.0), vec3<f32>(0.0, 0.0, 0.0),
    vec3<f32>(1.0, 0.0, 0.0), vec3<f32>(1.0, 1.0, 0.0), vec3<f32>(1.0, 1.0, 1.0), vec3<f32>(1.0, 0.0, 1.0),
);

// Brightness per direction, matching the existing cloud geometry so the rewrite is a movement of code rather than a
// change of look. The reference can additionally force a face to the top brightness; that flag is never set yet and
// is registered as a simplification in the spec.
const BRIGHTNESS = array<f32, 6>(0.7, 1.0, 0.8, 0.8, 0.9, 0.9);

struct VertexOutput {
    @builtin(position) clipPosition : vec4<f32>,
    @location(0) colour : vec3<f32>,
    @location(1) alpha : f32,
    @location(2) fogDistance : f32,
};

@vertex
fn vs_main(@builtin(vertex_index) vertexIndex : u32) -> VertexOutput {
    let cornerInQuad = i32(vertexIndex % 4u);
    let word = i32(vertexIndex / 4u) * 3;

    let flags = faces[word + 2];

    // The coordinate was halved when the table was built and its low bit carried in the flags; put it back.
    let cellX = (faces[word] << 1) | ((flags & FLAG_EXTRA_X) >> 7);
    let cellZ = (faces[word + 1] << 1) | ((flags & FLAG_EXTRA_Z) >> 6);

    let direction = flags & DIRECTION_MASK;
    let insideFace = (flags & FLAG_INSIDE_FACE) != 0;

    // Inside faces run the other way round so their winding still faces the camera.
    let cornerOrdinal = select(cornerInQuad, 3 - cornerInQuad, insideFace);
    let corner = CORNERS[direction * 4 + cornerOrdinal];

    let local = vec3<f32>(
        corner.x * clouds.cellSize.x,
        corner.y * clouds.thickness,
        corner.z * clouds.cellSize.y,
    );

    let inCell = vec3<f32>(f32(cellX) * clouds.cellSize.x, 0.0, f32(cellZ) * clouds.cellSize.y);
    let world = transform.model * vec4<f32>(local + inCell + clouds.offset, 1.0);

    let clip = camera.viewProjection * world;

    var output : VertexOutput;
    output.clipPosition = clip;
    output.colour = vec3<f32>(BRIGHTNESS[direction]) * clouds.colour.rgb * transform.tint.rgb;
    output.alpha = clouds.colour.a * transform.tint.a;

    // The camera uniform carries only the view projection, so fog takes the clip w the existing cloud shader uses.
    output.fogDistance = clip.w;
    return output;
}
