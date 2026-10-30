// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Identifiers;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Which blocks are drawn in the translucent layer.
/// </summary>
/// <remarks>
/// The reference gets this from the block registry; we do not have that data extracted yet, so this is a small curated table
/// of the blocks that visibly need blending. It is explicitly temporary: when the registry is available the table is
/// deleted and the layer is read from it (docs/specs/render/pipeline.md 15).
/// </remarks>
public static class BlockRenderLayers
{
    private static readonly HashSet<string> TranslucentNames = new(StringComparer.Ordinal)
    {
        "minecraft:water",
        "minecraft:ice",
        "minecraft:blue_ice",
        "minecraft:packed_ice",
        "minecraft:glass",
        "minecraft:glass_pane",
        "minecraft:tinted_glass",
        "minecraft:slime_block",
        "minecraft:honey_block",
        "minecraft:nether_portal"
    };

    private static readonly string[] TranslucentSuffixes = ["_stained_glass", "_stained_glass_pane"];

    private static readonly HashSet<string> CutoutNames = new(StringComparer.Ordinal)
    {
        "minecraft:short_grass",
        "minecraft:tall_grass",
        "minecraft:fern",
        "minecraft:large_fern",
        "minecraft:dead_bush",
        "minecraft:vine",
        "minecraft:ladder",
        "minecraft:cobweb",
        "minecraft:sugar_cane",
        "minecraft:wheat",
        "minecraft:torch",
        "minecraft:wall_torch",
        "minecraft:soul_torch",
        "minecraft:iron_bars",
        "minecraft:glass_pane",
        "minecraft:white_stained_glass_pane"
    };

    private static readonly string[] CutoutSuffixes =
    [
        "_leaves",
        "_sapling",
        "_flower",
        "_tulip",
        "_orchid",
        "_blossom",
        "_roots",
        "_mushroom",
        "_coral",
        "_carpet",
        "_pane"
    ];

    /// <summary>
    /// True when the block's model declares the cutout render type.
    /// </summary>
    /// <param name="block">Block identifier.</param>
    /// <remarks>
    /// The reference reads this from the model's declared render type, not from the texture: leaf textures in current packs
    /// can be fully opaque and are still cut out. This table is the same kind of stand-in as the translucent list, to be
    /// replaced once model render types are parsed (docs/specs/render/block-layer-and-tint.md).
    /// </remarks>
    public static bool IsCutout(Identifier block)
    {
        var name = block.ToString();

        if (CutoutNames.Contains(name))
        {
            return true;
        }

        foreach (var suffix in CutoutSuffixes)
        {
            if (name.EndsWith(suffix, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>True when the block belongs in the translucent layer.</summary>
    /// <param name="block">Block identifier.</param>
    public static bool IsTranslucent(Identifier block)
    {
        var name = block.ToString();

        if (TranslucentNames.Contains(name))
        {
            return true;
        }

        foreach (var suffix in TranslucentSuffixes)
        {
            if (name.EndsWith(suffix, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>True when a global state id belongs in the translucent layer.</summary>
    /// <param name="stateId">Global block state id.</param>
    /// <param name="layout">Layout used to name the state.</param>
    public static bool IsTranslucent(int stateId, Sharpcraft.Common.Registries.BlockStateLayout? layout)
    {
        if (layout?.BlockAt(stateId) is { } block)
        {
            return IsTranslucent(block);
        }

        return false;
    }
}
