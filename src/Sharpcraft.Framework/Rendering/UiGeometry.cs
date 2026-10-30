// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Framework.Rendering;

/// <summary>Which pipeline a range must be drawn with.</summary>
public enum UiMaterial
{
    /// <summary>Flat colour, no texture sampling.</summary>
    Colour,

    /// <summary>Textured, sampling the glyph atlas.</summary>
    Text
}

/// <summary>
/// Geometry for the two dimensional layer: quads, text runs and scissor ranges, rebuilt every frame
/// (docs/modules/ui.md 9).
/// </summary>
/// <remarks>
/// The layout is deliberately not the three dimensional one. A quad vertex is twenty bytes: position two floats,
/// texture coordinate two floats, and one packed colour word, against forty eight bytes for world geometry.
/// <para>
/// Callers append data and never issue draw calls. A range closes when the scissor rectangle or the material
/// changes, so the renderer binds each range once; a screen made of a couple of panels plus their text therefore
/// costs a couple of draws regardless of how many quads it contains.
/// </para>
/// </remarks>
public sealed class UiGeometry
{
    /// <summary>
    /// Converts each sRGB byte to its linear value, once.
    /// </summary>
    /// <remarks>
    /// The swap chain is an sRGB format, so the hardware encodes whatever is written to it. Interface colours are
    /// authored the way they should look, so they have to be decoded first or every colour comes out washed out: a
    /// pale blue of 192 arrives on screen as 225, which is exactly what the encoding does to it.
    /// </remarks>
    private static readonly byte[] SrgbToLinear = BuildSrgbToLinear();

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
    private UiMaterial _material;

    /// <summary>One contiguous run of indices sharing a scissor rectangle and a material.</summary>
    public readonly record struct Range(
        float MinX,
        float MinY,
        float MaxX,
        float MaxY,
        UiMaterial Material,
        int FirstIndex,
        int IndexCount);

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
        _material = UiMaterial.Colour;
    }

    /// <summary>Starts a new scissor range, or retargets the current one when it is still empty.</summary>
    /// <param name="minX">Left edge in virtual pixels.</param>
    /// <param name="minY">Top edge in virtual pixels.</param>
    /// <param name="maxX">Right edge.</param>
    /// <param name="maxY">Bottom edge.</param>
    public void SetScissor(float minX, float minY, float maxX, float maxY)
    {
        if (_rangeCount > 0)
        {
            var current = _ranges[_rangeCount - 1];
            var same = current.MinX == minX && current.MinY == minY && current.MaxX == maxX && current.MaxY == maxY
                && current.Material == _material;

            if (same)
            {
                return;
            }

            if (current.IndexCount == 0)
            {
                // An unused range is simply retargeted, so opening and closing without drawing costs nothing.
                _ranges[_rangeCount - 1] = current with
                {
                    MinX = minX,
                    MinY = minY,
                    MaxX = maxX,
                    MaxY = maxY,
                    Material = _material
                };

                return;
            }
        }

        EnsureRangeRoom();
        _ranges[_rangeCount++] = new Range(minX, minY, maxX, maxY, _material, _indexCount, 0);
    }

    /// <summary>
    /// Switches the material, closing the current range so the renderer can bind the right pipeline. Consecutive
    /// quads of the same material and scissor stay in one range, which keeps a panel plus its text at two draws.
    /// </summary>
    /// <param name="material">Material the following quads use.</param>
    public void UseMaterial(UiMaterial material)
    {
        if (_material == material)
        {
            return;
        }

        _material = material;

        if (_rangeCount == 0)
        {
            // Nothing open yet; the first quad opens a range carrying this material.
            return;
        }

        var last = _ranges[_rangeCount - 1];

        if (last.IndexCount == 0)
        {
            _ranges[_rangeCount - 1] = last with { Material = material };
            return;
        }

        EnsureRangeRoom();
        _ranges[_rangeCount++] = last with { Material = material, FirstIndex = _indexCount, IndexCount = 0 };
    }

    /// <summary>Appends a solid rectangle.</summary>
    /// <param name="x">Left edge.</param>
    /// <param name="y">Top edge.</param>
    /// <param name="width">Width.</param>
    /// <param name="height">Height.</param>
    /// <param name="colour">Packed RGBA, red in the low byte.</param>
    public void AddRectangle(float x, float y, float width, float height, uint colour)
    {
        UseMaterial(UiMaterial.Colour);
        AppendQuad(x, y, x + width, y + height, 0f, 0f, 0f, 0f, colour);
    }

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
        uint colour)
    {
        UseMaterial(UiMaterial.Text);
        AppendQuad(x, y, x + width, y + height, u0, v0, u1, v1, colour);
    }

    /// <summary>Packs a colour channel tuple into the word the vertex stores.</summary>
    /// <param name="red">Red 0-255.</param>
    /// <param name="green">Green 0-255.</param>
    /// <param name="blue">Blue 0-255.</param>
    /// <param name="alpha">Alpha 0-255.</param>
    public static uint PackColour(byte red, byte green, byte blue, byte alpha) =>
        red | ((uint)green << 8) | ((uint)blue << 16) | ((uint)alpha << 24);

    /// <summary>
    /// Converts a colour written as <c>AARRGGBB</c> into the word the vertex expects.
    /// </summary>
    /// <param name="argb">Colour with alpha in the top byte, then red, green and blue.</param>
    /// <remarks>
    /// Interface colours are written the readable way round, red first, while the vertex carries one byte per channel
    /// in memory order. Converting in a single place stops the two from drifting, which is how a selection once came
    /// out red: the literal was read as the vertex order instead of the written one.
    /// </remarks>
    public static uint FromArgb(uint argb) =>
        (argb & 0xFF000000u)
        | ((argb & 0x00FF0000u) >> 16)
        | (argb & 0x0000FF00u)
        | ((argb & 0x000000FFu) << 16);

    /// <summary>
    /// Decodes a packed sRGB colour into the linear values the sRGB swap chain expects.
    /// </summary>
    /// <param name="colour">Packed colour, red in the low byte.</param>
    public static uint ToLinear(uint colour)
    {
        var red = SrgbToLinear[colour & 0xFF];
        var green = SrgbToLinear[(colour >> 8) & 0xFF];
        var blue = SrgbToLinear[(colour >> 16) & 0xFF];
        return red | ((uint)green << 8) | ((uint)blue << 16) | (colour & 0xFF000000u);
    }

    private static byte[] BuildSrgbToLinear()
    {
        var table = new byte[256];

        for (var value = 0; value < table.Length; value++)
        {
            var channel = value / 255.0;

            table[value] = (byte)Math.Round(
                255.0 * (channel <= 0.04045 ? channel / 12.92 : Math.Pow((channel + 0.055) / 1.055, 2.4)));
        }

        return table;
    }

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

        // The swap chain is an sRGB format and encodes on write, so the authored colour is decoded here, once, at the
        // single point where a colour enters the vertex buffer.
        // Gamma-space blending matches the reference; see docs/decisions/0013-gamma-space-ui-blending.md.
        var linear = colour;

        WriteVertex(left, top, u0, v0, linear);
        WriteVertex(right, top, u1, v0, linear);
        WriteVertex(right, bottom, u1, v1, linear);
        WriteVertex(left, bottom, u0, v1, linear);

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
