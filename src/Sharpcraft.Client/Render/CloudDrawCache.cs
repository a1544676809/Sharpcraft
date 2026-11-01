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
    private int _vertical = int.MinValue;
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
        float verticalOffset,
        CloudCameraSide side)
    {
        var vertical = (int)MathF.Round(verticalOffset);
        if (_vertices.Length != 0 && cellX == _cellX && cellZ == _cellZ && side == _side && radiusCells == _radius
            && vertical == _vertical)
        {
            return (_vertices, _indices);
        }

        var quads = CloudMeshBuilder.Build(cells, textureWidth, textureHeight, cellX, cellZ, radiusCells, side);
        (_vertices, _indices) = CloudGeometryBuilder.Build(quads, Vector3.Zero, new Vector3(0f, vertical, 0f));
        _cellX = cellX;
        _cellZ = cellZ;
        _side = side;
        _radius = radiusCells;
        _vertical = vertical;
        return (_vertices, _indices);
    }
}
