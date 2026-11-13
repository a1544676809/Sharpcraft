// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Render;

namespace Sharpcraft.Client.Tests;

/// <summary>
/// Reads a field out of the interleaved terrain vertex by name instead of by float offset.
/// </summary>
/// <remarks>
/// The layout has changed under these tests three times: colour and light were a triple and a pair of floats and became
/// one packed word each (T-826), then the atlas rectangle left the vertex entirely in favour of a sixteen bit index into
/// a table (T-832). Tests that encoded the offsets directly all broke at once and had to be re-derived; naming the field
/// keeps the assertions about the values the mesher produced, which is what they were written to check.
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

    /// <summary>Tile coordinate of a vertex, in sprite repeats, decoded from two halves.</summary>
    /// <param name="vertices">Interleaved vertex stream.</param>
    /// <param name="vertex">Vertex index.</param>
    public static (float U, float V) Uv(ReadOnlySpan<float> vertices, int vertex)
    {
        var packed = BitConverter.SingleToUInt32Bits(vertices[(vertex * FloatsPerVertex) + 4]);
        return ((float)BitConverter.UInt16BitsToHalf((ushort)(packed & 0xFFFF)),
                (float)BitConverter.UInt16BitsToHalf((ushort)(packed >> 16)));
    }

    /// <summary>Index into the atlas rectangle table, carried instead of the rectangle itself (T-832).</summary>
    /// <param name="vertices">Interleaved vertex stream.</param>
    /// <param name="vertex">Vertex index.</param>
    public static int SpriteId(ReadOnlySpan<float> vertices, int vertex) =>
        (int)(BitConverter.SingleToUInt32Bits(vertices[(vertex * FloatsPerVertex) + 5]) & 0xFFFF);

    /// <summary>Sprite rectangle the vertex index resolves to, read back out of the table.</summary>
    /// <param name="table">Atlas rectangle table, four floats per sprite.</param>
    /// <param name="id">Sprite index from <see cref="SpriteId"/>.</param>
    public static (float U0, float V0, float U1, float V1) RectIn(ReadOnlySpan<float> table, int id)
    {
        var offset = id * 4;
        return (table[offset], table[offset + 1], table[offset + 2], table[offset + 3]);
    }

    /// <summary>Sky and block light of a vertex, unpacked from the sprite word.</summary>
    /// <param name="vertices">Interleaved vertex stream.</param>
    /// <param name="vertex">Vertex index.</param>
    public static (float Sky, float Block) Light(ReadOnlySpan<float> vertices, int vertex)
    {
        var packed = BitConverter.SingleToUInt32Bits(vertices[(vertex * FloatsPerVertex) + 5]);
        return (((packed >> 16) & 0xFF) / 255f, ((packed >> 24) & 0xFF) / 255f);
    }
}
