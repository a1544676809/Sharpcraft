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

    /// <summary>
    /// Gets the camera-relative vertices/indices for the given centre cell, rebuilding only when needed. The geometry is
    /// built around the origin (y from 0..thickness) and fog alpha uses the distance from that origin, so the caller can
    /// place it with a translation and move/scroll it without touching the buffers.
    /// </summary>
    public (float[] Vertices, uint[] Indices) Get(
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
            return (_vertices, _indices);
        }

        var quads = CloudMeshBuilder.Build(cells, textureWidth, textureHeight, cellX, cellZ, radiusCells, side);
        (_vertices, _indices) = CloudGeometryBuilder.Build(quads, Vector3.Zero, Vector3.Zero);
        _cellX = cellX;
        _cellZ = cellZ;
        _side = side;
        _radius = radiusCells;
        return (_vertices, _indices);
    }
}
