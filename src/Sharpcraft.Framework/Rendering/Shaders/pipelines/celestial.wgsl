struct Camera {
    viewProjection : mat4x4<f32>,
    // The same view with the camera at the origin. The bodies are built on a dome around the origin, so this matrix is
    // all they need — the reference sky shaders use a rotation-only ModelViewMat for exactly this reason (T-811, T-812).
    skyViewProjection : mat4x4<f32>,
};
@group(0) @binding(0) var<uniform> camera : Camera;

@group(0) @binding(1) var celestialTexture : texture_2d<f32>;
@group(0) @binding(2) var celestialSampler : sampler;

struct VertexOutput {
    @builtin(position) clipPosition : vec4<f32>,
    @location(0) uv : vec2<f32>,
    @location(1) tint : vec4<f32>,
};

@vertex
fn vs_celestial(
    @location(0) position : vec3<f32>,
    @location(1) uv : vec2<f32>,
    @location(2) tint : vec4<f32>) -> VertexOutput {
    var output : VertexOutput;
    output.clipPosition = camera.skyViewProjection * vec4<f32>(position, 1.0);
    output.uv = uv;
    output.tint = tint;
    return output;
}

@fragment
fn fs_celestial(input : VertexOutput) -> @location(0) vec4<f32> {
    // Celestial textures are sRGB-ish art; sample and apply the per-vertex tint (rain/star brightness in alpha).
    let sampled = textureSample(celestialTexture, celestialSampler, input.uv);
    return vec4<f32>(sampled.rgb * input.tint.rgb, sampled.a * input.tint.a);
}
