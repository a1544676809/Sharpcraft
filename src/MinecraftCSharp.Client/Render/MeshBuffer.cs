// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 MinecraftCSharp contributors

using System.Buffers;

namespace MinecraftCSharp.Client.Render;

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
        _vertexFloats = 0;
        _indexCount = 0;
    }

    /// <summary>Appends one quad: four vertices in winding order plus two triangles.</summary>
    public void AppendQuad(
        (int X, int Y, int Z) a,
        (int X, int Y, int Z) b,
        (int X, int Y, int Z) c,
        (int X, int Y, int Z) d,
        float red,
        float green,
        float blue)
    {
        const int floatsPerQuad = 4 * ChunkMesher.FloatsPerVertex;
        EnsureVertexRoom(floatsPerQuad);
        EnsureIndexRoom(ChunkMesher.IndicesPerQuad);

        var first = (uint)(_vertexFloats / ChunkMesher.FloatsPerVertex);
        WriteVertex(a, red, green, blue);
        WriteVertex(b, red, green, blue);
        WriteVertex(c, red, green, blue);
        WriteVertex(d, red, green, blue);

        _indices[_indexCount++] = first;
        _indices[_indexCount++] = first + 1;
        _indices[_indexCount++] = first + 2;
        _indices[_indexCount++] = first;
        _indices[_indexCount++] = first + 2;
        _indices[_indexCount++] = first + 3;
    }

    /// <summary>Copies the written geometry into exactly sized arrays.</summary>
    public MeshData ToMeshData(int blockCount) =>
        new(Vertices.ToArray(), Indices.ToArray(), blockCount);

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

    private void WriteVertex((int X, int Y, int Z) position, float red, float green, float blue)
    {
        var offset = _vertexFloats;
        _vertices[offset] = position.X;
        _vertices[offset + 1] = position.Y;
        _vertices[offset + 2] = position.Z;
        _vertices[offset + 3] = red;
        _vertices[offset + 4] = green;
        _vertices[offset + 5] = blue;
        _vertexFloats = offset + ChunkMesher.FloatsPerVertex;
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
