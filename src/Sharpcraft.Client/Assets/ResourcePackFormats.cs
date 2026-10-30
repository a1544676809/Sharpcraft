// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Text.Json;

namespace Sharpcraft.Client.Assets;

/// <summary>
/// Reads the format range a pack declares (docs/analysis/assets/resource-pack-format.md).
/// </summary>
/// <remarks>
/// Three shapes appear in the wild: <c>min_format</c>/<c>max_format</c> since 25w31a, a <c>supported_formats</c> number or
/// pair, and the older single <c>pack_format</c>. All three are read here so a pack written for any of those eras can be
/// judged instead of being silently accepted.
/// </remarks>
public static class ResourcePackFormats
{
    /// <summary>Parses the <c>pack</c> section's format range.</summary>
    /// <param name="json">File contents.</param>
    /// <returns>The range, or null when the file declares none.</returns>
    public static (PackFormatVersion Min, PackFormatVersion Max)? TryParseSupportedFormats(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);

            if (!document.RootElement.TryGetProperty("pack", out var pack) || pack.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            // min_format defaults its minor to 0 and max_format to the largest minor, exactly as the reference's bottom
            // and top codecs do; that is what lets a pack say max_format 97 and still accept 97.1.
            if (TryReadVersion(pack, "min_format", 0, out var min) &&
                TryReadVersion(pack, "max_format", int.MaxValue, out var max))
            {
                return (min, max);
            }

            if (pack.TryGetProperty("supported_formats", out var supported))
            {
                // supported_formats speaks in major versions only, so a pair means "every minor between these majors".
                if (supported.ValueKind == JsonValueKind.Number && supported.TryGetInt32(out var single))
                {
                    return (new PackFormatVersion(single, 0), new PackFormatVersion(single, int.MaxValue));
                }

                if (supported.ValueKind == JsonValueKind.Array && supported.GetArrayLength() == 2 &&
                    TryReadVersion(supported[0], 0, out var low) &&
                    TryReadVersion(supported[1], int.MaxValue, out var high))
                {
                    return (low, high);
                }
            }

            // The old single number also named a major, so every minor of it is acceptable.
            if (TryReadVersion(pack, "pack_format", 0, out var legacy))
            {
                return (legacy, new PackFormatVersion(legacy.Major, int.MaxValue));
            }

            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool TryReadVersion(
        JsonElement element,
        string name,
        int defaultMinor,
        out PackFormatVersion version)
    {
        version = default;

        if (!element.TryGetProperty(name, out var value))
        {
            return false;
        }

        return TryReadVersion(value, defaultMinor, out version);
    }

    private static bool TryReadVersion(JsonElement value, int defaultMinor, out PackFormatVersion version)
    {
        version = default;

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var major))
        {
            version = new PackFormatVersion(major, defaultMinor);
            return true;
        }

        if (value.ValueKind == JsonValueKind.Array && value.GetArrayLength() == 2 &&
            value[0].TryGetInt32(out var majorPart) && value[1].TryGetInt32(out var minorPart))
        {
            version = new PackFormatVersion(majorPart, minorPart);
            return true;
        }

        return value.ValueKind == JsonValueKind.String && PackFormatVersion.TryParse(value.GetString(), out version);
    }
}
