// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Common.Registries;

/// <summary>
/// Numeric ids of the vanilla data component types, read from the registration-order snapshot captured from the 26.3
/// jar. Only names and order are stored (protocol metadata, not assets).
/// </summary>
public static class DataComponentTypes
{
    private static readonly Lazy<Dictionary<string, int>> Ids = new(Load);

    /// <summary>Numeric registry id for a component identifier such as <c>charged_projectiles</c>; -1 when unknown.</summary>
    public static int GetId(string name) => Ids.Value.TryGetValue(name, out var id) ? id : -1;

    private static Dictionary<string, int> Load()
    {
        using var stream = typeof(DataComponentTypes).Assembly
            .GetManifestResourceStream("Sharpcraft.Common.Registries.Data.vanilla-data-component-order.txt")
            ?? throw new InvalidOperationException("data component order snapshot is missing");
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
