// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.World;
using Sharpcraft.Common.World.Blocks;

namespace Sharpcraft.Common.Tests;

/// <summary>
/// The generated block to type index has to agree with the reference's block entity registration:
/// one entry per block, not per type, so signs, banners and skulls resolve through their own block
/// paths. Issue 96.
/// </summary>
public sealed class BlockEntityBlockIndexTests
{
    [Theory]
    [InlineData("chest", VanillaBlockEntityTypes.Chest)]
    [InlineData("trapped_chest", VanillaBlockEntityTypes.TrappedChest)]
    [InlineData("ender_chest", VanillaBlockEntityTypes.EnderChest)]
    [InlineData("waxed_oxidized_copper_chest", VanillaBlockEntityTypes.Chest)]
    [InlineData("furnace", VanillaBlockEntityTypes.Furnace)]
    [InlineData("enchanting_table", VanillaBlockEntityTypes.EnchantingTable)]
    [InlineData("skeleton_skull", VanillaBlockEntityTypes.Skull)]
    [InlineData("skeleton_wall_skull", VanillaBlockEntityTypes.Skull)]
    [InlineData("white_banner", VanillaBlockEntityTypes.Banner)]
    [InlineData("black_wall_banner", VanillaBlockEntityTypes.Banner)]
    [InlineData("shulker_box", VanillaBlockEntityTypes.ShulkerBox)]
    [InlineData("purple_shulker_box", VanillaBlockEntityTypes.ShulkerBox)]
    [InlineData("conduit", VanillaBlockEntityTypes.Conduit)]
    [InlineData("lectern", VanillaBlockEntityTypes.Lectern)]
    [InlineData("bell", VanillaBlockEntityTypes.Bell)]
    [InlineData("campfire", VanillaBlockEntityTypes.Campfire)]
    [InlineData("oak_shelf", VanillaBlockEntityTypes.Shelf)]
    [InlineData("suspicious_sand", VanillaBlockEntityTypes.BrushableBlock)]
    [InlineData("decorated_pot", VanillaBlockEntityTypes.DecoratedPot)]
    [InlineData("vault", VanillaBlockEntityTypes.Vault)]
    [InlineData("copper_golem_statue", VanillaBlockEntityTypes.CopperGolemStatue)]
    [InlineData("oak_sign", 7)]
    [InlineData("acacia_sign", 7)]
    [InlineData("oak_wall_sign", 7)]
    [InlineData("oak_hanging_sign", 8)]
    [InlineData("chiseled_bookshelf", 38)]
    [InlineData("spawner", 9)]
    [InlineData("moving_piston", 11)]
    [InlineData("beacon", 15)]
    public void ABlockThatCarriesABlockEntityResolves(string blockPath, int expected)
    {
        Assert.True(BlockEntityBlockIndex.TryGetType(blockPath, out var typeId));
        Assert.Equal(expected, typeId);
    }

    [Theory]
    [InlineData("bookshelf")]
    [InlineData("stone")]
    [InlineData("oak_planks")]
    [InlineData("piston")]
    [InlineData("sticky_piston")]
    [InlineData("sign")]
    [InlineData("banner")]
    [InlineData("skull")]
    [InlineData("mob_spawner")]
    [InlineData("air")]
    public void ABlockWithoutABlockEntityDoesNotResolve(string blockPath) =>
        Assert.False(BlockEntityBlockIndex.TryGetType(blockPath, out _));

    [Fact]
    public void TheIndexCoversEveryTypeThatHasBlockEntityBearingBlocks()
    {
        Assert.Equal(191, BlockEntityBlockIndex.Count);
        var seen = new HashSet<int>();
        foreach (var path in BlockEntityBlockIndex.BlockPaths)
        {
            Assert.True(BlockEntityBlockIndex.TryGetType(path, out var typeId));
            seen.Add(typeId);
        }

        // Every registered type except the ones no block carries (none today) must appear.
        Assert.Equal(VanillaBlockEntityTypes.Count, seen.Count);
    }

    [Fact]
    public void ThePathsAreSortedAndUnique()
    {
        var paths = BlockEntityBlockIndex.BlockPaths;
        for (var index = 1; index < paths.Count; index++)
        {
            Assert.True(
                string.CompareOrdinal(paths[index - 1], paths[index]) < 0,
                $"{paths[index - 1]} and {paths[index]} are out of order or duplicated");
        }
    }

    /// <summary>The index describes minecraft blocks only, so a modded path never borrows a record.</summary>
    [Fact]
    public void AnIdentifierOutsideTheMinecraftNamespaceDoesNotResolve()
    {
        Assert.True(BlockEntityBlockIndex.TryGetType(Sharpcraft.Common.Identifiers.Identifier.Parse("minecraft:chest"), out var vanilla));
        Assert.False(BlockEntityBlockIndex.TryGetType(Sharpcraft.Common.Identifiers.Identifier.Parse("mymod:chest"), out var modded));
        Assert.Equal(-1, modded);
        Assert.True(vanilla >= 0);
    }
}
