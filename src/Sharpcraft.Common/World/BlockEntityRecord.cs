// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Nbt;

namespace Sharpcraft.Common.World;

/// <summary>
/// One block entity as it appears in the chunk packet, kept for the render stage.
/// </summary>
/// <param name="LocalX">Block X inside the chunk, zero to fifteen.</param>
/// <param name="Y">Absolute block Y.</param>
/// <param name="LocalZ">Block Z inside the chunk, zero to fifteen.</param>
/// <param name="TypeId">Registry id of the block entity type.</param>
/// <param name="Tag">Optional NBT compound carried with the block entity.</param>
public readonly record struct BlockEntityRecord(int LocalX, int Y, int LocalZ, int TypeId, NbtCompound? Tag = null);

/// <summary>
/// Built-in <c>minecraft:block_entity_type</c> registry order for Minecraft 26.3 (49 entries, ids 0..48).
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.world.level.block.entity.BlockEntityTypes", "ref://mc-26.3-analysis/src/net/minecraft/world/level/block/entity/BlockEntityTypes.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "Registry order and ids only; block entity behaviour not implemented")]
public static class VanillaBlockEntityTypes
{
    public const int Furnace = 0;
    public const int Chest = 1;
    public const int TrappedChest = 2;
    public const int EnderChest = 3;
    public const int ShulkerBox = 24;
    public const int Bell = 30;
    public const int DecoratedPot = 41;
    public const int CopperGolemStatue = 47;
    public const int PotentSulfur = 48;

    private static readonly string[] Paths =
    [
        "furnace", "chest", "trapped_chest", "ender_chest", "jukebox", "dispenser", "dropper",
        "sign", "hanging_sign", "mob_spawner", "creaking_heart", "piston", "brewing_stand",
        "enchanting_table", "end_portal", "beacon", "skull", "daylight_detector", "hopper",
        "comparator", "banner", "structure_block", "end_gateway", "command_block", "shulker_box",
        "conduit", "barrel", "smoker", "blast_furnace", "lectern", "bell", "jigsaw", "campfire",
        "beehive", "sculk_sensor", "calibrated_sculk_sensor", "sculk_catalyst", "sculk_shrieker",
        "chiseled_bookshelf", "shelf", "brushable_block", "decorated_pot", "crafter",
        "trial_spawner", "vault", "test_block", "test_instance_block", "copper_golem_statue",
        "potent_sulfur"
    ];

    public static int Count => Paths.Length;

    public static string? PathOf(int typeId) =>
        (uint)typeId < (uint)Paths.Length ? Paths[typeId] : null;

    /// <summary>
    /// Registry id for a block path. The eight copper chest blocks share the chest block entity
    /// type: the reference registers CHEST for <c>Blocks.COPPER_CHEST.asList()</c> plus
    /// <c>Blocks.CHEST</c>, so they are not separate entries in this table.
    /// </summary>
    /// <param name="path">Block id path, without the namespace.</param>
    public static int IdOf(string path) => path switch
    {
        "copper_chest" or "exposed_copper_chest" or "weathered_copper_chest" or "oxidized_copper_chest"
            or "waxed_copper_chest" or "waxed_exposed_copper_chest" or "waxed_weathered_copper_chest"
            or "waxed_oxidized_copper_chest" => Chest,
        _ => Array.IndexOf(Paths, path)
    };

    /// <summary>Chest-like types drawn by the chest renderer. Shulker boxes have their own renderer.</summary>
    public static bool IsChestLike(int typeId) =>
        typeId is Chest or TrappedChest or EnderChest;

    /// <summary>Shulker box type, drawn by its own two-box renderer rather than the chest one.</summary>
    public static bool IsShulkerBox(int typeId) => typeId == ShulkerBox;

    /// <summary>
    /// Whether a block entity's <c>facing</c> property is decoded as a reference
    /// <c>Direction</c> ordinal. Shulker boxes, decorated pots and copper golem statues use this
    /// encoding; chests use the face-step encoding instead. Sources:
    /// <c>ref://mc-26.3-analysis/src/net/minecraft/core/Direction.java:33-38,66-77,311-312</c>
    /// and <c>ref://mc-26.3-analysis/src/net/minecraft/world/level/block/entity/DecoratedPotBlockEntity.java:75-76</c>
    /// and <c>ref://mc-26.3-analysis/src/net/minecraft/world/level/block/CopperGolemStatueBlock.java:43-54</c>.
    /// </summary>
    public static bool UsesDirectionOrdinalFacing(int typeId) => typeId is ShulkerBox or DecoratedPot or CopperGolemStatue;

    /// <summary>
    /// Fallback Direction ordinal when the block state has no <c>facing</c> property:
    /// decorated pots default to NORTH (ordinal 2, the block's default state at
    /// <c>ref://mc-26.3-analysis/src/net/minecraft/world/level/block/DecoratedPotBlock.java:57</c>),
    /// copper golem statues default to NORTH too
    /// (<c>ref://mc-26.3-analysis/src/net/minecraft/world/level/block/CopperGolemStatueBlock.java:52-54</c>),
    /// shulker boxes keep their existing UP fallback (ordinal 1).
    /// </summary>
    public static byte DirectionOrdinalFallback(int typeId) => typeId is DecoratedPot or CopperGolemStatue ? (byte)2 : (byte)1;

    /// <summary>Copper golem statue block entity type, drawn by its own model renderer.</summary>
    public static bool IsCopperGolemStatue(int typeId) => typeId == CopperGolemStatue;
}
