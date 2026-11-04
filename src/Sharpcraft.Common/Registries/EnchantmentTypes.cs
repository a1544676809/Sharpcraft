// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Common.Registries;

/// <summary>
/// Numeric ids of the vanilla enchantments, read from the registration-order snapshot. Datapack registries load from a
/// sorted identifier map, so the baseline order is the sorted enchantment paths; only names/order are stored.
/// </summary>
public static class EnchantmentTypes
{
    private static readonly Lazy<Dictionary<string, int>> Ids = new(Load);

    /// <summary>Numeric registry id for an enchantment identifier such as <c>quick_charge</c>; -1 when unknown.</summary>
    public static int GetId(string name) => Ids.Value.TryGetValue(name, out var id) ? id : -1;

    private static Dictionary<string, int> Load()
    {
        using var stream = typeof(EnchantmentTypes).Assembly
            .GetManifestResourceStream("Sharpcraft.Common.Registries.Data.vanilla-enchantment-order.txt")
            ?? throw new InvalidOperationException("enchantment order snapshot is missing");
        using var reader = new StreamReader(stream);

        var ids = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var id = 0; reader.ReadLine() is { } line; id++)
        {
            var name = line.Trim();
            if (name.Length > 0)
            {
                ids[name] = id;
            }
        }

        return ids;
    }
}
