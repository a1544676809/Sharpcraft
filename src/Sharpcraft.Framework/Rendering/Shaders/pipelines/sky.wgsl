struct SkyOutput {
    @builtin(position) clipPosition : vec4<f32>,
    @location(0) height : f32,
};

@vertex
fn vs_sky(@builtin(vertex_index) index : u32) -> SkyOutput {
    // One oversized triangle covering the screen, so no vertex buffer is needed.
    var positions = array<vec2<f32>, 3>(
        vec2<f32>(-1.0, -3.0),
        vec2<f32>(-1.0, 1.0),
        vec2<f32>(3.0, 1.0));

    var output : SkyOutput;
    let position = positions[index];
    output.clipPosition = vec4<f32>(position, 0.0, 1.0);
    // 0 at the bottom of the screen, 1 at the top.
    output.height = (position.y + 1.0) * 0.5;
    return output;
}

@fragment
fn fs_sky(input : SkyOutput) -> @location(0) vec4<f32> {
    let horizon = vec3<f32>(0.75, 0.85, 1.0);
    let zenith = vec3<f32>(0.47, 0.65, 1.0);
    return vec4<f32>(mix(horizon, zenith, input.height), 1.0);
}
