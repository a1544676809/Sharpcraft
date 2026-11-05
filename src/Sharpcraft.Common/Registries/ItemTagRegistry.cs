// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Text.Json;
using Sharpcraft.Common.Identifiers;

namespace Sharpcraft.Common.Registries;

/// <summary>
/// Read-only item tag table (reference <c>data/minecraft/tags/item/**.json</c>), loaded from the extracted jar data.
/// The repository never contains the tag files themselves; the launcher extracts them outside the checkout and the
/// client parses them at startup (AGENTS.md principle 3).
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.tags.ItemTags", "ref://mc-26.3-analysis/src/net/minecraft/tags/ItemTags.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Merged, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial)]
public sealed class ItemTagRegistry
{
    private readonly Dictionary<Identifier, HashSet<Identifier>> _tags;

    private ItemTagRegistry(Dictionary<Identifier, HashSet<Identifier>> tags)
    {
        _tags = tags;
    }

    /// <summary>All tag identifiers present in the table.</summary>
    public IReadOnlyCollection<Identifier> Tags => _tags.Keys;

    /// <summary>True when <paramref name="item"/> belongs to <paramref name="tag"/> (directly or via nested tags).</summary>
    public bool IsInTag(Identifier tag, Identifier item) =>
        _tags.TryGetValue(tag, out var values) && values.Contains(item);

    /// <summary>The resolved item set of one tag; empty when the tag is unknown.</summary>
    public IReadOnlyCollection<Identifier> Values(Identifier tag) =>
        _tags.TryGetValue(tag, out var values) ? values : Array.Empty<Identifier>();

    /// <summary>Loads every <c>*.json</c> under an extracted item-tag directory (e.g. <c>.../data/minecraft/tags/item</c>).</summary>
    public static ItemTagRegistry LoadDirectory(string itemTagsDirectory)
    {
        if (!Directory.Exists(itemTagsDirectory))
        {
            return Empty;
        }

        var sources = new List<(Identifier Tag, string Json)>();
        foreach (var file in Directory.EnumerateFiles(itemTagsDirectory, "*.json", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(itemTagsDirectory, file).Replace('\\', '/');
            var tagPath = relative[..^".json".Length];
            sources.Add((new Identifier("minecraft", tagPath), File.ReadAllText(file)));
        }

        return Parse(sources);
    }

    /// <summary>An empty table, used when no extracted data is available.</summary>
    public static ItemTagRegistry Empty { get; } = new([]);

    /// <summary>Parses tag JSON documents. References to other tags (<c>#namespace:path</c>) are resolved afterwards.</summary>
    public static ItemTagRegistry Parse(IEnumerable<(Identifier Tag, string Json)> sources)
    {
        var raw = new Dictionary<Identifier, List<Identifier>>();
        var references = new Dictionary<Identifier, List<Identifier>>();

        foreach (var (tag, json) in sources)
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (!root.TryGetProperty("values", out var values) || values.ValueKind != JsonValueKind.Array)
            {
                raw[tag] = [];
                continue;
            }

            var direct = new List<Identifier>();
            var refs = new List<Identifier>();
            foreach (var element in values.EnumerateArray())
            {
                var value = ReadValue(element);
                if (value is null)
                {
                    continue;
                }

                if (value.StartsWith('#'))
                {
                    refs.Add(ParseIdentifier(value[1..]));
                }
                else
                {
                    direct.Add(ParseIdentifier(value));
                }
            }

            raw[tag] = direct;
            references[tag] = refs;
        }

        var resolved = new Dictionary<Identifier, HashSet<Identifier>>();
        foreach (var tag in raw.Keys)
        {
            resolved[tag] = Resolve(tag, raw, references, resolved, []);
        }

        return new ItemTagRegistry(resolved);
    }

    private static HashSet<Identifier> Resolve(
        Identifier tag,
        Dictionary<Identifier, List<Identifier>> raw,
        Dictionary<Identifier, List<Identifier>> references,
        Dictionary<Identifier, HashSet<Identifier>> resolved,
        HashSet<Identifier> visiting)
    {
        if (resolved.TryGetValue(tag, out var done))
        {
            return done;
        }

        var result = new HashSet<Identifier>();
        if (!visiting.Add(tag))
        {
            // Cycle guard: return what is known so far rather than recursing forever.
            return result;
        }

        if (raw.TryGetValue(tag, out var direct))
        {
            result.UnionWith(direct);
        }

        if (references.TryGetValue(tag, out var refs))
        {
            foreach (var reference in refs)
            {
                result.UnionWith(Resolve(reference, raw, references, resolved, visiting));
            }
        }

        visiting.Remove(tag);
        resolved[tag] = result;
        return result;
    }

    private static string? ReadValue(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Object when element.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String => id.GetString(),
        _ => null
    };

    private static Identifier ParseIdentifier(string value)
    {
        var colon = value.IndexOf(':');
        return colon < 0
            ? new Identifier("minecraft", value)
            : new Identifier(value[..colon], value[(colon + 1)..]);
    }
}
