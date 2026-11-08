// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Network;

namespace Sharpcraft.Common.Registries;

/// <summary>
/// Positional trim registry order for the current connection. The network <c>trim</c> component carries holder ids,
/// so a server that reorders or appends trim entries must be reflected here instead of always using the bootstrap
/// order.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.core.Registry", "ref://mc-26.3-analysis/src/net/minecraft/core/Registry.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Adapter, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "Trim material/pattern positional order from configuration registry_data; bootstrap fallback")]
public static class TrimRegistryTable
{
    private static IReadOnlyList<TrimMaterialDefinition> _materials = TrimMaterialRegistry.Entries;
    private static IReadOnlyList<TrimPatternDefinition> _patterns = TrimPatternRegistry.Entries;

    /// <summary>Material definitions in the current connection's positional order.</summary>
    public static IReadOnlyList<TrimMaterialDefinition> Materials => _materials;

    /// <summary>Pattern definitions in the current connection's positional order.</summary>
    public static IReadOnlyList<TrimPatternDefinition> Patterns => _patterns;

    /// <summary>Material registry id at a network holder id, or null when the id is out of range.</summary>
    public static string? MaterialIdAt(int index) =>
        (uint)index < (uint)_materials.Count ? _materials[index].Id : null;

    /// <summary>Pattern registry id at a network holder id, or null when the id is out of range.</summary>
    public static string? PatternIdAt(int index) =>
        (uint)index < (uint)_patterns.Count ? _patterns[index].Id : null;

    /// <summary>Palette id for a material id, including a dynamic entry's NBT palette_id.</summary>
    public static string? PaletteIdFor(string materialId)
    {
        foreach (var entry in _materials)
        {
            if (string.Equals(entry.Id, materialId, StringComparison.Ordinal))
            {
                return entry.PaletteId;
            }
        }

        return null;
    }

    /// <summary>Definition for a pattern id, including a dynamic entry's NBT asset_id/decal.</summary>
    public static TrimPatternDefinition? PatternForId(string patternId)
    {
        foreach (var entry in _patterns)
        {
            if (string.Equals(entry.Id, patternId, StringComparison.Ordinal))
            {
                return entry;
            }
        }

        return null;
    }

    /// <summary>Rebuilds the positional tables from one connection's configuration registries.</summary>
    public static void Configure(DynamicRegistries? registries)
    {
        _materials = ReadMaterials(registries) ?? TrimMaterialRegistry.Entries;
        _patterns = ReadPatterns(registries) ?? TrimPatternRegistry.Entries;
    }

    private static IReadOnlyList<TrimMaterialDefinition>? ReadMaterials(DynamicRegistries? registries)
    {
        var entries = registries?.Get(new Identifier("minecraft", "trim_material"));
        if (entries is null)
        {
            return null;
        }

        var result = new List<TrimMaterialDefinition>(entries.Count);
        foreach (var entry in entries)
        {
            var id = entry.Id.ToString();
            var palette = entry.Data?.GetString("palette_id") ?? TrimMaterialRegistry.PaletteIdFor(id);
            if (palette is not null)
            {
                result.Add(new TrimMaterialDefinition(id, palette));
            }
        }

        return result;
    }

    private static IReadOnlyList<TrimPatternDefinition>? ReadPatterns(DynamicRegistries? registries)
    {
        var entries = registries?.Get(new Identifier("minecraft", "trim_pattern"));
        if (entries is null)
        {
            return null;
        }

        var result = new List<TrimPatternDefinition>(entries.Count);
        foreach (var entry in entries)
        {
            var id = entry.Id.ToString();
            var fallback = TrimPatternRegistry.ForId(id);
            var assetId = entry.Data?.GetString("asset_id") ?? fallback?.AssetId;
            if (assetId is not null)
            {
                result.Add(new TrimPatternDefinition(
                    id,
                    assetId,
                    entry.Data?.GetBool("decal") ?? fallback?.Decal ?? false));
            }
        }

        return result;
    }
}
