// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Identifiers;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Which blocks take a biome tint, and how widely.
/// </summary>
/// <remarks>
/// The reference registers a tint source per block and applies it to the faces whose model carries a tint index. Our models
/// still resolve to plain sprites, so the table records the block level decision plus the face rule: grass block tints only
/// its top, plants and leaves tint every face, water is tinted everywhere.
/// </remarks>
public static class BlockTintTable
{
    private static readonly HashSet<string> GrassBlocks = new(StringComparer.Ordinal)
    {
        "minecraft:grass_block"
    };

    private static readonly string[] GrassPlants =
    [
        "short_grass", "tall_grass", "fern", "large_fern", "potted_fern", "bush"
    ];

    private static readonly string[] FoliageLeaves =
    [
        "oak_leaves", "jungle_leaves", "acacia_leaves", "dark_oak_leaves", "mangrove_leaves", "pale_oak_leaves"
    ];

    /// <summary>Tint kind for a block.</summary>
    /// <param name="block">Block identifier.</param>
    public static BlockTintKind KindOf(Identifier block)
    {
        var path = block.Path;

        if (GrassBlocks.Contains(block.ToString()))
        {
            return BlockTintKind.Grass;
        }

        foreach (var name in GrassPlants)
        {
            if (path == name)
            {
                return BlockTintKind.Grass;
            }
        }

        foreach (var name in FoliageLeaves)
        {
            if (path == name)
            {
                return BlockTintKind.Foliage;
            }
        }

        return path switch
        {
            "spruce_leaves" => BlockTintKind.FoliageEvergreen,
            "birch_leaves" => BlockTintKind.FoliageBirch,
            "water" => BlockTintKind.Water,
            _ => BlockTintKind.None
        };
    }

    /// <summary>
    /// True when this kind tints only the upward face, as the reference grass block does.
    /// </summary>
    /// <param name="kind">Tint kind of the block.</param>
    public static bool TintsTopFaceOnly(BlockTintKind kind) => kind == BlockTintKind.Grass;
}
