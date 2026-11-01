// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Turns the visible cloud faces (CloudMeshBuilder) into interleaved position(3)+colour(4) vertices for the
/// position-colour pipeline, matching CloudRenderer's cell size (12), thickness (4) and face brightness
/// (docs/modules/clouds.md 2). Position is absolute world space; the draw's tint supplies the cloud colour.
/// </summary>
public static class CloudGeometryBuilder
{
    // Face vertex offsets in the reference order (CloudRenderer.buildMesh / clouds.vsh vertex table).
    private static readonly Vector3[][] FaceVertices =
    [
        // Down
        [new(1, 0, 0), new(1, 0, 1), new(0, 0, 1), new(0, 0, 0)],
        // Up
        [new(0, 1, 0), new(0, 1, 1), new(1, 1, 1), new(1, 1, 0)],
        // North
        [new(0, 0, 0), new(0, 1, 0), new(1, 1, 0), new(1, 0, 0)],
        // South
        [new(1, 0, 1), new(1, 1, 1), new(0, 1, 1), new(0, 0, 1)],
        // West
        [new(0, 0, 1), new(0, 1, 1), new(0, 1, 0), new(0, 0, 0)],
        // East
        [new(1, 0, 0), new(1, 1, 0), new(1, 1, 1), new(1, 0, 1)]
    ];

    // Face brightness from clouds.vsh faceColors (bottom 0.7, top 1.0, north/south 0.8, west/east 0.9).
    private static readonly float[] FaceBrightness = [0.7f, 1.0f, 0.8f, 0.8f, 0.9f, 0.9f];

    /// <summary>Builds the geometry for the given visible faces.</summary>
    /// <param name="quads">Visible faces from <see cref="CloudMeshBuilder.Build"/>.</param>
    /// <param name="eye">Camera position, for the cloud fog fade.</param>
    /// <param name="origin">World position of cell (0,0) face vertex (0,0,0) at the layer's bottom.</param>
    /// <param name="cellSize">Horizontal cell size in blocks (12).</param>
    /// <param name="thickness">Cloud thickness in blocks (4).</param>
    /// <param name="fogEnd">Cloud fog end distance (2048); alpha fades linearly to it.</param>
    public static (float[] Vertices, uint[] Indices) Build(
        IReadOnlyList<CloudQuad> quads,
        Vector3 eye,
        Vector3 origin,
        float cellSize = 12f,
        float thickness = 4f,
        float fogEnd = 2048f)
    {
        ArgumentNullException.ThrowIfNull(quads);

        var vertices = new float[quads.Count * 4 * 7];
        var indices = new uint[quads.Count * 6];
        var vertexCursor = 0;
        var indexCursor = 0;
        uint baseVertex = 0;

        foreach (var quad in quads)
        {
            var faces = FaceVertices[(int)quad.Direction];
            var brightness = FaceBrightness[(int)quad.Direction];
            var cellOffsetX = quad.CellX * cellSize;
            var cellOffsetZ = quad.CellZ * cellSize;

            for (var i = 0; i < 4; i++)
            {
                var face = faces[i];
                var x = origin.X + cellOffsetX + (face.X * cellSize);
                var y = origin.Y + (face.Y * thickness);
                var z = origin.Z + cellOffsetZ + (face.Z * cellSize);

                var distance = Vector3.Distance(new Vector3(x, y, z), eye);
                var fog = Math.Clamp(1f - (distance / fogEnd), 0f, 1f);

                vertices[vertexCursor++] = x;
                vertices[vertexCursor++] = y;
                vertices[vertexCursor++] = z;
                vertices[vertexCursor++] = brightness;
                vertices[vertexCursor++] = brightness;
                vertices[vertexCursor++] = brightness;
                vertices[vertexCursor++] = fog;
            }

            indices[indexCursor++] = baseVertex;
            indices[indexCursor++] = baseVertex + 1;
            indices[indexCursor++] = baseVertex + 2;
            indices[indexCursor++] = baseVertex;
            indices[indexCursor++] = baseVertex + 2;
            indices[indexCursor++] = baseVertex + 3;
            baseVertex += 4;
        }

        return (vertices, indices);
    }
}
