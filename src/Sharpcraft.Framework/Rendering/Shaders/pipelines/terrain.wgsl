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

// Per-frame lightmap parameters (docs/decisions/0019, docs/specs/render/lighting.md 6). The vertex carries the sky/block
// light levels; the shader turns them into the lightmap multiplier per fragment so day/night needs no re-mesh.
struct Lightmap {
    skyFactor : f32,
    blockFactor : f32,
    brightnessFactor : f32,
    padding : f32,
    skyLightColor : vec4<f32>,
    blockLightTint : vec4<f32>,
    ambientColor : vec4<f32>,
};
@group(0) @binding(4) var<uniform> lightmap : Lightmap;

// Reference block.vsh/block.fsh multiply in plain (gamma/sRGB) space with no decode or encode:
//   vertexColor = Color * sample_lightmap(...);   color = texture * vertexColor * ColorModulator.
// So combine the sampled texel, the vertex Colour (tint x shade x AO) and the lightmap colour by direct multiplication,
// exactly like the reference. lightmapColour already reproduces lightmap.fsh's brightness math 1:1.
fn litColour(texel : vec3<f32>, light : vec3<f32>) -> vec3<f32> {
    return clamp(texel * light, vec3<f32>(0.0), vec3<f32>(1.0));
}

fn brightnessCurve(x : f32) -> f32 { return x / (4.0 - 3.0 * x); }

fn notGamma(c : vec3<f32>) -> vec3<f32> {
    let m = max(c.x, max(c.y, c.z));
    if (m <= 0.0) { return c; }
    let inv = 1.0 - m;
    let scaled = 1.0 - (inv * inv * inv * inv);
    return c * (scaled / m);
}

// Exact port of VanillaLightmap.Colour (docs/specs/render/lighting.md 3): sky/block levels are 0..1 (= level/15).
fn lightmapColour(skyLevel : f32, blockLevel : f32) -> vec3<f32> {
    let blockBrightness = brightnessCurve(blockLevel) * lightmap.blockFactor;
    let skyBrightness = brightnessCurve(skyLevel) * lightmap.skyFactor;

    var colour = lightmap.ambientColor.rgb + lightmap.skyLightColor.rgb * skyBrightness;

    var parabolic = (2.0 * blockLevel) - 1.0;
    parabolic = parabolic * parabolic;
    let blockLightColour = mix(lightmap.blockLightTint.rgb, vec3<f32>(1.0), 0.9 * parabolic);
    colour = colour + blockLightColour * blockBrightness;

    colour = clamp(colour, vec3<f32>(0.0), vec3<f32>(1.0));
    let ng = notGamma(colour);
    colour = mix(colour, ng, clamp(lightmap.brightnessFactor, 0.0, 1.0));
    return clamp(colour, vec3<f32>(0.0), vec3<f32>(1.0));
}

struct VertexOutput {
    @builtin(position) clipPosition : vec4<f32>,
    @location(0) colour : vec3<f32>,
    @location(1) uv : vec2<f32>,
    @location(2) spriteRect : vec4<f32>,
    @location(3) fogDistance : f32,
    @location(4) light : vec2<f32>,
};

@vertex
fn vs_main(
    @location(0) position : vec3<f32>,
    @location(1) colour : vec3<f32>,
    @location(2) uv : vec2<f32>,
    @location(3) spriteRect : vec4<f32>,
    @location(4) light : vec2<f32>,
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
    output.light = light;
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

    // The atlas carries one texel of edge padding around every sprite, so a continuous coordinate inside the rectangle
    // never reads a neighbour: linear filtering and mip selection stay on the sprite, and the hardware picks the level from
    // the fragment footprint.
    let atlasUv = rectMin + (input.uv - floor(input.uv)) * (rectMax - rectMin);
    // A merged quad tiles its sprite several times, so the wrapped coordinate jumps at every block edge and the hardware
    // would choose a different mip level on either side of the jump; that showed as large rectangular patches on flat
    // ice. The gradients come from the continuous (unwrapped) uv, so mip selection stays uniform across the whole quad.
    let gradientScale = rectMax - rectMin;
    let sampled = textureSampleGrad(
        atlasTexture,
        atlasSampler,
        atlasUv,
        dpdx(input.uv) * gradientScale,
        dpdy(input.uv) * gradientScale);

    // Alpha test here rather than in a separate cutout pipeline: leaves and plants then stream with the same per chunk
    // mesh as everything else. It is the same rule the reference applies to cutout terrain (threshold 0.5), just carried by
    // one pipeline instead of two.
    if (sampled.a < 0.5) {
        discard;
    }

    let light = input.colour * lightmapColour(input.light.x, input.light.y);
    let base = vec4<f32>(litColour(sampled.rgb, light), 1.0);
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
    // Continuous mapping into the sprite rectangle: with a mipmapped, linearly filtered atlas this stays smooth when a
    // block covers fewer pixels than its texture has texels. Snapping to one texel per fragment speckled at distance.
    let atlasUv = rectMin + (input.uv - floor(input.uv)) * (rectMax - rectMin);

    // A merged quad tiles its sprite several times, so the wrapped coordinate jumps at every block edge and the hardware
    // would choose a different mip level on either side of the jump; that showed as large rectangular patches on flat
    // ice. The gradients come from the continuous (unwrapped) uv, so mip selection stays uniform across the whole quad.
    let gradientScale = rectMax - rectMin;
    let sampled = textureSampleGrad(
        atlasTexture,
        atlasSampler,
        atlasUv,
        dpdx(input.uv) * gradientScale,
        dpdy(input.uv) * gradientScale);

    // The reference keeps the texture's own alpha for translucent terrain and only cuts out nearly invisible texels
    // (ALPHA_CUTOUT 0.1), so ice and stained glass keep their tint. A flat placeholder alpha made every translucent
    // block look the same, which is what made ice read wrong.
    if (sampled.a < 0.1)
    {
        discard;
    }

    let light = input.colour * lightmapColour(input.light.x, input.light.y);
    let base = vec4<f32>(litColour(sampled.rgb, light), sampled.a);
    let span = max(fog.range.y - fog.range.x, 0.001);
    let fogged = clamp((input.fogDistance - fog.range.x) / span, 0.0, 1.0);
    return vec4<f32>(mix(base.rgb, fog.colour.rgb, fogged), base.a);
}

// Cutout pass: identical colour, but texels below half alpha are discarded instead of blended, which is what leaves and
// plants need (ref: ChunkSectionLayer.CUTOUT uses alpha testing, not blending).
@fragment
fn fs_cutout(input : VertexOutput) -> @location(0) vec4<f32> {
    let atlasSize = vec2<f32>(textureDimensions(atlasTexture));
    let rectMin = input.spriteRect.xy;
    let rectMax = input.spriteRect.zw;
    // Continuous mapping into the sprite rectangle: with a mipmapped, linearly filtered atlas this stays smooth when a
    // block covers fewer pixels than its texture has texels. Snapping to one texel per fragment speckled at distance.
    let atlasUv = rectMin + (input.uv - floor(input.uv)) * (rectMax - rectMin);

    // A merged quad tiles its sprite several times, so the wrapped coordinate jumps at every block edge and the hardware
    // would choose a different mip level on either side of the jump; that showed as large rectangular patches on flat
    // ice. The gradients come from the continuous (unwrapped) uv, so mip selection stays uniform across the whole quad.
    let gradientScale = rectMax - rectMin;
    let sampled = textureSampleGrad(
        atlasTexture,
        atlasSampler,
        atlasUv,
        dpdx(input.uv) * gradientScale,
        dpdy(input.uv) * gradientScale);

    if (sampled.a < 0.5) {
        discard;
    }

    let light = input.colour * lightmapColour(input.light.x, input.light.y);
    let base = vec4<f32>(litColour(sampled.rgb, light), 1.0);
    let span = max(fog.range.y - fog.range.x, 0.001);
    let fogged = clamp((input.fogDistance - fog.range.x) / span, 0.0, 1.0);
    return vec4<f32>(mix(base.rgb, fog.colour.rgb, fogged), 1.0);
}
