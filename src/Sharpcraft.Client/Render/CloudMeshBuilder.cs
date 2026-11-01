// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.Render;

/// <summary>which faces of a cloud cell a camera can see, mirroring CloudRenderer.RelativeCameraPos.</summary>
public enum CloudCameraSide
{
    /// <summary>Camera is below the cloud layer (sees the bottom).</summary>
    AboveClouds,

    /// <summary>Camera is above the cloud layer (sees the top).</summary>
    BelowClouds,

    /// <summary>Camera is inside the cloud layer.</summary>
    InsideClouds
}

/// <summary>The six face directions a cloud cell can emit (matches Direction.get3DDataValue order).</summary>
public enum CloudFaceDirection
{
    Down = 0,
    Up = 1,
    North = 2,
    South = 3,
    West = 4,
    East = 5
}

/// <summary>One cloud face the renderer must draw: the cell (world cell coordinates) and the face direction.</summary>
/// <param name="CellX">Cell X in the cloud grid around the camera.</param>
/// <param name="CellZ">Cell Z in the cloud grid around the camera.</param>
/// <param name="Direction">Face direction.</param>
/// <param name="InsideFace">True for the extra inward-facing faces added within one cell of the camera.</param>
public readonly record struct CloudQuad(int CellX, int CellZ, CloudFaceDirection Direction, bool InsideFace);

/// <summary>
/// Builds the visible cloud faces around the camera, exactly like the reference CloudRenderer.buildMesh /
/// tryBuildCell / buildExtrudedCell (docs/modules/clouds.md 2).
/// </summary>
public static class CloudMeshBuilder
{
    // A cloud cell is empty when its texture alpha is below this (CloudRenderer.isCellEmpty).
    public const int EmptyAlphaThreshold = 10;

    /// <summary>Packs a cell's colour and its four neighbour-empty flags, mirroring CloudRenderer.packCellData.</summary>
    public static long PackCell(int argb, bool northEmpty, bool eastEmpty, bool southEmpty, bool westEmpty) =>
        ((long)argb << 4)
        | (northEmpty ? 1L << 3 : 0)
        | (eastEmpty ? 1L << 2 : 0)
        | (southEmpty ? 1L << 1 : 0)
        | (westEmpty ? 1L : 0);

    private static bool IsNorthEmpty(long cell) => ((cell >> 3) & 1) != 0;
    private static bool IsEastEmpty(long cell) => ((cell >> 2) & 1) != 0;
    private static bool IsSouthEmpty(long cell) => ((cell >> 1) & 1) != 0;
    private static bool IsWestEmpty(long cell) => (cell & 1) != 0;

    /// <summary>
    /// Collects the visible faces within <paramref name="radiusCells"/> of the camera cell, in the reference ring order.
    /// The caller adds the cloud offset/height when turning these into geometry.
    /// </summary>
    public static List<CloudQuad> Build(
        ReadOnlySpan<long> cells,
        int textureWidth,
        int textureHeight,
        int centerCellX,
        int centerCellZ,
        int radiusCells,
        CloudCameraSide side)
    {
        var quads = new List<CloudQuad>();

        for (var ring = 0; ring <= 2 * radiusCells; ring++)
        {
            for (var relativeCellX = -ring; relativeCellX <= ring; relativeCellX++)
            {
                var relativeCellZ = ring - Math.Abs(relativeCellX);
                if (relativeCellZ < 0 || relativeCellZ > radiusCells
                    || (relativeCellX * relativeCellX) + (relativeCellZ * relativeCellZ) > radiusCells * radiusCells)
                {
                    continue;
                }

                if (relativeCellZ != 0)
                {
                    TryBuildCell(quads, cells, textureWidth, textureHeight, centerCellX, centerCellZ, relativeCellX, -relativeCellZ, side);
                }

                TryBuildCell(quads, cells, textureWidth, textureHeight, centerCellX, centerCellZ, relativeCellX, relativeCellZ, side);
            }
        }

        return quads;
    }

    /// <summary>Fast clouds: only the down face of each non-empty cell (reference buildFlatCell).</summary>
    public static List<CloudQuad> BuildFlat(
        ReadOnlySpan<long> cells,
        int textureWidth,
        int textureHeight,
        int centerCellX,
        int centerCellZ,
        int radiusCells)
    {
        var quads = new List<CloudQuad>();

        for (var ring = 0; ring <= 2 * radiusCells; ring++)
        {
            for (var relativeCellX = -ring; relativeCellX <= ring; relativeCellX++)
            {
                var relativeCellZ = ring - Math.Abs(relativeCellX);
                if (relativeCellZ < 0 || relativeCellZ > radiusCells
                    || (relativeCellX * relativeCellX) + (relativeCellZ * relativeCellZ) > radiusCells * radiusCells)
                {
                    continue;
                }

                if (relativeCellZ != 0)
                {
                    TryAddFlat(quads, cells, textureWidth, textureHeight, centerCellX, centerCellZ, relativeCellX, -relativeCellZ);
                }

                TryAddFlat(quads, cells, textureWidth, textureHeight, centerCellX, centerCellZ, relativeCellX, relativeCellZ);
            }
        }

        return quads;
    }

    private static void TryAddFlat(
        List<CloudQuad> quads,
        ReadOnlySpan<long> cells,
        int textureWidth,
        int textureHeight,
        int centerCellX,
        int centerCellZ,
        int relativeCellX,
        int relativeCellZ)
    {
        var indexX = FloorMod(centerCellX + relativeCellX, textureWidth);
        var indexY = FloorMod(centerCellZ + relativeCellZ, textureHeight);
        if (cells[indexX + (indexY * textureWidth)] != 0L)
        {
            quads.Add(new CloudQuad(relativeCellX, relativeCellZ, CloudFaceDirection.Down, false));
        }
    }

    private static void TryBuildCell(
        List<CloudQuad> quads,
        ReadOnlySpan<long> cells,
        int textureWidth,
        int textureHeight,
        int centerCellX,
        int centerCellZ,
        int relativeCellX,
        int relativeCellZ,
        CloudCameraSide side)
    {
        var indexX = FloorMod(centerCellX + relativeCellX, textureWidth);
        var indexY = FloorMod(centerCellZ + relativeCellZ, textureHeight);
        var cell = cells[indexX + (indexY * textureWidth)];
        if (cell == 0L)
        {
            return;
        }

        if (side != CloudCameraSide.BelowClouds)
        {
            quads.Add(new CloudQuad(relativeCellX, relativeCellZ, CloudFaceDirection.Up, false));
        }

        if (side != CloudCameraSide.AboveClouds)
        {
            quads.Add(new CloudQuad(relativeCellX, relativeCellZ, CloudFaceDirection.Down, false));
        }

        if (IsNorthEmpty(cell) && relativeCellZ > 0)
        {
            quads.Add(new CloudQuad(relativeCellX, relativeCellZ, CloudFaceDirection.North, false));
        }

        if (IsSouthEmpty(cell) && relativeCellZ < 0)
        {
            quads.Add(new CloudQuad(relativeCellX, relativeCellZ, CloudFaceDirection.South, false));
        }

        if (IsWestEmpty(cell) && relativeCellX > 0)
        {
            quads.Add(new CloudQuad(relativeCellX, relativeCellZ, CloudFaceDirection.West, false));
        }

        if (IsEastEmpty(cell) && relativeCellX < 0)
        {
            quads.Add(new CloudQuad(relativeCellX, relativeCellZ, CloudFaceDirection.East, false));
        }

        if (Math.Abs(relativeCellX) <= 1 && Math.Abs(relativeCellZ) <= 1)
        {
            for (var direction = 0; direction < 6; direction++)
            {
                quads.Add(new CloudQuad(relativeCellX, relativeCellZ, (CloudFaceDirection)direction, true));
            }
        }
    }

    private static int FloorMod(int value, int modulus)
    {
        var result = value % modulus;
        return result < 0 ? result + modulus : result;
    }
}
