// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Common.Network;

/// <summary>
/// Decodes the block positions used by the update packets. Both packings are documented here because the bit
/// layout is not guessable: a block position keeps 26 bits for X, 12 for Y and 26 for Z, and a section keeps
/// 22/20/22, while the local offset inside a section puts X in the high bits
/// (docs/specs/network/play-movement.md 7.17).
/// </summary>
public static class BlockPositionCodec
{
    /// <summary>Unpacks a <c>BlockPos</c> packed long into world coordinates.</summary>
    /// <param name="packed">Packed value from the packet.</param>
    public static (int X, int Y, int Z) UnpackBlockPosition(long packed) =>
        ((int)(packed >> 38), (int)((packed << 52) >> 52), (int)((packed << 26) >> 38));

    /// <summary>Packs world coordinates the way the protocol does; used by tests and tooling.</summary>
    /// <param name="x">Block x.</param>
    /// <param name="y">Block y.</param>
    /// <param name="z">Block z.</param>
    public static long PackBlockPosition(int x, int y, int z) =>
        ((long)(x & 0x3FFFFFF) << 38) | ((long)(z & 0x3FFFFFF) << 12) | (long)(y & 0xFFF);

    /// <summary>Unpacks a <c>SectionPos</c> packed long into section coordinates.</summary>
    /// <param name="packed">Packed value from the packet.</param>
    public static (int X, int Y, int Z) UnpackSectionPosition(long packed) =>
        ((int)(packed >> 42), (int)((packed << 44) >> 44), (int)((packed << 22) >> 42));

    /// <summary>Packs section coordinates the way the protocol does.</summary>
    /// <param name="x">Section x.</param>
    /// <param name="y">Section y.</param>
    /// <param name="z">Section z.</param>
    public static long PackSectionPosition(int x, int y, int z) =>
        ((long)(x & 0x3FFFFF) << 42) | ((long)(z & 0x3FFFFF) << 20) | (long)(y & 0xFFFFF);

    /// <summary>
    /// Splits the local offset packed into a section update entry. X lives in the high four bits, Z in the
    /// middle four and Y in the low four, which is the opposite of what the wording suggests
    /// (<c>SectionPos.sectionRelativeX</c> shifts by eight).
    /// </summary>
    /// <param name="relative">Packed local offset, twelve bits.</param>
    public static (int X, int Y, int Z) UnpackSectionRelative(int relative) =>
        ((relative >> 8) & 15, relative & 15, (relative >> 4) & 15);
}
