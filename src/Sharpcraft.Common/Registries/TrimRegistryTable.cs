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
    private static readonly string[] BootstrapMaterials = [.. TrimMaterialRegistry.Entries.Select(entry => entry.Id)];
    private static readonly string[] BootstrapPatterns = [.. TrimPatternRegistry.Entries.Select(entry => entry.Id)];

    private static string[] _materials = BootstrapMaterials;
    private static string[] _patterns = BootstrapPatterns;

    /// <summary>Material registry id at a network holder id, or null when the id is out of range.</summary>
    public static string? MaterialIdAt(int index) =>
        (uint)index < (uint)_materials.Length ? _materials[index] : null;

    /// <summary>Pattern registry id at a network holder id, or null when the id is out of range.</summary>
    public static string? PatternIdAt(int index) =>
        (uint)index < (uint)_patterns.Length ? _patterns[index] : null;

    /// <summary>Rebuilds the positional tables from one connection's configuration registries.</summary>
    public static void Configure(DynamicRegistries? registries)
    {
        _materials = ReadIds(registries, "trim_material") ?? BootstrapMaterials;
        _patterns = ReadIds(registries, "trim_pattern") ?? BootstrapPatterns;
    }

    private static string[]? ReadIds(DynamicRegistries? registries, string path)
    {
        var entries = registries?.Get(new Identifier("minecraft", path));
        return entries is null ? null : [.. entries.Select(entry => entry.Id.ToString())];
    }
}
