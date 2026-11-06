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
public static class VanillaBlockEntityTypes
{
    public const int Furnace = 0;
    public const int Chest = 1;
    public const int TrappedChest = 2;
    public const int EnderChest = 3;
    public const int ShulkerBox = 24;
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

    public static int IdOf(string path) => Array.IndexOf(Paths, path);

    public static bool IsChestLike(int typeId) =>
        typeId is Chest or TrappedChest or EnderChest or ShulkerBox;
}
