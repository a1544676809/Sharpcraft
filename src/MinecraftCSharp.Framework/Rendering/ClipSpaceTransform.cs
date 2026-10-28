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
    /// <summary>Floats per transformed vertex: clip position (4) plus colour (3).</summary>
    public const int FloatsPerVertex = 7;

    /// <summary>Stride of one transformed vertex in bytes.</summary>
    public const int VertexStride = FloatsPerVertex * sizeof(float);

    /// <summary>Builds interleaved clip-space positions and colours from a frame request.</summary>
    public static float[] Build(FrameRequest request, Matrix4x4 viewProjection)
    {
        ArgumentNullException.ThrowIfNull(request);

        var count = request.VertexCount;
        var data = new float[count * FloatsPerVertex];

        for (var i = 0; i < count; i++)
        {
            var offset = i * FrameRequest.FloatsPerVertex;
            var position = new Vector3(
                request.Vertices[offset],
                request.Vertices[offset + 1],
                request.Vertices[offset + 2]);
            var clip = Vector4.Transform(position, viewProjection);

            var destination = i * FloatsPerVertex;
            data[destination] = clip.X;
            data[destination + 1] = clip.Y;
            data[destination + 2] = clip.Z;
            data[destination + 3] = clip.W;
            data[destination + 4] = request.Vertices[offset + 3];
            data[destination + 5] = request.Vertices[offset + 4];
            data[destination + 6] = request.Vertices[offset + 5];
        }

        return data;
    }
}
