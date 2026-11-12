// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Caches the camera-relative cloud mesh. The reference rebuilds only when the centre cell or camera side changes and
/// moves the whole mesh with a CloudOffset uniform; rebuilding ~cloudRange(128 chunks) of cells every frame is far too
/// expensive, so keep the vertices and let the caller supply the per-frame translation.
/// </summary>
public sealed class CloudDrawCache
{
    private int _cellX = int.MinValue;
    private int _cellZ = int.MinValue;
    private CloudCameraSide _side;
    private int _radius;
    private float[] _vertices = [];
    private uint[] _indices = [];
    private int[] _faces = [];

    /// <summary>
    /// Gets the camera-relative vertices/indices for the given centre cell, rebuilding only when needed. The geometry is
    /// built around the origin (y from 0..thickness) and fog alpha uses the distance from that origin, so the caller can
    /// place it with a translation and move/scroll it without touching the buffers.
    /// </summary>
    /// <remarks>
    /// The face table rides along in the same entry (T-827). It describes the same quads the vertices were expanded
    /// from, so it changes exactly when they do and costs nothing extra to keep; the procedural cloud passes read it
    /// instead of the vertex array.
    /// </remarks>
    public (float[] Vertices, uint[] Indices, int[] Faces) Get(
        ReadOnlySpan<long> cells,
        int textureWidth,
        int textureHeight,
        int cellX,
        int cellZ,
        int radiusCells,
        CloudCameraSide side)
    {
        if (_vertices.Length != 0 && cellX == _cellX && cellZ == _cellZ && side == _side && radiusCells == _radius)
        {
            return (_vertices, _indices, _faces);
        }

        var quads = CloudMeshBuilder.Build(cells, textureWidth, textureHeight, cellX, cellZ, radiusCells, side);
        (_vertices, _indices) = CloudGeometryBuilder.Build(quads, Vector3.Zero, Vector3.Zero);
        _faces = CloudFaceTable.Build(quads);
        _cellX = cellX;
        _cellZ = cellZ;
        _side = side;
        _radius = radiusCells;
        return (_vertices, _indices, _faces);
    }
}
