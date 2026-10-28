// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Common.Coordinates;

/// <summary>
/// Coordinate conversions and rounding with Minecraft 26.3 semantics
/// (docs/analysis/common/coordinates.md).
/// </summary>
/// <remarks>
/// The trap here is negative numbers: Java uses arithmetic shifts and floor division,
/// while C#'s <c>/</c> truncates toward zero. Always go through this type instead of
/// hand-writing <c>/ 16</c> or an <c>(int)</c> cast.
/// </remarks>
public static class BlockCoordinates
{
    /// <summary>Bits per section/chunk axis.</summary>
    public const int SectionBits = 4;

    /// <summary>Blocks per section/chunk axis.</summary>
    public const int SectionSize = 1 << SectionBits;

    /// <summary>Mask for in-section coordinates.</summary>
    public const int SectionMask = SectionSize - 1;

    /// <summary>Block coordinate to chunk/section coordinate (arithmetic shift, floor-like).</summary>
    public static int ToSectionCoord(int blockCoord) => blockCoord >> SectionBits;

    /// <summary>Double coordinate to chunk/section coordinate (floor first, then shift).</summary>
    public static int ToSectionCoord(double coord) => Floor(coord) >> SectionBits;

    /// <summary>In-section coordinate in <c>[0, 15]</c>, correct for negatives.</summary>
    public static int SectionRelative(int blockCoord) => blockCoord & SectionMask;

    /// <summary>Chunk/section coordinate to its minimum block coordinate.</summary>
    public static int ToMinBlockCoord(int sectionCoord) => sectionCoord << SectionBits;

    /// <summary>Flooring double to int (never truncates toward zero).</summary>
    public static int Floor(double value) => (int)Math.Floor(value);

    /// <summary>Flooring division; C#'s <c>/</c> truncates and would be wrong here.</summary>
    public static int FloorDiv(int dividend, int divisor)
    {
        var quotient = dividend / divisor;
        return (dividend % divisor != 0) && ((dividend < 0) != (divisor < 0)) ? quotient - 1 : quotient;
    }

    /// <summary>Flooring division for long values.</summary>
    public static long FloorDiv(long dividend, long divisor)
    {
        var quotient = dividend / divisor;
        return (dividend % divisor != 0) && ((dividend < 0) != (divisor < 0)) ? quotient - 1 : quotient;
    }
}
