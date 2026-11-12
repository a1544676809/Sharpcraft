// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Buffers;
using Sharpcraft.Client.Assets;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Reusable geometry storage for the mesher (docs/specs/performance-baseline.md §7).
/// </summary>
/// <remarks>
/// The previous implementation appended to <see cref="List{T}"/>, which grew by doubling and
/// allocated ~15 MB per full-world mesh; that allocation, not the arithmetic, was what pushed the
/// 95th percentile six times above the minimum. Buffers here come from
/// <see cref="ArrayPool{T}.Shared"/> and are reused, so a warm buffer meshes with zero allocation.
/// <para>
/// Not thread-safe by design: one buffer belongs to one meshing worker.
/// </para>
/// </remarks>
public sealed class MeshBuffer : IDisposable
{
    private float[] _vertices;
    private uint[] _indices;
    private int _vertexFloats;
    private int _indexCount;
    // Index count at the start of each chunk section, so the renderer can cull 16 cube sections instead of whole
    // columns (T-829). A plain array that only grows, because marking a section happens once per section inside the
    // meshing hot path, which must not allocate.
    private int[] _sectionStarts = [];
    private int _sectionCount;
    private bool _disposed;

    /// <summary>Creates a buffer, optionally pre-sized for an expected load.</summary>
    /// <param name="expectedBlocks">Rough number of blocks that will produce faces.</param>
    public MeshBuffer(int expectedBlocks = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(expectedBlocks);

        var floats = expectedBlocks <= 0 ? 0 : expectedBlocks * 6 * 4 * ChunkMesher.FloatsPerVertex;
        var indices = expectedBlocks <= 0 ? 0 : expectedBlocks * 6 * ChunkMesher.IndicesPerQuad;

        _vertices = floats == 0 ? [] : ArrayPool<float>.Shared.Rent(floats);
        _indices = indices == 0 ? [] : ArrayPool<uint>.Shared.Rent(indices);
    }

    /// <summary>Vertices written so far.</summary>
    public int VertexCount => _vertexFloats / ChunkMesher.FloatsPerVertex;

    /// <summary>Indices written so far.</summary>
    public int IndexCount => _indexCount;

    /// <summary>Interleaved position and colour floats.</summary>
    public ReadOnlySpan<float> Vertices => _vertices.AsSpan(0, _vertexFloats);

    /// <summary>Triangle indices.</summary>
    public ReadOnlySpan<uint> Indices => _indices.AsSpan(0, _indexCount);

    /// <summary>Backing capacity in vertices (diagnostics and tests).</summary>
    public int VertexCapacity => _vertices.Length / ChunkMesher.FloatsPerVertex;

    /// <summary>Drops the written geometry but keeps the rented memory for the next mesh.</summary>
    public void Clear()
    {
        _sectionCount = 0;
        _vertexFloats = 0;
        _indexCount = 0;
    }

    /// <summary>Appends one quad: four vertices in winding order plus two triangles.</summary>
    /// <param name="a">First corner (the quad origin).</param>
    /// <param name="b">Second corner.</param>
    /// <param name="c">Third corner.</param>
    /// <param name="d">Fourth corner.</param>
    /// <param name="red">Red component of the lit colour.</param>
    /// <param name="green">Green component of the lit colour.</param>
    /// <param name="blue">Blue component of the lit colour.</param>
    /// <param name="uvA">UV of the first corner, in sprite repeats.</param>
    /// <param name="uvB">UV of the second corner, in sprite repeats.</param>
    /// <param name="uvC">UV of the third corner, in sprite repeats.</param>
    /// <param name="uvD">UV of the fourth corner, in sprite repeats.</param>
    /// <param name="sprite">Atlas cell to sample; null covers the whole texture.</param>
    public void AppendQuad(
        (int X, int Y, int Z) a,
        (int X, int Y, int Z) b,
        (int X, int Y, int Z) c,
        (int X, int Y, int Z) d,
        float red,
        float green,
        float blue,
        (float U, float V) uvA,
        (float U, float V) uvB,
        (float U, float V) uvC,
        (float U, float V) uvD,
        SpriteRect? sprite = null,
        (float Sky, float Block) light = default) =>
        AppendQuad(a, b, c, d, (red, green, blue), (red, green, blue), (red, green, blue), (red, green, blue), light, light, light, light, uvA, uvB, uvC, uvD, sprite);

    /// <summary>
    /// Appends a quad whose four corners carry their own lit colour.
    /// </summary>
    /// <remarks>
    /// Smooth lighting and ambient occlusion vary per corner, exactly as the reference computes them per vertex, so a quad
    /// needs four colours rather than one. The single colour overload forwards here with all four equal.
    /// </remarks>
    /// <param name="a">First corner position.</param>
    /// <param name="b">Second corner position.</param>
    /// <param name="c">Third corner position.</param>
    /// <param name="d">Fourth corner position.</param>
    /// <param name="colourA">Lit colour of the first corner.</param>
    /// <param name="colourB">Lit colour of the second corner.</param>
    /// <param name="colourC">Lit colour of the third corner.</param>
    /// <param name="colourD">Lit colour of the fourth corner.</param>
    /// <param name="uvA">UV of the first corner, in sprite repeats.</param>
    /// <param name="uvB">UV of the second corner, in sprite repeats.</param>
    /// <param name="uvC">UV of the third corner, in sprite repeats.</param>
    /// <param name="uvD">UV of the fourth corner, in sprite repeats.</param>
    /// <param name="sprite">Atlas cell to sample; null covers the whole texture.</param>
    public void AppendQuad(
        (int X, int Y, int Z) a,
        (int X, int Y, int Z) b,
        (int X, int Y, int Z) c,
        (int X, int Y, int Z) d,
        (float R, float G, float B) colourA,
        (float R, float G, float B) colourB,
        (float R, float G, float B) colourC,
        (float R, float G, float B) colourD,
        (float Sky, float Block) lightA,
        (float Sky, float Block) lightB,
        (float Sky, float Block) lightC,
        (float Sky, float Block) lightD,
        (float U, float V) uvA,
        (float U, float V) uvB,
        (float U, float V) uvC,
        (float U, float V) uvD,
        SpriteRect? sprite = null)
    {
        const int floatsPerQuad = 4 * ChunkMesher.FloatsPerVertex;
        EnsureVertexRoom(floatsPerQuad);
        EnsureIndexRoom(ChunkMesher.IndicesPerQuad);

        var rect = sprite ?? WholeAtlas;
        var first = (uint)(_vertexFloats / ChunkMesher.FloatsPerVertex);
        WriteVertex(a, colourA.R, colourA.G, colourA.B, lightA, uvA, rect);
        WriteVertex(b, colourB.R, colourB.G, colourB.B, lightB, uvB, rect);
        WriteVertex(c, colourC.R, colourC.G, colourC.B, lightC, uvC, rect);
        WriteVertex(d, colourD.R, colourD.G, colourD.B, lightD, uvD, rect);

        _indices[_indexCount++] = first;
        _indices[_indexCount++] = first + 1;
        _indices[_indexCount++] = first + 2;
        _indices[_indexCount++] = first;
        _indices[_indexCount++] = first + 2;
        _indices[_indexCount++] = first + 3;
    }

    /// <summary>
    /// Appends a quad and its reverse winding. The reference draws fluid faces double-sided (addFace's addBackFace), so a
    /// fluid quad in an opaque/depth-culling pass must cover both sides or back-face culling eats it.
    /// </summary>
    public void AppendQuadDoubleSided(
        (float X, float Y, float Z) a,
        (float X, float Y, float Z) b,
        (float X, float Y, float Z) c,
        (float X, float Y, float Z) d,
        (float R, float G, float B) colourA,
        (float R, float G, float B) colourB,
        (float R, float G, float B) colourC,
        (float R, float G, float B) colourD,
        (float Sky, float Block) lightA,
        (float Sky, float Block) lightB,
        (float Sky, float Block) lightC,
        (float Sky, float Block) lightD,
        (float U, float V) uvA,
        (float U, float V) uvB,
        (float U, float V) uvC,
        (float U, float V) uvD,
        SpriteRect? sprite = null)
    {
        AppendQuad(a, b, c, d, colourA, colourB, colourC, colourD, lightA, lightB, lightC, lightD, uvA, uvB, uvC, uvD, sprite);
        AppendQuad(a, d, c, b, colourA, colourD, colourC, colourB, lightA, lightD, lightC, lightB, uvA, uvD, uvC, uvB, sprite);
    }
    /// <summary>Marks the start of a chunk section in the index stream.</summary>
    /// <remarks>
    /// The renderer culls per section, and a section's geometry is a contiguous run of the column's index buffer, so
    /// recording where each run begins is enough for it to decide what to submit. Call once per section, in order.
    /// </remarks>
    /// <summary>Prepares one slot per chunk section before meshing begins.</summary>
    /// <param name="sectionCount">Sections the caller is about to mark.</param>
    /// <remarks>
    /// Sized up front rather than grown while marking: the meshing hot path must not allocate, and a growing array
    /// would allocate on the first section of every warm call.
    /// </remarks>
    public void ReserveSections(int sectionCount)
    {
        _sectionCount = 0;
        if (_sectionStarts.Length < sectionCount)
        {
            _sectionStarts = new int[sectionCount];
        }
    }

    /// <summary>Records the current index count as the start of a chunk section.</summary>
    public void MarkSection()
    {
        if (_sectionCount < _sectionStarts.Length)
        {
            _sectionStarts[_sectionCount] = _indexCount;
        }

        _sectionCount++;
    }

    /// <summary>Copies the written geometry into exactly sized arrays.</summary>
    public MeshData ToMeshData(int blockCount)
    {
        // The final start doubles as the end of the last section, so the ranges are starts[i]..starts[i + 1].
        int[]? starts = null;
        if (_sectionCount > 0)
        {
            starts = new int[_sectionCount + 1];
            _sectionStarts.AsSpan(0, _sectionCount).CopyTo(starts);
            starts[^1] = _indexCount;
        }

        return new MeshData(Vertices.ToArray(), Indices.ToArray(), blockCount, starts);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_vertices.Length > 0)
        {
            ArrayPool<float>.Shared.Return(_vertices);
            _vertices = [];
        }

        if (_indices.Length > 0)
        {
            ArrayPool<uint>.Shared.Return(_indices);
            _indices = [];
        }

        _vertexFloats = 0;
        _indexCount = 0;
    }

    /// <summary>Default sprite rect: the whole texture, used when no atlas entry is known.</summary>
    private static readonly SpriteRect WholeAtlas = new(0, 0, 1, 1, 1)
    {
        U0 = 0f,
        V0 = 0f,
        U1 = 1f,
        V1 = 1f
    };

    /// <summary>Writes one vertex from a fractional position, used by the fluid surface height.</summary>
    private void WriteVertex(
        (float X, float Y, float Z) position,
        float red,
        float green,
        float blue,
        (float Sky, float Block) light,
        (float U, float V) uv,
        SpriteRect sprite)
    {
        var offset = _vertexFloats;
        _vertices[offset] = position.X;
        _vertices[offset + 1] = position.Y;
        _vertices[offset + 2] = position.Z;
        _vertices[offset + 3] = BitConverter.UInt32BitsToSingle(
            (uint)Math.Clamp((int)MathF.Round(red * 255f), 0, 255)
            | ((uint)Math.Clamp((int)MathF.Round(green * 255f), 0, 255) << 8)
            | ((uint)Math.Clamp((int)MathF.Round(blue * 255f), 0, 255) << 16)
            | (255u << 24));
        _vertices[offset + 4] = BitConverter.UInt32BitsToSingle(
            (uint)BitConverter.HalfToUInt16Bits((Half)uv.U)
            | ((uint)BitConverter.HalfToUInt16Bits((Half)uv.V) << 16));
        _vertices[offset + 5] = BitConverter.UInt32BitsToSingle(
            (uint)(sprite.RectId & 0xFFFF)
            | ((uint)Math.Clamp((int)MathF.Round(light.Sky * 255f), 0, 255) << 16)
            | ((uint)Math.Clamp((int)MathF.Round(light.Block * 255f), 0, 255) << 24));

        _vertexFloats = offset + ChunkMesher.FloatsPerVertex;
    }

    private void WriteVertex(
        (int X, int Y, int Z) position,
        float red,
        float green,
        float blue,
        (float Sky, float Block) light,
        (float U, float V) uv,
        SpriteRect sprite)
    {
        var offset = _vertexFloats;
        _vertices[offset] = position.X;
        _vertices[offset + 1] = position.Y;
        _vertices[offset + 2] = position.Z;
        _vertices[offset + 3] = BitConverter.UInt32BitsToSingle(
            (uint)Math.Clamp((int)MathF.Round(red * 255f), 0, 255)
            | ((uint)Math.Clamp((int)MathF.Round(green * 255f), 0, 255) << 8)
            | ((uint)Math.Clamp((int)MathF.Round(blue * 255f), 0, 255) << 16)
            | (255u << 24));
        _vertices[offset + 4] = BitConverter.UInt32BitsToSingle(
            (uint)BitConverter.HalfToUInt16Bits((Half)uv.U)
            | ((uint)BitConverter.HalfToUInt16Bits((Half)uv.V) << 16));
        _vertices[offset + 5] = BitConverter.UInt32BitsToSingle(
            (uint)(sprite.RectId & 0xFFFF)
            | ((uint)Math.Clamp((int)MathF.Round(light.Sky * 255f), 0, 255) << 16)
            | ((uint)Math.Clamp((int)MathF.Round(light.Block * 255f), 0, 255) << 24));

        _vertexFloats = offset + ChunkMesher.FloatsPerVertex;
    }

    /// <summary>
    /// Appends a quad whose corners are fractional positions, which the fluid surface needs because still water sits at
    /// eight ninths of a block rather than at the block top.
    /// </summary>
    /// <param name="a">First corner position.</param>
    /// <param name="b">Second corner position.</param>
    /// <param name="c">Third corner position.</param>
    /// <param name="d">Fourth corner position.</param>
    /// <param name="colourA">Lit colour of the first corner.</param>
    /// <param name="colourB">Lit colour of the second corner.</param>
    /// <param name="colourC">Lit colour of the third corner.</param>
    /// <param name="colourD">Lit colour of the fourth corner.</param>
    /// <param name="uvA">UV of the first corner.</param>
    /// <param name="uvB">UV of the second corner.</param>
    /// <param name="uvC">UV of the third corner.</param>
    /// <param name="uvD">UV of the fourth corner.</param>
    /// <param name="sprite">Atlas cell to sample; null covers the whole texture.</param>
    public void AppendQuad(
        (float X, float Y, float Z) a,
        (float X, float Y, float Z) b,
        (float X, float Y, float Z) c,
        (float X, float Y, float Z) d,
        (float R, float G, float B) colourA,
        (float R, float G, float B) colourB,
        (float R, float G, float B) colourC,
        (float R, float G, float B) colourD,
        (float Sky, float Block) lightA,
        (float Sky, float Block) lightB,
        (float Sky, float Block) lightC,
        (float Sky, float Block) lightD,
        (float U, float V) uvA,
        (float U, float V) uvB,
        (float U, float V) uvC,
        (float U, float V) uvD,
        SpriteRect? sprite = null)
    {
        EnsureVertexRoom(4 * ChunkMesher.FloatsPerVertex);
        EnsureIndexRoom(ChunkMesher.IndicesPerQuad);

        var rect = sprite ?? WholeAtlas;
        var first = (uint)(_vertexFloats / ChunkMesher.FloatsPerVertex);
        WriteVertex(a, colourA.R, colourA.G, colourA.B, lightA, uvA, rect);
        WriteVertex(b, colourB.R, colourB.G, colourB.B, lightB, uvB, rect);
        WriteVertex(c, colourC.R, colourC.G, colourC.B, lightC, uvC, rect);
        WriteVertex(d, colourD.R, colourD.G, colourD.B, lightD, uvD, rect);

        _indices[_indexCount++] = first;
        _indices[_indexCount++] = first + 1;
        _indices[_indexCount++] = first + 2;
        _indices[_indexCount++] = first;
        _indices[_indexCount++] = first + 2;
        _indices[_indexCount++] = first + 3;
    }

    private void EnsureVertexRoom(int floats)
    {
        var required = _vertexFloats + floats;
        if (required <= _vertices.Length)
        {
            return;
        }

        var target = Math.Max(required, Math.Max(1024, _vertices.Length * 2));
        var rented = ArrayPool<float>.Shared.Rent(target);
        _vertices.AsSpan(0, _vertexFloats).CopyTo(rented);

        if (_vertices.Length > 0)
        {
            ArrayPool<float>.Shared.Return(_vertices);
        }

        _vertices = rented;
    }

    private void EnsureIndexRoom(int indices)
    {
        var required = _indexCount + indices;
        if (required <= _indices.Length)
        {
            return;
        }

        var target = Math.Max(required, Math.Max(256, _indices.Length * 2));
        var rented = ArrayPool<uint>.Shared.Rent(target);
        _indices.AsSpan(0, _indexCount).CopyTo(rented);

        if (_indices.Length > 0)
        {
            ArrayPool<uint>.Shared.Return(_indices);
        }

        _indices = rented;
    }
}
