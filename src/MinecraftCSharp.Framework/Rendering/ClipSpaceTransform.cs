// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 MinecraftCSharp contributors

using System.Numerics;

namespace MinecraftCSharp.Framework.Rendering;

/// <summary>
/// CPU-side vertex preparation: positions are transformed into WebGPU clip space before upload,
/// so the first shader only passes them through (docs/decisions/0003-wgpu-binding.md).
/// </summary>
public static class ClipSpaceTransform
{
    /// <summary>
    /// Floats per transformed vertex: clip position (4), colour (3), tile (2) and sprite rect (4).
    /// </summary>
    public const int FloatsPerVertex = 13;

    /// <summary>Stride of one transformed vertex in bytes.</summary>
    public const int VertexStride = FloatsPerVertex * sizeof(float);

    /// <summary>Number of floats a frame needs in its destination span.</summary>
    public static int RequiredFloats(FrameRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.VertexCount * FloatsPerVertex;
    }

    /// <summary>
    /// Writes interleaved clip-space positions and colours into <paramref name="destination"/>.
    /// Callers that own a pooled buffer use this to avoid allocating a fresh array per frame.
    /// </summary>
    public static void Build(FrameRequest request, Matrix4x4 viewProjection, Span<float> destination)
    {
        ArgumentNullException.ThrowIfNull(request);

        var required = RequiredFloats(request);
        if (destination.Length < required)
        {
            throw new ArgumentException(
                $"destination holds {destination.Length} floats but {required} are required",
                nameof(destination));
        }

        WriteVertices(request, viewProjection, destination);
    }

    /// <summary>Builds interleaved clip-space positions and colours from a frame request.</summary>
    public static float[] Build(FrameRequest request, Matrix4x4 viewProjection)
    {
        ArgumentNullException.ThrowIfNull(request);

        var count = request.VertexCount;
        var data = new float[count * FloatsPerVertex];

        WriteVertices(request, viewProjection, data);
        return data;
    }

    private static void WriteVertices(FrameRequest request, Matrix4x4 viewProjection, Span<float> destination)
    {
        var count = request.VertexCount;

        for (var i = 0; i < count; i++)
        {
            var offset = i * FrameRequest.FloatsPerVertex;
            var position = new Vector3(
                request.Vertices[offset],
                request.Vertices[offset + 1],
                request.Vertices[offset + 2]);
            var clip = Vector4.Transform(position, viewProjection);

            var target = i * FloatsPerVertex;
            destination[target] = clip.X;
            destination[target + 1] = clip.Y;
            destination[target + 2] = clip.Z;
            destination[target + 3] = clip.W;
            destination[target + 4] = request.Vertices[offset + 3];
            destination[target + 5] = request.Vertices[offset + 4];
            destination[target + 6] = request.Vertices[offset + 5];

            // tile (how many sprite repeats the quad covers) and the sprite rect in the atlas
            destination[target + 7] = request.Vertices[offset + 6];
            destination[target + 8] = request.Vertices[offset + 7];
            destination[target + 9] = request.Vertices[offset + 8];
            destination[target + 10] = request.Vertices[offset + 9];
            destination[target + 11] = request.Vertices[offset + 10];
            destination[target + 12] = request.Vertices[offset + 11];
        }
    }
}
