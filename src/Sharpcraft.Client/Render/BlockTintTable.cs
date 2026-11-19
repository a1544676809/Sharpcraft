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

    /// <summary>
    /// The colour multiplier a block's tinted faces are drawn with, or null when the block takes no tint.
    /// </summary>
    /// <param name="tint">Biome tint, or null when the caller has none.</param>
    /// <param name="block">Block identifier the state resolves to.</param>
    /// <param name="x">Block X, for the biome lookup.</param>
    /// <param name="y">Block Y, for the biome lookup.</param>
    /// <param name="z">Block Z, for the biome lookup.</param>
    /// <remarks>
    /// This is the single answer to "is this block tinted, and with what colour": the kind lookup, the biome blend and
    /// the reference's channel unpacking all live here, so both mesh paths and the fluid path agree by construction and
    /// a test can pin the decision without a GPU. What stays per path is only the <em>face rule</em>, because the
    /// reference really does differ: the element path honours each face's model tint index, the cube path uses the
    /// per-block rule below, and fluid is always water. Issue 227 was the element path not asking at all.
    /// </remarks>
    public static (float R, float G, float B)? MultiplierFor(BiomeTint? tint, Identifier block, int x, int y, int z) =>
        MultiplierForKind(tint, KindOf(block), x, y, z);

    /// <summary>
    /// The same multiplier for a block the cube path draws, applying the reference's top-face-only rule.
    /// </summary>
    /// <param name="tint">Biome tint, or null when the caller has none.</param>
    /// <param name="block">Block identifier the state resolves to.</param>
    /// <param name="x">Block X, for the biome lookup.</param>
    /// <param name="y">Block Y, for the biome lookup.</param>
    /// <param name="z">Block Z, for the biome lookup.</param>
    /// <param name="upward">Whether the face being drawn looks up.</param>
    /// <returns>The multiplier, or null when this face takes no tint.</returns>
    public static (float R, float G, float B)? MultiplierForCubeFace(
        BiomeTint? tint,
        Identifier block,
        int x,
        int y,
        int z,
        bool upward)
    {
        var kind = KindOf(block);
        if (kind != BlockTintKind.None && TintsTopFaceOnly(kind) && !upward)
        {
            return null;
        }

        return MultiplierForKind(tint, kind, x, y, z);
    }

    /// <summary>
    /// The multiplier for a caller that already knows the tint kind, which is the fluid path: water is water.
    /// </summary>
    /// <param name="tint">Biome tint, or null when the caller has none.</param>
    /// <param name="kind">Tint kind to resolve.</param>
    /// <param name="x">Block X, for the biome lookup.</param>
    /// <param name="y">Block Y, for the biome lookup.</param>
    /// <param name="z">Block Z, for the biome lookup.</param>
    public static (float R, float G, float B)? MultiplierForKind(BiomeTint? tint, BlockTintKind kind, int x, int y, int z) =>
        tint is null || kind == BlockTintKind.None
            ? null
            : BiomeTint.ToMultiplier(tint.ColourAt(kind, x, y, z));
}
