// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Common.Registries;

/// <summary>Numeric ids of the vanilla attributes, from the registration-order snapshot (names/order metadata only).</summary>
public static class AttributeTypes
{
    private static readonly Lazy<Dictionary<string, int>> Ids = new(Load);

    public static int GetId(string name) => Ids.Value.TryGetValue(name, out var id) ? id : -1;

    private static Dictionary<string, int> Load()
    {
        using var stream = typeof(AttributeTypes).Assembly
            .GetManifestResourceStream("Sharpcraft.Common.Registries.Data.vanilla-attribute-order.txt")
            ?? throw new InvalidOperationException("attribute order snapshot is missing");
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
