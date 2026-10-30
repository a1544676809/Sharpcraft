struct Transform {
    matrix: mat4x4<f32>,
};

@group(0) @binding(0) var sprite: texture_2d<f32>;
@group(0) @binding(1) var spriteSampler: sampler;
@group(0) @binding(2) var<uniform> transform: Transform;

struct VertexInput {
    @location(0) position: vec2<f32>,
    @location(1) colour: vec4<f32>,
    @location(2) uv: vec2<f32>,
};

struct VertexOutput {
    @builtin(position) position: vec4<f32>,
    @location(0) colour: vec4<f32>,
    @location(1) uv: vec2<f32>,
};

@vertex
fn vs_main(input: VertexInput) -> VertexOutput {
    var output: VertexOutput;
    output.position = transform.matrix * vec4<f32>(input.position, 0.0, 1.0);
    output.colour = input.colour;
    output.uv = input.uv;
    return output;
}

@fragment
fn fs_main(input: VertexOutput) -> @location(0) vec4<f32> {
    // Sprites carry their own pixels, unlike glyph sheets where only the coverage matters.
    let sampled = textureSample(sprite, spriteSampler, input.uv);
    return sampled * input.colour;
}
