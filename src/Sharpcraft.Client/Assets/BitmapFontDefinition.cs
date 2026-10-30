// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Text.Json;
using Sharpcraft.Common.Identifiers;

namespace Sharpcraft.Client.Assets;

/// <summary>One resolved bitmap font provider: which sheet to read and how to slice and size it.</summary>
/// <param name="File">Sheet identifier.</param>
/// <param name="Height">Declared line height in font pixels.</param>
/// <param name="Ascent">Baseline distance from the top of the line.</param>
/// <param name="Columns">Cells across, from the character grid.</param>
/// <param name="Rows">Cells down, from the character grid.</param>
public readonly record struct BitmapFontProvider(Identifier File, int Height, int Ascent, int Columns, int Rows);

/// <summary>
/// Resolves the ascii bitmap provider through the font definition files
/// (docs/analysis/assets/font-slicing.md §1).
/// </summary>
/// <remarks>
/// The grid, height and ascent are declared by the pack, so assuming sixteen columns and a height of eight would be wrong
/// for a pack that says otherwise. Reference providers are followed a bounded number of times, which is what the vanilla
/// definition files use to split the font into several files.
/// </remarks>
public static class BitmapFontDefinition
{
    private const string Root = "minecraft:default";
    private const int MaxDepth = 8;

    /// <summary>Resolves the ascii provider, or null when no definition can be read.</summary>
    /// <param name="resources">Session resource manager.</param>
    public static BitmapFontProvider? Resolve(ResourceManager resources)
    {
        ArgumentNullException.ThrowIfNull(resources);

        var pending = new Queue<(Identifier Id, int Depth)>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        if (Identifier.TryParse(Root, out var root))
        {
            pending.Enqueue((root, 0));
        }

        while (pending.Count > 0)
        {
            var (id, depth) = pending.Dequeue();

            if (depth > MaxDepth || !seen.Add(id.ToString()))
            {
                continue;
            }

            var bytes = resources.Read(ResourceKind.Font, id);

            if (bytes is null)
            {
                continue;
            }

            var found = Read(bytes, pending, depth);

            if (found is { } provider)
            {
                return provider;
            }
        }

        return null;
    }

    private static BitmapFontProvider? Read(
        byte[] bytes,
        Queue<(Identifier Id, int Depth)> pending,
        int depth)
    {
        try
        {
            using var document = JsonDocument.Parse(bytes);

            if (!document.RootElement.TryGetProperty("providers", out var providers) ||
                providers.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var provider in providers.EnumerateArray())
            {
                var type = provider.TryGetProperty("type", out var typeElement) ? typeElement.GetString() : null;

                if (type == "reference")
                {
                    if (provider.TryGetProperty("id", out var idElement) &&
                        Identifier.TryParse(idElement.GetString(), out var referenced))
                    {
                        pending.Enqueue((referenced, depth + 1));
                    }

                    continue;
                }

                if (type != "bitmap")
                {
                    continue;
                }

                if (!provider.TryGetProperty("file", out var fileElement) ||
                    fileElement.GetString() is not { Length: > 0 } file ||
                    !file.Contains("ascii", StringComparison.Ordinal) ||
                    !Identifier.TryParse(file, out var identifier))
                {
                    continue;
                }

                var height = provider.TryGetProperty("height", out var heightElement) && heightElement.TryGetInt32(out var declared)
                    ? declared
                    : 8;

                var ascent = provider.TryGetProperty("ascent", out var ascentElement) && ascentElement.TryGetInt32(out var baseline)
                    ? baseline
                    : 7;

                if (!provider.TryGetProperty("chars", out var chars) || chars.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                var rows = chars.GetArrayLength();

                if (rows == 0 || chars[0].GetString() is not { } firstRow)
                {
                    continue;
                }

                var columns = 0;

                foreach (var _ in firstRow.EnumerateRunes())
                {
                    columns++;
                }

                if (columns == 0)
                {
                    continue;
                }

                return new BitmapFontProvider(identifier, height, ascent, columns, rows);
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }
}
