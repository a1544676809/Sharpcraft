// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Framework.Rendering;

/// <summary>
/// Geometry for the two dimensional layer: quads, text runs and scissor ranges, rebuilt every frame
/// (docs/modules/ui.md 9).
/// </summary>
/// <remarks>
/// The layout is deliberately not the three dimensional one. A quad vertex is twenty bytes, position two floats,
/// texture coordinate two floats, and one packed colour word, against forty eight bytes for world geometry.
/// Packing the colour into a single word is what keeps the vertex small; the packed value travels through a float
/// array unchanged.
/// <para>
/// Callers append data and never issue draw calls. Changing the scissor closes the current range and opens a new
/// one, so the renderer can walk the ranges and bind each once; a screen made of a handful of panels therefore
/// costs a handful of draws regardless of how many quads it contains.
/// </para>
/// </remarks>
public sealed class UiGeometry
{
    /// <summary>Floats per vertex: x, y, u, v and the packed colour.</summary>
    public const int FloatsPerVertex = 5;

    /// <summary>Bytes per vertex, which is the budget the design pins.</summary>
    public const int BytesPerVertex = FloatsPerVertex * sizeof(float);

    private const int FloatsPerQuad = 4 * FloatsPerVertex;
    private const int IndicesPerQuad = 6;

    private float[] _vertices = [];
    private uint[] _indices = [];
    private Range[] _ranges = [];
    private int _vertexFloats;
    private int _indexCount;
    private int _rangeCount;

    /// <summary>One contiguous run of indices sharing a scissor rectangle.</summary>
    public readonly record struct Range(float MinX, float MinY, float MaxX, float MaxY, int FirstIndex, int IndexCount);

    /// <summary>Vertex data in use.</summary>
    public ReadOnlySpan<float> Vertices => _vertices.AsSpan(0, _vertexFloats);

    /// <summary>Indices in use, zero based.</summary>
    public ReadOnlySpan<uint> Indices => _indices.AsSpan(0, _indexCount);

    /// <summary>Scissor ranges in use, in draw order.</summary>
    public ReadOnlySpan<Range> Ranges => _ranges.AsSpan(0, _rangeCount);

    /// <summary>Quads appended since the last clear, for diagnostics and tests.</summary>
    public int QuadCount => _indexCount / IndicesPerQuad;

    /// <summary>Drops the frame without releasing the buffers.</summary>
    public void Clear()
    {
        _vertexFloats = 0;
        _indexCount = 0;
        _rangeCount = 0;
    }

    /// <summary>Starts a new scissor range. Calling it with the current rectangle again is a no-op.</summary>
    /// <param name="minX">Left edge in virtual pixels.</param>
    /// <param name="minY">Top edge in virtual pixels.</param>
    /// <param name="maxX">Right edge.</param>
    /// <param name="maxY">Bottom edge.</param>
    public void SetScissor(float minX, float minY, float maxX, float maxY)
    {
        if (_rangeCount > 0)
        {
            var current = _ranges[_rangeCount - 1];
            var same = current.MinX == minX && current.MinY == minY && current.MaxX == maxX && current.MaxY == maxY;
            var emptySoFar = current.IndexCount == 0;
            var reused = _rangeCount - 1;

            if (same)
            {
                return;
            }

            if (emptySoFar)
            {
                // An unused range is simply retargeted, so opening and closing without drawing costs nothing.
                _ranges[reused] = current with { MinX = minX, MinY = minY, MaxX = maxX, MaxY = maxY };
                return;
            }
        }

        EnsureRangeRoom();
        _ranges[_rangeCount++] = new Range(minX, minY, maxX, maxY, _indexCount, 0);
    }

    /// <summary>Appends a solid rectangle, optionally snapping its edges to whole pixels.</summary>
    /// <param name="x">Left edge.</param>
    /// <param name="y">Top edge.</param>
    /// <param name="width">Width.</param>
    /// <param name="height">Height.</param>
    /// <param name="colour">Packed RGBA, red in the low byte.</param>
    public void AddRectangle(float x, float y, float width, float height, uint colour) =>
        AppendQuad(x, y, x + width, y + height, 0f, 0f, 0f, 0f, colour);

    /// <summary>Appends a textured rectangle, such as one glyph from the font atlas.</summary>
    /// <param name="x">Left edge.</param>
    /// <param name="y">Top edge.</param>
    /// <param name="width">Width.</param>
    /// <param name="height">Height.</param>
    /// <param name="u0">Left texture coordinate.</param>
    /// <param name="v0">Top texture coordinate.</param>
    /// <param name="u1">Right texture coordinate.</param>
    /// <param name="v1">Bottom texture coordinate.</param>
    /// <param name="colour">Packed RGBA tint.</param>
    public void AddTexturedRectangle(
        float x,
        float y,
        float width,
        float height,
        float u0,
        float v0,
        float u1,
        float v1,
        uint colour) =>
        AppendQuad(x, y, x + width, y + height, u0, v0, u1, v1, colour);

    /// <summary>Packs a colour channel tuple into the word the vertex stores.</summary>
    /// <param name="red">Red 0-255.</param>
    /// <param name="green">Green 0-255.</param>
    /// <param name="blue">Blue 0-255.</param>
    /// <param name="alpha">Alpha 0-255.</param>
    public static uint PackColour(byte red, byte green, byte blue, byte alpha) =>
        red | ((uint)green << 8) | ((uint)blue << 16) | ((uint)alpha << 24);

    private void AppendQuad(
        float left,
        float top,
        float right,
        float bottom,
        float u0,
        float v0,
        float u1,
        float v1,
        uint colour)
    {
        if (_rangeCount == 0)
        {
            // Drawing without an explicit scissor still needs one range, otherwise the renderer would skip it.
            SetScissor(float.MinValue, float.MinValue, float.MaxValue, float.MaxValue);
        }

        EnsureVertexRoom(FloatsPerQuad);
        EnsureIndexRoom(IndicesPerQuad);

        var first = (uint)(_vertexFloats / FloatsPerVertex);
        WriteVertex(left, top, u0, v0, colour);
        WriteVertex(right, top, u1, v0, colour);
        WriteVertex(right, bottom, u1, v1, colour);
        WriteVertex(left, bottom, u0, v1, colour);

        _indices[_indexCount++] = first;
        _indices[_indexCount++] = first + 1;
        _indices[_indexCount++] = first + 2;
        _indices[_indexCount++] = first;
        _indices[_indexCount++] = first + 2;
        _indices[_indexCount++] = first + 3;

        var range = _ranges[_rangeCount - 1];
        _ranges[_rangeCount - 1] = range with { IndexCount = range.IndexCount + IndicesPerQuad };
    }

    private void WriteVertex(float x, float y, float u, float v, uint colour)
    {
        _vertices[_vertexFloats++] = x;
        _vertices[_vertexFloats++] = y;
        _vertices[_vertexFloats++] = u;
        _vertices[_vertexFloats++] = v;
        _vertices[_vertexFloats++] = BitConverter.UInt32BitsToSingle(colour);
    }

    private void EnsureVertexRoom(int floats)
    {
        if (_vertices.Length - _vertexFloats >= floats)
        {
            return;
        }

        var capacity = Math.Max(_vertexFloats + floats, _vertices.Length == 0 ? 4096 : _vertices.Length * 2);
        Array.Resize(ref _vertices, capacity);
    }

    private void EnsureIndexRoom(int indices)
    {
        if (_indices.Length - _indexCount >= indices)
        {
            return;
        }

        var capacity = Math.Max(_indexCount + indices, _indices.Length == 0 ? 1536 : _indices.Length * 2);
        Array.Resize(ref _indices, capacity);
    }

    private void EnsureRangeRoom()
    {
        if (_rangeCount < _ranges.Length)
        {
            return;
        }

        var capacity = _ranges.Length == 0 ? 16 : _ranges.Length * 2;
        Array.Resize(ref _ranges, capacity);
    }
}
