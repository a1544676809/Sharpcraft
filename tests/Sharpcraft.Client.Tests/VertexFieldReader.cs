// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;

namespace Sharpcraft.Client.Tests;

/// <summary>
/// Reads a field out of the interleaved terrain vertex by name instead of by float offset.
/// </summary>
/// <remarks>
/// The layout has changed under these tests twice: colour and light were a triple and a pair of floats and are now one
/// packed word each, which moved every offset after them (T-826). Tests that encoded the offsets directly all broke at
/// once and had to be re-derived. Naming the field keeps the assertions about the values the mesher produced, which is
/// what they were written to check.
/// </remarks>
internal static class VertexFieldReader
{
    /// <summary>Floats per vertex, so a test can walk the stream.</summary>
    public const int FloatsPerVertex = ChunkMesher.FloatsPerVertex;

    /// <summary>World position of a vertex.</summary>
    /// <param name="vertices">Interleaved vertex stream.</param>
    /// <param name="vertex">Vertex index.</param>
    public static (float X, float Y, float Z) Position(ReadOnlySpan<float> vertices, int vertex)
    {
        var offset = vertex * FloatsPerVertex;
        return (vertices[offset], vertices[offset + 1], vertices[offset + 2]);
    }

    /// <summary>Lit colour of a vertex, unpacked from its single word.</summary>
    /// <param name="vertices">Interleaved vertex stream.</param>
    /// <param name="vertex">Vertex index.</param>
    public static (float R, float G, float B) Colour(ReadOnlySpan<float> vertices, int vertex)
    {
        var packed = BitConverter.SingleToUInt32Bits(vertices[(vertex * FloatsPerVertex) + 3]);
        return ((packed & 0xFF) / 255f, ((packed >> 8) & 0xFF) / 255f, ((packed >> 16) & 0xFF) / 255f);
    }

    /// <summary>Tile coordinate of a vertex, in sprite repeats.</summary>
    /// <param name="vertices">Interleaved vertex stream.</param>
    /// <param name="vertex">Vertex index.</param>
    public static (float U, float V) Uv(ReadOnlySpan<float> vertices, int vertex)
    {
        var offset = vertex * FloatsPerVertex;
        return (vertices[offset + 4], vertices[offset + 5]);
    }

    /// <summary>Sprite rectangle in the atlas: u0, v0, u1, v1.</summary>
    /// <param name="vertices">Interleaved vertex stream.</param>
    /// <param name="vertex">Vertex index.</param>
    public static (float U0, float V0, float U1, float V1) Rect(ReadOnlySpan<float> vertices, int vertex)
    {
        var offset = vertex * FloatsPerVertex;
        return (vertices[offset + 6], vertices[offset + 7], vertices[offset + 8], vertices[offset + 9]);
    }

    /// <summary>Sky and block light of a vertex, unpacked from its single word.</summary>
    /// <param name="vertices">Interleaved vertex stream.</param>
    /// <param name="vertex">Vertex index.</param>
    public static (float Sky, float Block) Light(ReadOnlySpan<float> vertices, int vertex)
    {
        var packed = BitConverter.SingleToUInt32Bits(vertices[(vertex * FloatsPerVertex) + 10]);
        return ((packed & 0xFF) / 255f, ((packed >> 8) & 0xFF) / 255f);
    }
}
