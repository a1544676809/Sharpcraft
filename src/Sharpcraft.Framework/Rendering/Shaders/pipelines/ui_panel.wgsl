struct Transform {
    matrix: mat4x4<f32>,
};

@group(0) @binding(2) var<uniform> transform: Transform;

struct VertexInput {
    @location(0) position: vec2<f32>,
    @location(1) colour: vec4<f32>,
};

struct VertexOutput {
    @builtin(position) position: vec4<f32>,
    @location(0) colour: vec4<f32>,
};

@vertex
fn vs_main(input: VertexInput) -> VertexOutput {
    var output: VertexOutput;
    // Positions are already two dimensional; depth is fixed at zero because nothing in this layer occludes.
    output.position = transform.matrix * vec4<f32>(input.position, 0.0, 1.0);
    output.colour = input.colour;
    return output;
}

@fragment
fn fs_main(input: VertexOutput) -> @location(0) vec4<f32> {
    return input.colour;
}
