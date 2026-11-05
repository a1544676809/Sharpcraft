// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Client.World;

/// <summary>
/// Eight equipment slot item ids (mainhand, offhand, feet, legs, chest, head, body, saddle), with -1 for empty.
/// Kept as a value type so a per-frame render state carries it without allocating.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.world.entity.EntityEquipment", "ref://mc-26.3-analysis/src/net/minecraft/world/entity/EntityEquipment.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "auto-resolved by name; verify")]
public readonly record struct EntityEquipment(
    int MainHand,
    int OffHand,
    int Feet,
    int Legs,
    int Chest,
    int Head,
    int Body,
    int Saddle)
{
    public int this[int slot] => slot switch
    {
        0 => MainHand,
        1 => OffHand,
        2 => Feet,
        3 => Legs,
        4 => Chest,
        5 => Head,
        6 => Body,
        7 => Saddle,
        _ => -1
    };
}
