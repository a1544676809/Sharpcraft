// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Common.World;

public enum BlockFace
{
    Down = 0,
    Up = 1,
    North = 2,
    South = 3,
    West = 4,
    East = 5,
}

public static class BlockFaceExtensions
{
    public static BlockFace Opposite(this BlockFace face) => face switch
    {
        BlockFace.Down => BlockFace.Up,
        BlockFace.Up => BlockFace.Down,
        BlockFace.North => BlockFace.South,
        BlockFace.South => BlockFace.North,
        BlockFace.West => BlockFace.East,
        BlockFace.East => BlockFace.West,
        _ => face,
    };
}

public readonly record struct OcclusionShape(
    double MinX,
    double MinY,
    double MinZ,
    double MaxX,
    double MaxY,
    double MaxZ,
    bool IsEmpty)
{
    public const float MaxFluidHeight = 0.8888889f;

    public const double Epsilon = 1.0e-7;

    public static OcclusionShape Empty => new(0, 0, 0, 0, 0, 0, IsEmpty: true);

    public static OcclusionShape FullBlock => new(0, 0, 0, 1, 1, 1, IsEmpty: false);

    public static OcclusionShape BottomSlab => Box(0, 0, 0, 1, 0.5, 1);

    public static OcclusionShape TopSlab => Box(0, 0.5, 0, 1, 1, 1);

    public bool IsFullBlock => !IsEmpty
        && FuzzyEquals(MinX, 0)
        && FuzzyEquals(MinY, 0)
        && FuzzyEquals(MinZ, 0)
        && FuzzyEquals(MaxX, 1)
        && FuzzyEquals(MaxY, 1)
        && FuzzyEquals(MaxZ, 1);

    public static OcclusionShape Box(double minX, double minY, double minZ, double maxX, double maxY, double maxZ)
    {
        // A degenerate extent collapses the box to the empty shape.
        if (maxX - minX <= Epsilon || maxY - minY <= Epsilon || maxZ - minZ <= Epsilon)
        {
            return Empty;
        }

        return new OcclusionShape(minX, minY, minZ, maxX, maxY, maxZ, false);
    }

    // Reproduces Vanilla face-occlusion boundary contact plus 2D cross-section containment.
    public static bool BlockOccludes(OcclusionShape shape, OcclusionShape occluder, BlockFace direction)
    {
        if (occluder.IsEmpty)
        {
            return false;
        }

        if (shape.IsFullBlock && occluder.IsFullBlock)
        {
            return true;
        }

        if (shape.IsEmpty)
        {
            return true;
        }

        return direction switch
        {
            BlockFace.Up => FuzzyEquals(shape.MaxY, 1.0)
                && FuzzyEquals(occluder.MinY, 0.0)
                && CoversXZ(shape, occluder),
            BlockFace.Down => FuzzyEquals(occluder.MaxY, 1.0)
                && FuzzyEquals(shape.MinY, 0.0)
                && CoversXZ(shape, occluder),
            BlockFace.South => FuzzyEquals(shape.MaxZ, 1.0)
                && FuzzyEquals(occluder.MinZ, 0.0)
                && CoversXY(shape, occluder),
            BlockFace.North => FuzzyEquals(occluder.MaxZ, 1.0)
                && FuzzyEquals(shape.MinZ, 0.0)
                && CoversXY(shape, occluder),
            BlockFace.East => FuzzyEquals(shape.MaxX, 1.0)
                && FuzzyEquals(occluder.MinX, 0.0)
                && CoversYZ(shape, occluder),
            BlockFace.West => FuzzyEquals(occluder.MaxX, 1.0)
                && FuzzyEquals(shape.MinX, 0.0)
                && CoversYZ(shape, occluder),
            _ => false,
        };
    }

    // Full blocks occlude every face except a not-full-height fluid top; other shapes use the box test.
    public static bool IsFaceOccludedByState(BlockFace direction, float height, OcclusionShape occluder)
    {
        if (occluder.IsEmpty)
        {
            return false;
        }

        if (occluder.IsFullBlock)
        {
            return direction != BlockFace.Up || height == 1.0f;
        }

        OcclusionShape shape = Box(0.0, 0.0, 0.0, 1.0, height, 1.0);
        return BlockOccludes(shape, occluder, direction);
    }

    public static bool IsFaceOccludedBySelf(OcclusionShape selfShape, BlockFace direction) =>
        IsFaceOccludedByState(direction.Opposite(), 1.0f, selfShape);

    private static bool FuzzyEquals(double a, double b) => System.Math.Abs(a - b) <= Epsilon;

    private static bool CoversXZ(OcclusionShape shape, OcclusionShape occluder) =>
        shape.MinX >= occluder.MinX - Epsilon
        && shape.MaxX <= occluder.MaxX + Epsilon
        && shape.MinZ >= occluder.MinZ - Epsilon
        && shape.MaxZ <= occluder.MaxZ + Epsilon;

    private static bool CoversXY(OcclusionShape shape, OcclusionShape occluder) =>
        shape.MinX >= occluder.MinX - Epsilon
        && shape.MaxX <= occluder.MaxX + Epsilon
        && shape.MinY >= occluder.MinY - Epsilon
        && shape.MaxY <= occluder.MaxY + Epsilon;

    private static bool CoversYZ(OcclusionShape shape, OcclusionShape occluder) =>
        shape.MinY >= occluder.MinY - Epsilon
        && shape.MaxY <= occluder.MaxY + Epsilon
        && shape.MinZ >= occluder.MinZ - Epsilon
        && shape.MaxZ <= occluder.MaxZ + Epsilon;
}
