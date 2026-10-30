// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Text.Json;

namespace Sharpcraft.Client.Ui;

/// <summary>
/// Reads the advances a font definition assigns to characters that have no ink (docs/modules/ui.md 5).
/// </summary>
/// <remarks>
/// A space has no lit pixels, so its width cannot come from the glyph sheet: the sheet yields a width of zero and the
/// advance would collapse to a single pixel, which draws every sentence with its words run together. The reference
/// therefore declares such characters in the font definition with an explicit advance, and this reads them. Only the
/// parts needed for that are understood: <c>space</c> providers and the <c>reference</c> providers that lead to them.
/// </remarks>
public static class FontDefinition
{
    /// <summary>How many references deep the reader follows.</summary>
    public const int MaxReferenceDepth = 8;

    /// <summary>
    /// Applies the advances declared by a font definition to a font.
    /// </summary>
    /// <param name="json">Contents of a font definition.</param>
    /// <param name="readReference">Loads a referenced definition by its identifier, or returns null.</param>
    /// <param name="font">Font that receives the advances.</param>
    /// <returns>How many characters gained an advance.</returns>
    /// <exception cref="JsonException">The definition is not a JSON object.</exception>
    public static int Apply(string json, Func<string, string?> readReference, BitmapFont font)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(readReference);
        ArgumentNullException.ThrowIfNull(font);

        return Apply(json, readReference, font, depth: 0);
    }

    private static int Apply(string json, Func<string, string?> readReference, BitmapFont font, int depth)
    {
        if (depth > MaxReferenceDepth)
        {
            return 0;
        }

        using var document = JsonDocument.Parse(json);

        if (document.RootElement.ValueKind != JsonValueKind.Object ||
            !document.RootElement.TryGetProperty("providers", out var providers) ||
            providers.ValueKind != JsonValueKind.Array)
        {
            return 0;
        }

        var applied = 0;

        foreach (var provider in providers.EnumerateArray())
        {
            if (provider.ValueKind != JsonValueKind.Object ||
                !provider.TryGetProperty("type", out var type) ||
                type.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            switch (type.GetString())
            {
                case "space":
                    if (provider.TryGetProperty("advances", out var advances) &&
                        advances.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var advance in advances.EnumerateObject())
                        {
                            if (advance.Name.Length == 0 || advance.Value.ValueKind != JsonValueKind.Number)
                            {
                                continue;
                            }

                            font.SetAdvance(advance.Name[0], advance.Value.GetInt32());
                            applied++;
                        }
                    }

                    break;

                case "reference":
                    if (provider.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String &&
                        readReference(id.GetString() ?? string.Empty) is { Length: > 0 } referenced)
                    {
                        applied += Apply(referenced, readReference, font, depth + 1);
                    }

                    break;
            }
        }

        return applied;
    }
}
