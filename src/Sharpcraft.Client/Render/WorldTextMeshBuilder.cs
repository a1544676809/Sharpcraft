// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using Sharpcraft.Client.Ui;
using Sharpcraft.Framework.Rendering;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Builds world-space text draws from the bitmap font. Every glyph becomes a four-vertex textured quad; the
/// background is a separate solid-colour quad using the same world plane.
/// </summary>
public static class WorldTextMeshBuilder
{
    /// <summary>Reference nameplate world scale (blocks per font pixel).</summary>
    public const float NameplateScale = 0.025f;

    /// <summary>Builds a billboard nameplate: background then glyphs, centered on <paramref name="anchor"/>.</summary>
    public static List<WorldTextDraw> BuildNameplate(
        string text,
        BitmapFont font,
        Vector3 anchor,
        Vector3 right,
        Vector3 up,
        uint textColour,
        uint backgroundColour)
    {
        var result = new List<WorldTextDraw>(2);
        var width = font.MeasureWidth(text);
        if (width <= 0)
        {
            return result;
        }

        var glyphScale = font.Scale;
        var glyphHeight = font.CellHeight * glyphScale;
        var left = -width * 0.5f;
        var background = new List<float>(36);
        var backgroundIndices = new List<uint>(6);
        AddQuad(
            background,
            backgroundIndices,
            anchor,
            right,
            up,
            left - 1f,
            -1f,
            left + width,
            glyphHeight + 1f,
            -1f,
            -1f,
            backgroundColour);
        result.Add(new WorldTextDraw([.. background], [.. backgroundIndices]));

        var textVertices = new List<float>(width * 36);
        var textIndices = new List<uint>();
        var pen = left;
        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (font.TryGet(character, out var glyph))
            {
                var (u0, v0, u1, v1) = font.Uv(glyph);
                var uRight = u1;
                AddQuad(
                    textVertices,
                    textIndices,
                    anchor,
                    right,
                    up,
                    pen,
                    0f,
                    pen + (font.CellWidth * glyphScale),
                    glyphHeight,
                    u0,
                    v0,
                    textColour,
                    uRight,
                    v1);
            }

            pen += font.AdvanceOf(character);
        }

        result.Add(new WorldTextDraw([.. textVertices], [.. textIndices]));
        return result;
    }

    private static void AddQuad(
        List<float> vertices,
        List<uint> indices,
        Vector3 anchor,
        Vector3 right,
        Vector3 up,
        float x0,
        float y0,
        float x1,
        float y1,
        float u0,
        float v0,
        uint colour,
        float u1 = -1f,
        float v1 = -1f)
    {
        var index = (uint)(vertices.Count / 9);
        AddVertex(vertices, anchor + (right * (x0 * NameplateScale)) + (up * (-y0 * NameplateScale)), u0, v0, colour);
        AddVertex(vertices, anchor + (right * (x0 * NameplateScale)) + (up * (-y1 * NameplateScale)), u0, v1, colour);
        AddVertex(vertices, anchor + (right * (x1 * NameplateScale)) + (up * (-y1 * NameplateScale)), u1, v1, colour);
        AddVertex(vertices, anchor + (right * (x1 * NameplateScale)) + (up * (-y0 * NameplateScale)), u1, v0, colour);
        indices.Add(index);
        indices.Add(index + 1);
        indices.Add(index + 2);
        indices.Add(index);
        indices.Add(index + 2);
        indices.Add(index + 3);
    }

    private static void AddVertex(List<float> vertices, Vector3 position, float u, float v, uint colour)
    {
        vertices.Add(position.X);
        vertices.Add(position.Y);
        vertices.Add(position.Z);
        vertices.Add(u);
        vertices.Add(v);
        vertices.Add(((colour >> 16) & 0xFF) / 255f);
        vertices.Add(((colour >> 8) & 0xFF) / 255f);
        vertices.Add((colour & 0xFF) / 255f);
        vertices.Add(((colour >> 24) & 0xFF) / 255f);
    }
}
