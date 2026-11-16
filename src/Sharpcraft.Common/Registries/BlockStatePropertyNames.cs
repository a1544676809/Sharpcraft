// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Common.Registries;

/// <summary>
/// Block state property names this project reads.
/// </summary>
/// <remarks>
/// These are interaction fields, not presentation details: a name that does not match the reference
/// makes the lookup miss and the caller fall back silently. The copper golem statue rendered every
/// pose as standing for exactly that reason, so the names live here once and a test checks them
/// against <see cref="VanillaBlockStatePropertyNames"/>.
/// </remarks>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.world.level.block.state.properties.BlockStateProperties", "ref://mc-26.3-analysis/src/net/minecraft/world/level/block/state/properties/BlockStateProperties.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "Block state property names; a mismatch makes lookups miss silently")]
public static class BlockStatePropertyNames
{
    /// <summary>Horizontal facing of a block that points somewhere.</summary>
    public const string Facing = "facing";

    /// <summary>Chest shape: single, left or right.</summary>
    public const string Type = "type";

    /// <summary>Sixteen step rotation of banners and signs.</summary>
    public const string Rotation = "rotation";

    /// <summary>How far a brushable block has been brushed.</summary>
    public const string Dusted = "dusted";

    /// <summary>Whether a lectern holds a book.</summary>
    public const string HasBook = "has_book";

    /// <summary>Copper golem statue pose.</summary>
    public const string CopperGolemPose = "copper_golem_pose";

    /// <summary>Every name in this class, for the check against the reference table.</summary>
    public static IReadOnlyList<string> All { get; } =
    [
        Facing,
        Type,
        Rotation,
        Dusted,
        HasBook,
        CopperGolemPose
    ];
}