// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Assets;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Writes one terrain-layout vertex into a float array, for the meshers that build their own arrays instead of going
/// through <see cref="MeshBuffer"/> (T-832).
/// </summary>
/// <remarks>
/// The vertex is twenty four bytes in six float slots: position, a packed colour word, two halves of tile coordinate,
/// and one word holding the sprite index plus both light levels. Two of the slots carry bit patterns rather than
/// numbers, and every builder that feeds a terrain-layout pipeline has to agree on that byte for byte, so the packing
/// lives in exactly one place.
/// </remarks>
internal static class TerrainVertexWriter
{
    /// <summary>Writes one vertex and advances the offset by <see cref="ChunkMesher.FloatsPerVertex"/>.</summary>
    /// <param name="destination">Vertex array being built.</param>
    /// <param name="offset">Write position, advanced past the vertex.</param>
    /// <param name="x">World X.</param>
    /// <param name="y">World Y.</param>
    /// <param name="z">World Z.</param>
    /// <param name="red">Lit red, 0..1.</param>
    /// <param name="green">Lit green, 0..1.</param>
    /// <param name="blue">Lit blue, 0..1.</param>
    /// <param name="u">Tile coordinate along the face.</param>
    /// <param name="v">Tile coordinate across the face.</param>
    /// <param name="sprite">Atlas cell the face samples.</param>
    /// <param name="sky">Sky light, 0..1.</param>
    /// <param name="block">Block light, 0..1.</param>
    public static void Write(
        float[] destination,
        ref int offset,
        float x,
        float y,
        float z,
        float red,
        float green,
        float blue,
        float u,
        float v,
        SpriteRect sprite,
        float sky,
        float block)
    {
        destination[offset] = x;
        destination[offset + 1] = y;
        destination[offset + 2] = z;
        destination[offset + 3] = BitConverter.UInt32BitsToSingle(
            Channel(red)
            | (Channel(green) << 8)
            | (Channel(blue) << 16)
            | (255u << 24));
        destination[offset + 4] = BitConverter.UInt32BitsToSingle(
            (uint)BitConverter.HalfToUInt16Bits((Half)u)
            | ((uint)BitConverter.HalfToUInt16Bits((Half)v) << 16));
        destination[offset + 5] = BitConverter.UInt32BitsToSingle(
            (uint)(sprite.RectId & 0xFFFF)
            | (Channel(sky) << 16)
            | (Channel(block) << 24));

        offset += ChunkMesher.FloatsPerVertex;
    }

    private static uint Channel(float value) =>
        (uint)Math.Clamp((int)MathF.Round(value * 255f), 0, 255);
}
