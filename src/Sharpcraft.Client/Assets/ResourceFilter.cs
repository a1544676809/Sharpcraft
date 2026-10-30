// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Text.Json;
using System.Text.RegularExpressions;
using Sharpcraft.Common.Identifiers;

namespace Sharpcraft.Client.Assets;

/// <summary>
/// A pack's <c>filter</c> block: paths it hides from every lower priority pack
/// (docs/analysis/assets/resource-pack-stack.md §2).
/// </summary>
/// <remarks>
/// Each entry carries an optional namespace and path regular expression, and a missing side matches everything. The
/// reference tests these with a predicate built from a compiled pattern, which is a substring match rather than a whole
/// string match, so the same is done here; anchoring would silently change which resources a pack hides.
/// </remarks>
public sealed class ResourceFilter
{
    private readonly List<(Regex? Namespace, Regex? Path)> _entries;

    private ResourceFilter(List<(Regex? Namespace, Regex? Path)> entries) => _entries = entries;

    /// <summary>Parses the <c>filter</c> block of a <c>pack.mcmeta</c>.</summary>
    /// <param name="json">File contents.</param>
    /// <returns>The filter, or null when the file declares none or it cannot be read.</returns>
    public static ResourceFilter? TryParse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);

            if (!document.RootElement.TryGetProperty("filter", out var filter) ||
                !filter.TryGetProperty("block", out var block) ||
                block.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var entries = new List<(Regex? Namespace, Regex? Path)>();

            foreach (var entry in block.EnumerateArray())
            {
                var ns = ReadPattern(entry, "namespace");
                var path = ReadPattern(entry, "path");

                if (ns is null && path is null)
                {
                    // An entry with neither field matches every resource.
                    entries.Add((null, null));
                    continue;
                }

                entries.Add((ns, path));
            }

            return entries.Count == 0 ? null : new ResourceFilter(entries);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    /// <summary>True when this filter hides the location.</summary>
    /// <param name="location">Resource to test.</param>
    public bool IsFiltered(Identifier location)
    {
        foreach (var (ns, path) in _entries)
        {
            var namespaceMatches = ns?.IsMatch(location.Namespace) ?? true;
            var pathMatches = path?.IsMatch(location.Path) ?? true;

            if (namespaceMatches && pathMatches)
            {
                return true;
            }
        }

        return false;
    }

    private static Regex? ReadPattern(JsonElement entry, string name)
    {
        if (!entry.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return new Regex(value.GetString()!, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    }
}
