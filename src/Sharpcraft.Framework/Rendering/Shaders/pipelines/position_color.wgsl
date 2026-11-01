struct Camera {
    viewProjection : mat4x4<f32>,
};
@group(0) @binding(0) var<uniform> camera : Camera;

struct Transform {
    model : mat4x4<f32>,
    tint : vec4<f32>,
};
@group(0) @binding(1) var<uniform> transform : Transform;

struct VertexOutput {
    @builtin(position) clipPosition : vec4<f32>,
    @location(0) colour : vec4<f32>,
};

// Port of the reference core/position_color.vsh: clip = ProjMat * ModelViewMat * Position, and the
// per-vertex colour goes straight through.
@vertex
fn vs_position_color(@location(0) position : vec3<f32>, @location(1) colour : vec4<f32>) -> VertexOutput {
    var output : VertexOutput;
    output.clipPosition = camera.viewProjection * transform.model * vec4<f32>(position, 1.0);
    output.colour = colour;
    return output;
}

// Port of core/position_color.fsh: drop fully transparent fragments, then multiply by the colour modulator
// (the per-draw tint, e.g. the sunrise/sunset colour or the star brightness).
@fragment
fn fs_position_color(input : VertexOutput) -> @location(0) vec4<f32> {
    if (input.colour.a == 0.0) {
        discard;
    }
    return input.colour * transform.tint;
}
