// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Text.Json;
using System.Text.RegularExpressions;

namespace Sharpcraft.Client.Assets;

/// <summary>One <c>overlays.entries</c> element: a folder that applies for a range of pack formats.</summary>
/// <param name="MinFormat">Lowest pack format the folder applies to.</param>
/// <param name="MaxFormat">Highest pack format the folder applies to.</param>
/// <param name="Directory">Folder name inside the pack.</param>
public readonly record struct OverlayEntry(int MinFormat, int MaxFormat, string Directory)
{
    /// <summary>True when this entry applies to the given pack format.</summary>
    /// <param name="format">Pack format in use.</param>
    public bool AppliesTo(int format) => format >= MinFormat && format <= MaxFormat;
}

/// <summary>
/// A pack's <c>overlays</c> block: folders layered over the pack root for matching formats
/// (docs/analysis/assets/resource-pack-stack.md §6/§8).
/// </summary>
/// <remarks>
/// The reference validates every folder name against a safe character set and only keeps entries whose format range
/// covers the running pack format. An entry is one format or a <c>[min, max]</c> pair, so the same pack can carry art for
/// several versions at once.
/// </remarks>
public static class ResourcePackOverlays
{
    private static readonly Regex DirectoryName = new("^[-_a-zA-Z0-9.]+$", RegexOptions.CultureInvariant);

    /// <summary>Parses the <c>overlays</c> block of a <c>pack.mcmeta</c>.</summary>
    /// <param name="json">File contents.</param>
    /// <returns>The entries, or null when the file declares none or cannot be read.</returns>
    public static List<OverlayEntry>? TryParse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);

            if (!document.RootElement.TryGetProperty("overlays", out var overlays) ||
                !overlays.TryGetProperty("entries", out var entries) ||
                entries.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var result = new List<OverlayEntry>();

            foreach (var entry in entries.EnumerateArray())
            {
                if (!entry.TryGetProperty("overlay", out var directoryElement) ||
                    directoryElement.GetString() is not { Length: > 0 } directory ||
                    !DirectoryName.IsMatch(directory))
                {
                    // An unsafe or missing name is refused rather than used as a path.
                    continue;
                }

                if (!TryReadRange(entry, out var min, out var max))
                {
                    continue;
                }

                result.Add(new OverlayEntry(min, max, directory));
            }

            return result.Count == 0 ? null : result;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool TryReadRange(JsonElement entry, out int min, out int max)
    {
        min = 0;
        max = 0;

        if (!entry.TryGetProperty("format", out var format))
        {
            return false;
        }

        if (format.ValueKind == JsonValueKind.Number)
        {
            if (!format.TryGetInt32(out var single))
            {
                return false;
            }

            min = single;
            max = single;
            return true;
        }

        if (format.ValueKind != JsonValueKind.Array || format.GetArrayLength() != 2)
        {
            return false;
        }

        return format[0].TryGetInt32(out min) && format[1].TryGetInt32(out max);
    }
}
