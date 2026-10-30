@group(0) @binding(0) var atlasTexture : texture_2d<f32>;
@group(0) @binding(1) var atlasSampler : sampler;

struct Camera {
    viewProjection : mat4x4<f32>,
};
@group(0) @binding(2) var<uniform> camera : Camera;

struct Fog {
    colour : vec4<f32>,
    range : vec4<f32>,
};
@group(0) @binding(3) var<uniform> fog : Fog;

struct VertexOutput {
    @builtin(position) clipPosition : vec4<f32>,
    @location(0) colour : vec3<f32>,
    @location(1) uv : vec2<f32>,
    @location(2) spriteRect : vec4<f32>,
    @location(3) fogDistance : f32,
};

@vertex
fn vs_main(
    @location(0) position : vec3<f32>,
    @location(1) colour : vec3<f32>,
    @location(2) uv : vec2<f32>,
    @location(3) spriteRect : vec4<f32>,
) -> VertexOutput {
    var output : VertexOutput;

    // Vertices stay in world space; the camera moves by updating this uniform, so a moving camera
    // never forces the mesh to be re-transformed and re-uploaded.
    let clip = camera.viewProjection * vec4<f32>(position, 1.0);
    output.clipPosition = clip;
    output.fogDistance = clip.w;
    output.colour = colour;
    output.uv = uv;
    output.spriteRect = spriteRect;
    return output;
}

@fragment
fn fs_main(input : VertexOutput) -> @location(0) vec4<f32> {
    // The sprite rectangle runs from the centre of the first texel to the centre of the last, so
    // interpolating across it directly spans one texel less than the sprite: a sixteen pixel tile
    // covers fifteen, and the half texel lost at each edge shows the neighbouring sprite along block
    // borders. Recover the texel count from the rectangle, then sample texel centres, which keeps the
    // whole tile and never steps outside it (render.md 6.1,
    // docs/specs/network/play-movement.md 7.22).
    let atlasSize = vec2<f32>(textureDimensions(atlasTexture));
    let rectMin = input.spriteRect.xy;
    let rectMax = input.spriteRect.zw;

    // Centre-to-centre, so the number of texels is the span plus one.
    let spriteTexels = max(round((rectMax - rectMin) * atlasSize) + vec2<f32>(1.0), vec2<f32>(1.0));

    let repeated = input.uv - floor(input.uv);
    let texel = clamp(floor(repeated * spriteTexels), vec2<f32>(0.0), spriteTexels - vec2<f32>(1.0));
    let atlasUv = rectMin + texel / atlasSize;

    let sampled = textureSample(atlasTexture, atlasSampler, atlasUv);
    let base = vec4<f32>(sampled.rgb * input.colour, 1.0);
    let span = max(fog.range.y - fog.range.x, 0.001);
    let fogFactor = clamp((input.fogDistance - fog.range.x) / span, 0.0, 1.0);
    return vec4<f32>(mix(base.rgb, fog.colour.rgb, fogFactor), base.a);
}

// Translucent pass: the same colour as the opaque one, with an alpha below one so the layer behind shows through.
// The alpha is a placeholder until the reference water alpha is confirmed from assets (docs/specs/render/pipeline.md 15).
@fragment
fn fs_translucent(input : VertexOutput) -> @location(0) vec4<f32> {
    let atlasSize = vec2<f32>(textureDimensions(atlasTexture));
    let rectMin = input.spriteRect.xy;
    let rectMax = input.spriteRect.zw;
    let spriteTexels = max(round((rectMax - rectMin) * atlasSize) + vec2<f32>(1.0), vec2<f32>(1.0));
    let repeated = input.uv - floor(input.uv);
    let texel = clamp(floor(repeated * spriteTexels), vec2<f32>(0.0), spriteTexels - vec2<f32>(1.0));
    let atlasUv = rectMin + texel / atlasSize;

    let sampled = textureSample(atlasTexture, atlasSampler, atlasUv);
    let base = vec4<f32>(sampled.rgb * input.colour, 1.0);
    let span = max(fog.range.y - fog.range.x, 0.001);
    let fogged = clamp((input.fogDistance - fog.range.x) / span, 0.0, 1.0);
    return vec4<f32>(mix(base.rgb, fog.colour.rgb, fogged), 0.7);
}

// Cutout pass: identical colour, but texels below half alpha are discarded instead of blended, which is what leaves and
// plants need (ref: ChunkSectionLayer.CUTOUT uses alpha testing, not blending).
@fragment
fn fs_cutout(input : VertexOutput) -> @location(0) vec4<f32> {
    let atlasSize = vec2<f32>(textureDimensions(atlasTexture));
    let rectMin = input.spriteRect.xy;
    let rectMax = input.spriteRect.zw;
    let spriteTexels = max(round((rectMax - rectMin) * atlasSize) + vec2<f32>(1.0), vec2<f32>(1.0));
    let repeated = input.uv - floor(input.uv);
    let texel = clamp(floor(repeated * spriteTexels), vec2<f32>(0.0), spriteTexels - vec2<f32>(1.0));
    let atlasUv = rectMin + texel / atlasSize;

    let sampled = textureSample(atlasTexture, atlasSampler, atlasUv);

    if (sampled.a < 0.5) {
        discard;
    }

    let base = vec4<f32>(sampled.rgb * input.colour, 1.0);
    let span = max(fog.range.y - fog.range.x, 0.001);
    let fogged = clamp((input.fogDistance - fog.range.x) / span, 0.0, 1.0);
    return vec4<f32>(mix(base.rgb, fog.colour.rgb, fogged), 1.0);
}
