struct Camera {
    viewProjection : mat4x4<f32>,
};
@group(0) @binding(0) var<uniform> camera : Camera;

struct CelestialTransform {
    model : mat4x4<f32>,
    tint : vec4<f32>,
};
@group(0) @binding(1) var<uniform> transform : CelestialTransform;

@group(0) @binding(2) var celestialTexture : texture_2d<f32>;
@group(0) @binding(3) var celestialSampler : sampler;

struct VertexOutput {
    @builtin(position) clipPosition : vec4<f32>,
    @location(0) uv : vec2<f32>,
};

@vertex
fn vs_celestial(@location(0) position : vec3<f32>, @location(1) uv : vec2<f32>) -> VertexOutput {
    var output : VertexOutput;
    output.clipPosition = camera.viewProjection * transform.model * vec4<f32>(position, 1.0);
    output.uv = uv;
    return output;
}

@fragment
fn fs_celestial(input : VertexOutput) -> @location(0) vec4<f32> {
    // Celestial textures are sRGB-ish art; sample and apply the tint (rain/star brightness in alpha).
    let sampled = textureSample(celestialTexture, celestialSampler, input.uv);
    return vec4<f32>(sampled.rgb * transform.tint.rgb, sampled.a * transform.tint.a);
}
