// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Common.Registries;

/// <summary>One bootstrap trim material: registry id and its palette id.</summary>
/// <param name="Id">Registry id, for example <c>minecraft:iron</c>.</param>
/// <param name="PaletteId">Palette id, for example <c>minecraft:trim/iron</c>.</param>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.world.item.equipment.trim.TrimMaterial", "ref://mc-26.3-analysis/src/net/minecraft/world/item/equipment/trim/TrimMaterial.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Full)]
public readonly record struct TrimMaterialDefinition(string Id, string PaletteId);

/// <summary>One bootstrap trim pattern: registry id, visual asset id and decal flag.</summary>
/// <param name="Id">Registry id, for example <c>minecraft:coast</c>.</param>
/// <param name="AssetId">Visual asset id, for example <c>minecraft:coast</c>.</param>
/// <param name="Decal">Whether the trim uses the decal render pipeline.</param>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.world.item.equipment.trim.TrimPattern", "ref://mc-26.3-analysis/src/net/minecraft/world/item/equipment/trim/TrimPattern.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Full)]
public readonly record struct TrimPatternDefinition(string Id, string AssetId, bool Decal);

/// <summary>
/// The 26.3 bootstrap order of trim materials. Dynamic registry data can override it later; the network component
/// carries holder ids, so this table is the fallback mapping from id to palette.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.world.item.equipment.trim.TrimMaterials", "ref://mc-26.3-analysis/src/net/minecraft/world/item/equipment/trim/TrimMaterials.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Full)]
public static class TrimMaterialRegistry
{
    /// <summary>Bootstrap order used by the default registry.</summary>
    public static readonly IReadOnlyList<TrimMaterialDefinition> Entries =
    [
        new("minecraft:quartz", "minecraft:trim/quartz"),
        new("minecraft:iron", "minecraft:trim/iron"),
        new("minecraft:netherite", "minecraft:trim/netherite"),
        new("minecraft:redstone", "minecraft:trim/redstone"),
        new("minecraft:copper", "minecraft:trim/copper"),
        new("minecraft:gold", "minecraft:trim/gold"),
        new("minecraft:emerald", "minecraft:trim/emerald"),
        new("minecraft:diamond", "minecraft:trim/diamond"),
        new("minecraft:lapis", "minecraft:trim/lapis"),
        new("minecraft:amethyst", "minecraft:trim/amethyst"),
        new("minecraft:resin", "minecraft:trim/resin")
    ];

    /// <summary>Registry id at <paramref name="index"/>, or null when the index is outside the bootstrap table.</summary>
    public static string? IdAt(int index) =>
        (uint)index < (uint)Entries.Count ? Entries[index].Id : null;

    /// <summary>Palette id for a registry id, or null when the material is unknown.</summary>
    public static string? PaletteIdFor(string id)
    {
        foreach (var entry in Entries)
        {
            if (string.Equals(entry.Id, id, StringComparison.Ordinal))
            {
                return entry.PaletteId;
            }
        }

        return null;
    }
}

/// <summary>The 26.3 bootstrap order of trim patterns; the same fallback rule as trim materials applies.</summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.world.item.equipment.trim.TrimPatterns", "ref://mc-26.3-analysis/src/net/minecraft/world/item/equipment/trim/TrimPatterns.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Full)]
public static class TrimPatternRegistry
{
    /// <summary>Bootstrap order used by the default registry; every 26.3 pattern currently has decal=false.</summary>
    public static readonly IReadOnlyList<TrimPatternDefinition> Entries =
    [
        new("minecraft:sentry", "minecraft:sentry", false),
        new("minecraft:dune", "minecraft:dune", false),
        new("minecraft:coast", "minecraft:coast", false),
        new("minecraft:wild", "minecraft:wild", false),
        new("minecraft:ward", "minecraft:ward", false),
        new("minecraft:eye", "minecraft:eye", false),
        new("minecraft:vex", "minecraft:vex", false),
        new("minecraft:tide", "minecraft:tide", false),
        new("minecraft:snout", "minecraft:snout", false),
        new("minecraft:rib", "minecraft:rib", false),
        new("minecraft:spire", "minecraft:spire", false),
        new("minecraft:wayfinder", "minecraft:wayfinder", false),
        new("minecraft:shaper", "minecraft:shaper", false),
        new("minecraft:silence", "minecraft:silence", false),
        new("minecraft:raiser", "minecraft:raiser", false),
        new("minecraft:host", "minecraft:host", false),
        new("minecraft:flow", "minecraft:flow", false),
        new("minecraft:bolt", "minecraft:bolt", false)
    ];

    /// <summary>Registry id at <paramref name="index"/>, or null when the index is outside the bootstrap table.</summary>
    public static string? IdAt(int index) =>
        (uint)index < (uint)Entries.Count ? Entries[index].Id : null;

    /// <summary>Pattern definition for a registry id, or null when the pattern is unknown.</summary>
    public static TrimPatternDefinition? ForId(string id)
    {
        foreach (var entry in Entries)
        {
            if (string.Equals(entry.Id, id, StringComparison.Ordinal))
            {
                return entry;
            }
        }

        return null;
    }
}
