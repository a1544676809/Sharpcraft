// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Reference;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Works out which block is the other half of a double chest. The reference walks a single
/// direction: a left half joins clockwise, a right half counter-clockwise, and only a neighbour of
/// the same block with the complementary type facing the same way counts.
/// </summary>
[ReferenceClass("net.minecraft.world.level.block.ChestBlock", "ref://mc-26.3-analysis/src/net/minecraft/world/level/block/ChestBlock.java", Status = ReferenceStatus.Partial)]
public static class ChestPairing
{
    /// <summary>Clockwise neighbour facing: north to east, east to south, south to west, west to north.</summary>
    /// <param name="facing">Facing id used by the block state reader (0 west, 1 east, 4 north, 5 south).</param>
    public static byte Clockwise(byte facing) => facing switch { 4 => 1, 1 => 5, 5 => 0, 0 => 4, _ => facing };

    /// <summary>Counter-clockwise neighbour facing, the inverse of <see cref="Clockwise"/>.</summary>
    /// <param name="facing">Facing id used by the block state reader.</param>
    public static byte CounterClockwise(byte facing) => facing switch { 4 => 0, 0 => 5, 5 => 1, 1 => 4, _ => facing };

    /// <summary>Offset from a chest to the half it connects to, or zero for a single chest.</summary>
    /// <param name="facing">Facing id used by the block state reader.</param>
    /// <param name="chestType">Zero single, one left half, two right half.</param>
    public static (int Dx, int Dz) ConnectedOffset(byte facing, byte chestType)
    {
        if (chestType == 0)
        {
            return (0, 0);
        }

        var direction = chestType == 1 ? Clockwise(facing) : CounterClockwise(facing);
        return direction switch
        {
            0 => (-1, 0),
            1 => (1, 0),
            4 => (0, -1),
            5 => (0, 1),
            _ => (0, 0)
        };
    }

    /// <summary>Whether a neighbour can be the other half of this chest.</summary>
    /// <param name="facing">Facing id of this chest.</param>
    /// <param name="chestType">Type of this chest.</param>
    /// <param name="otherFacing">Facing id of the neighbour.</param>
    /// <param name="otherChestType">Type of the neighbour.</param>
    public static bool IsPartner(byte facing, byte chestType, byte otherFacing, byte otherChestType) =>
        chestType != 0
        && otherChestType != 0
        && otherChestType != chestType
        && otherFacing == facing;
}
