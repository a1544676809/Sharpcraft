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
    /// <summary>Floats per vertex.</summary>
    public const int FloatsPerVertex = 12;

    /// <summary>Vertex count.</summary>
    public int VertexCount => Vertices.Length / FloatsPerVertex;

    /// <summary>
    /// Atlas sampled by the shader, or null for a colour-only frame. UVs stay relative to the sprite
    /// rect so a greedy quad can tile its texture inside one atlas cell.
    /// </summary>
    public TextureData? Atlas { get; init; }
}

/// <summary>An RGBA8 image uploaded as a texture, typically the stitched sprite atlas.</summary>
/// <param name="Width">Pixel width.</param>
/// <param name="Height">Pixel height.</param>
/// <param name="Rgba">Pixels, four bytes per texel.</param>
public sealed record TextureData(int Width, int Height, byte[] Rgba)
{
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
