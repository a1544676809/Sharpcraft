// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;

namespace Sharpcraft.Framework.Rendering;

/// <summary>
/// One offscreen frame request: geometry plus the camera and clear colour
/// (docs/decisions/0003-wgpu-binding.md).
/// </summary>
/// <param name="Width">Target width in pixels.</param>
/// <param name="Height">Target height in pixels.</param>
/// <param name="Vertices">Interleaved position (3), colour (3), tile (2) and sprite rect (4) floats.</param>
/// <param name="Indices">Triangle indices.</param>
/// <param name="ClearColour">Background colour (linear RGBA).</param>
/// <param name="CameraPosition">Eye position in world space.</param>
/// <param name="CameraTarget">Look-at target in world space.</param>
/// <param name="FieldOfViewDegrees">Vertical field of view.</param>
public sealed record FrameRequest(
    int Width,
    int Height,
    float[] Vertices,
    uint[] Indices,
    Vector4 ClearColour,
    Vector3 CameraPosition,
    Vector3 CameraTarget,
    float FieldOfViewDegrees = 60f)
{
    /// <summary>Floats per vertex: position (3) + base colour (3) + uv (2) + sprite rect (4) + light (2).</summary>
    public const int FloatsPerVertex = 14;

    /// <summary>Vertex count.</summary>
    public int VertexCount => Vertices.Length / FloatsPerVertex;

    /// <summary>
    /// Per-frame lightmap parameters the terrain shader uses to compute the lightmap from each vertex's light levels
    /// (docs/decisions/0019, docs/specs/render/lighting.md 6). Defaults to overworld day.
    /// </summary>
    public LightmapUniform Lightmap { get; init; } = LightmapUniform.Day;

    /// <summary>
    /// Fog colour (RGB 0..1) for this frame. The reference multiplies a base fog colour by the time/weather fog
    /// timeline; the host recomputes this per frame (docs/modules/weather-and-sky-color.md 6.3).
    /// </summary>
    public Vector4 FogColour { get; init; } = new(0.75f, 0.85f, 1.0f, 1f);

    /// <summary>
    /// Atlas sampled by the shader, or null for a colour-only frame. UVs stay relative to the sprite
    /// rect so a greedy quad can tile its texture inside one atlas cell.
    /// </summary>
    public TextureData? Atlas { get; init; }

    /// <summary>Host supplied diagnostic options, or null for the plain frame.</summary>
    public FrameDiagnostics? Diagnostics { get; init; }

    /// <summary>
    /// Index ranges to draw separately, one per chunk. Null draws everything in one call.
    /// </summary>
    /// <remarks>
    /// Occlusion culling can only change the picture if a hidden chunk can be omitted, which needs one draw per chunk;
    /// while there is a single range the result is identical to one big draw.
    /// </remarks>
    public (int FirstIndex, int IndexCount)[]? DrawRanges { get; init; }

    /// <summary>
    /// Vertices of the translucent layer, drawn after the opaque pass with blending on and depth writes off.
    /// </summary>
    public float[]? TranslucentVertices { get; init; }

    /// <summary>Indices of the translucent layer, in the same vertex layout as <see cref="Vertices"/>.</summary>
    public uint[]? TranslucentIndices { get; init; }

    /// <summary>Vertices of the cutout layer: alpha tested, no blending, depth writes on.</summary>
    public float[]? CutoutVertices { get; init; }

    /// <summary>Indices of the cutout layer.</summary>
    public uint[]? CutoutIndices { get; init; }

    /// <summary>
    /// Index ranges of the translucent layer in back to front order. Null draws it in one call.
    /// </summary>
    /// <remarks>
    /// Blending is order dependent, so the caller has to sort: it is the only side that knows where each chunk sits
    /// relative to the camera.
    /// </remarks>
    public (int FirstIndex, int IndexCount)[]? TranslucentRanges { get; init; }

    /// <summary>
    /// When true the full-screen gradient sky (sky.wgsl) is not drawn; the caller supplies a sky disc / other backdrop
    /// instead (docs/modules/weather-and-sky-color.md 6.2).
    /// </summary>
    public bool SkipGradientSky { get; init; }

    /// <summary>Textures referenced by <see cref="CelestialDraws"/>, uploaded once and sampled by index.</summary>
    public TextureData[]? CelestialTextures { get; init; }

    /// <summary>
    /// Celestial body quads (sun, moon) drawn after the sky gradient and before world geometry, so terrain covers them.
    /// </summary>
    /// <remarks>
    /// Positions are already in the sky frame; the draw's model matrix places them relative to the camera so the bodies
    /// read as infinitely far. Depth writes are off, so a single order-independent pass is enough.
    /// </remarks>
    public CelestialQuadDraw[]? CelestialDraws { get; init; }

    /// <summary>
    /// Celestial position-colour draws (sunrise fan, stars): no texture, per-vertex colour times a per-draw tint.
    /// </summary>
    /// <remarks>Drawn after the textured bodies, still before world geometry, so terrain covers them.</remarks>
    public CelestialColorDraw[]? CelestialColorDraws { get; init; }

    /// <summary>Opaque sky-disc draws (top/bottom), rendered before the textured celestial bodies.</summary>
    public CelestialColorDraw[]? SkyDiscDraws { get; init; }
}

/// <summary>One textured celestial quad (sun or moon) in the sky backdrop.</summary>
/// <param name="TextureIndex">Index into <see cref="FrameRequest.CelestialTextures"/>.</param>
/// <param name="Vertices">Interleaved position (3) + uv (2) floats.</param>
/// <param name="Indices">Triangle indices into <paramref name="Vertices"/>.</param>
/// <param name="Model">Model matrix applied before the camera; typically a translation to the eye.</param>
/// <param name="Tint">Multiplied into the sampled texel (rgb) and alpha.</param>
public sealed record CelestialQuadDraw(
    int TextureIndex,
    float[] Vertices,
    ushort[] Indices,
    Matrix4x4 Model,
    Vector4 Tint);

/// <summary>Blend mode for a position-colour celestial draw, matching the reference pipeline.</summary>
public enum CelestialColorBlend
{
    /// <summary>Reference sunrise/sunset: colour SrcAlpha/OneMinusSrcAlpha, alpha One/OneMinusSrcAlpha.</summary>
    Translucent,

    /// <summary>Reference stars: OVERLAY = colour SrcAlpha/One, alpha One/Zero (additive onto the sky).</summary>
    Overlay,

    /// <summary>Opaque sky-disc geometry: no blending, writes the colour directly.</summary>
    Opaque
}

/// <summary>One position-colour celestial draw (sunrise fan or stars).</summary>
/// <param name="Vertices">Interleaved position (3) + colour (4) floats, per vertex.</param>
/// <param name="Indices">Triangle list indices into <paramref name="Vertices"/>.</param>
/// <param name="Model">Model matrix applied before the camera; typically a translation to the eye.</param>
/// <param name="Tint">Colour modulator multiplied into the vertex colour (rgba).</param>
/// <param name="Blend">Which reference blend state to use.</param>
public sealed record CelestialColorDraw(
    float[] Vertices,
    uint[] Indices,
    Matrix4x4 Model,
    Vector4 Tint,
    CelestialColorBlend Blend);

/// <summary>
/// Per-frame lightmap parameters for the terrain shader's per-fragment lightmap (docs/decisions/0019,
/// docs/specs/render/lighting.md 6). Layout matches the WGSL <c>Lightmap</c> struct: four floats then three vec4s (64 bytes).
/// </summary>
/// <param name="SkyFactor">Sky-light multiplier (day 1.0, night ~0.24).</param>
/// <param name="BlockFactor">Block-light multiplier (1.4).</param>
/// <param name="BrightnessFactor">Brightness slider (0.5).</param>
/// <param name="Padding">Explicit padding so the first vec4 is 16-byte aligned.</param>
/// <param name="SkyLightColor">Linear sky-light colour (day white, night blue-tinted).</param>
/// <param name="BlockLightTint">Block-light tint (raw 0..1 channels, like ARGB.vector3fFromRGB24).</param>
/// <param name="AmbientColor">Linear ambient colour (overworld black).</param>
public readonly record struct LightmapUniform(
    float SkyFactor,
    float BlockFactor,
    float BrightnessFactor,
    float Padding,
    Vector4 SkyLightColor,
    Vector4 BlockLightTint,
    Vector4 AmbientColor)
{
    /// <summary>Overworld daytime defaults, matching <c>VanillaLightmap.LightmapParameters.DayOverworld</c>.</summary>
    public static LightmapUniform Day { get; } = new(
        SkyFactor: 1f,
        BlockFactor: 1.4f,
        BrightnessFactor: 0.5f,
        Padding: 0f,
        SkyLightColor: Vector4.One,
        BlockLightTint: new Vector4(1.000f, 0.847f, 0.549f, 1f),
        AmbientColor: Vector4.Zero);
}

/// <summary>An RGBA8 image uploaded as a texture, typically the stitched sprite atlas.</summary>
/// <param name="Width">Pixel width.</param>
/// <param name="Height">Pixel height.</param>
/// <param name="Rgba">Pixels, four bytes per texel.</param>
public sealed record TextureData(int Width, int Height, byte[] Rgba)
{
    /// <summary>
    /// Mip levels one and up, each half the previous size, or null when only level zero is available.
    /// </summary>
    /// <remarks>
    /// The chain is generated where the sprites are known, because the reference rescales each sprite's alpha to keep its
    /// alpha test coverage; the renderer only uploads what it is given.
    /// </remarks>
    public byte[][]? Mips { get; init; }

    /// <summary>A single white texel, used when a frame samples no atlas.</summary>
    public static TextureData White { get; } = new(1, 1, [255, 255, 255, 255]);
}

/// <summary>Result of an offscreen render: RGBA8 pixels plus the matrix used.</summary>
/// <param name="Width">Frame width.</param>
/// <param name="Height">Frame height.</param>
/// <param name="Rgba">Row-major RGBA8 pixels.</param>
/// <param name="ViewProjection">Camera matrix that was applied.</param>
/// <param name="AdapterName">Adapter that rendered the frame, for diagnostics.</param>
public sealed record FrameResult(int Width, int Height, byte[] Rgba, Matrix4x4 ViewProjection, string AdapterName, float[]? Depth = null);

/// <summary>
/// Software-side camera math, kept separate from the GPU calls so it can be unit tested without
/// an adapter.
/// </summary>
public static class CameraMath
{
    /// <summary>Builds a right-handed look-at view-projection matrix with a WebGPU depth range (0..1).</summary>
    public static Matrix4x4 BuildViewProjection(
        Vector3 eye,
        Vector3 target,
        float fieldOfViewDegrees,
        float aspectRatio,
        float near = 0.1f,
        float far = 2000f)
    {
        if (aspectRatio <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(aspectRatio), aspectRatio, "aspect ratio must be positive");
        }

        var view = Matrix4x4.CreateLookAt(eye, target, Vector3.UnitY);
        var projection = BuildProjection(fieldOfViewDegrees, aspectRatio, near, far);
        return view * projection;
    }

    /// <summary>
    /// Same as the aspect-ratio overload, but takes pixel dimensions so callers cannot disagree
    /// with the surface they render into.
    /// </summary>
    public static Matrix4x4 BuildViewProjection(
        Vector3 eye,
        Vector3 target,
        float fieldOfViewDegrees,
        int width,
        int height)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, "width must be positive");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), height, "height must be positive");
        }

        return BuildViewProjection(eye, target, fieldOfViewDegrees, (float)width / height);
    }

    /// <summary>
    /// Perspective projection mapped to WebGPU's clip space: z in 0..1 instead of -1..1.
    /// </summary>
    public static Matrix4x4 BuildProjection(float fieldOfViewDegrees, float aspectRatio, float near, float far)
    {
        if (fieldOfViewDegrees is <= 0f or >= 180f)
        {
            throw new ArgumentOutOfRangeException(nameof(fieldOfViewDegrees), fieldOfViewDegrees, "field of view must be in (0, 180)");
        }

        if (aspectRatio <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(aspectRatio), aspectRatio, "aspect ratio must be positive");
        }

        var f = 1f / MathF.Tan(fieldOfViewDegrees * (MathF.PI / 180f) / 2f);
        var range = far / (near - far);

        return new Matrix4x4(
            f / aspectRatio, 0, 0, 0,
            0, f, 0, 0,
            0, 0, range, -1,
            0, 0, range * near, 0);
    }

    /// <summary>
    /// Places an eye so a sphere of the given radius roughly fills a 60 degree view, looking down
    /// at about 35 degrees from the south-east.
    /// </summary>
    public static (Vector3 Eye, Vector3 Target) DefaultCamera(Vector3 centre, float radius)
    {
        // Half of the subject extent divided by tan(30 deg) keeps it inside a 60 degree view; the
        // extra 1.35 leaves a margin so the subject is not clipped by the frame edges.
        var distance = MathF.Max(6f, radius / MathF.Tan(30f * MathF.PI / 180f) * 1.35f);
        var direction = Vector3.Normalize(new Vector3(0.55f, 1f, 0.55f));
        var eye = centre + direction * distance;
        return (eye, centre);
    }
}
