// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Network;

namespace Sharpcraft.Common.Registries;

/// <summary>
/// Positional <c>banner_pattern</c> registry order for the current connection. The network <c>banner_patterns</c>
/// component carries pattern holder ids, so a server that reorders or appends entries must be reflected here instead of
/// always using the bootstrap order (reference <c>BannerPatterns.bootstrap</c>).
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.world.level.block.entity.BannerPatterns", "ref://mc-26.3-analysis/src/net/minecraft/world/level/block/entity/BannerPatterns.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Adapter, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "Banner pattern bootstrap order and asset id mapping; dynamic order from registry_data")]
public static class BannerPatternTable
{
    /// <summary>Bootstrap registration order of <c>minecraft:banner_pattern</c> entries.</summary>
    private static readonly string[] Bootstrap =
    [
        "base",
        "square_bottom_left",
        "square_bottom_right",
        "square_top_left",
        "square_top_right",
        "stripe_bottom",
        "stripe_top",
        "stripe_left",
        "stripe_right",
        "stripe_center",
        "stripe_middle",
        "stripe_downright",
        "stripe_downleft",
        "small_stripes",
        "cross",
        "straight_cross",
        "triangle_bottom",
        "triangle_top",
        "triangles_bottom",
        "triangles_top",
        "diagonal_left",
        "diagonal_right",
        "diagonal_up_left",
        "diagonal_up_right",
        "circle",
        "rhombus",
        "half_vertical",
        "half_horizontal",
        "half_vertical_right",
        "half_horizontal_bottom",
        "border",
        "gradient",
        "gradient_up",
        "bricks",
        "curly_border",
        "globe",
        "creeper",
        "skull",
        "flower",
        "mojang",
        "piglin",
        "flow",
        "guster"
    ];

    private static IReadOnlyList<string> _assets = Bootstrap;

    /// <summary>Pattern asset ids in the current connection's positional order.</summary>
    public static IReadOnlyList<string> Assets => _assets;

    /// <summary>Pattern asset id at a network holder id, or null when the id is out of range.</summary>
    public static string? AssetAt(int index) => (uint)index < (uint)_assets.Count ? _assets[index] : null;

    /// <summary>Atlas texture path for a pattern holder id, or null when the id is out of range.</summary>
    public static string? SpriteAt(int index) => AssetAt(index) is { } asset ? "entity/banner/" + asset : null;

    /// <summary>Rebuilds the positional table from one connection's configuration registries.</summary>
    public static void Configure(DynamicRegistries? registries)
    {
        var entries = registries?.Get(new Identifier("minecraft", "banner_pattern"));
        if (entries is null || entries.Count == 0)
        {
            _assets = Bootstrap;
            return;
        }

        var result = new List<string>(entries.Count);
        foreach (var entry in entries)
        {
            result.Add(entry.Id.Path);
        }

        _assets = result;
    }
}
