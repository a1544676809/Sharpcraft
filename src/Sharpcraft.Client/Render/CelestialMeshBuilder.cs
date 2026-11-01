// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Builds the interleaved vertex/index arrays the celestial pipeline draws: position (xyz) + uv (uv) per corner, matching
/// the reference buildCelestialQuad UV assignment (docs/specs/render/sky-celestial.md 2-3).
/// </summary>
public static class CelestialMeshBuilder
{
    /// <summary>Floats per vertex: position (3) + uv (2).</summary>
    public const int FloatsPerVertex = 5;

    /// <summary>Indices per quad (two triangles).</summary>
    public const int IndicesPerQuad = 6;

    /// <summary>Builds one textured quad from four corners and a sprite UV rectangle (u0,v0,u1,v1).</summary>
    public static (float[] Vertices, ushort[] Indices) Quad(Vector3[] corners, (float U0, float V0, float U1, float V1) uv)
    {
        ArgumentNullException.ThrowIfNull(corners);
        if (corners.Length != 4)
        {
            throw new ArgumentException("a quad needs four corners", nameof(corners));
        }

        // Corner order is (-1,-1),(1,-1),(1,1),(-1,1); the reference maps UVs (u0,v0),(u1,v0),(u1,v1),(u0,v1).
        Span<(float U, float V)> uvs =
        [
            (uv.U0, uv.V0), (uv.U1, uv.V0), (uv.U1, uv.V1), (uv.U0, uv.V1)
        ];

        var vertices = new float[4 * FloatsPerVertex];
        for (var i = 0; i < 4; i++)
        {
            var offset = i * FloatsPerVertex;
            vertices[offset] = corners[i].X;
            vertices[offset + 1] = corners[i].Y;
            vertices[offset + 2] = corners[i].Z;
            vertices[offset + 3] = uvs[i].U;
            vertices[offset + 4] = uvs[i].V;
        }

        return (vertices, new ushort[] { 0, 1, 2, 0, 2, 3 });
    }

    /// <summary>Builds a sun/moon quad for an angle (radians), height and size.</summary>
    public static (float[] Vertices, ushort[] Indices) CelestialQuad(float angleRadians, float height, float size, (float U0, float V0, float U1, float V1) uv) =>
        Quad(CelestialGeometry.CelestialQuad(angleRadians, height, size), uv);
}
