// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Identifiers;

namespace Sharpcraft.Common.Registries;

/// <summary>One entry of the vanilla item registry; only the identity is modelled at this stage.</summary>
public sealed record ItemInfo(Identifier Id);

/// <summary>
/// Read-only vanilla item registry loaded from a registration-order snapshot. Line number after comments is the
/// numeric id, so lookup and enumeration share the same source of truth (docs/analysis/common/item-registry.md).
/// </summary>
public sealed class ItemRegistry
{
    private readonly Registry<ItemInfo> _registry = new();
    private readonly IReadOnlyList<ItemInfo> _items;

    private ItemRegistry(IEnumerable<string> identifiers)
    {
        foreach (var line in identifiers)
        {
            _registry.Register(Identifier.Parse(line), new ItemInfo(Identifier.Parse(line)));
        }

        _registry.Freeze();
        _items = _registry.Entries.Select(entry => entry.Value).ToArray();
    }

    /// <summary>Number of registered items.</summary>
    public int Count => _items.Count;

    /// <summary>Items in registration order; index equals the numeric item id.</summary>
    public IReadOnlyList<ItemInfo> Items => _items;

    /// <summary>Item at a registry id, or null when absent.</summary>
    public ItemInfo? ById(int id) => _registry.ById(id);

    /// <summary>Looks up an item by identifier.</summary>
    public bool TryGet(Identifier id, out ItemInfo? item) => _registry.TryGet(id, out item);

    /// <summary>Numeric registry id for an item, or -1 when absent.</summary>
    public int GetId(Identifier id) => _registry.TryGet(id, out var item) && item is not null ? _registry.GetId(item) : -1;

    /// <summary>Loads the embedded baseline snapshot, which is the runtime default.</summary>
    public static ItemRegistry LoadEmbedded()
    {
        using var stream = typeof(ItemRegistry).Assembly.GetManifestResourceStream("Sharpcraft.Common.Registries.Data.vanilla-item-order.txt")
            ?? throw new InvalidOperationException("embedded item registry snapshot is missing");
        using var reader = new StreamReader(stream);
        return FromLines(ReadLines(reader));
    }

    private static IEnumerable<string> ReadLines(StreamReader reader)
    {
        while (reader.ReadLine() is { } line)
        {
            yield return line;
        }
    }
    /// <summary>Loads the snapshot shipped under the test data directory.</summary>
    public static ItemRegistry Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"item registry snapshot missing: {path}", path);
        }

        return FromLines(File.ReadLines(path));
    }

    /// <summary>Builds a registry from snapshot lines; comments and blank lines are ignored.</summary>
    public static ItemRegistry FromLines(IEnumerable<string> lines)
    {
        var identifiers = lines
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .ToArray();

        if (identifiers.Length == 0)
        {
            throw new InvalidDataException("item registry snapshot has no identifiers");
        }

        if (!string.Equals(identifiers[0], "minecraft:air", StringComparison.Ordinal))
        {
            throw new InvalidDataException("item registry snapshot must start with minecraft:air");
        }

        return new ItemRegistry(identifiers);
    }
}



