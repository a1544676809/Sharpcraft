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

            if (TryReadVersion(pack, "min_format", out var min) && TryReadVersion(pack, "max_format", out var max))
            {
                return (min, max);
            }

            if (pack.TryGetProperty("supported_formats", out var supported))
            {
                if (supported.ValueKind == JsonValueKind.Number && supported.TryGetInt32(out var single))
                {
                    var version = new PackFormatVersion(single, 0);
                    return (version, version);
                }

                if (supported.ValueKind == JsonValueKind.Array && supported.GetArrayLength() == 2 &&
                    TryReadVersion(supported[0], out var low) && TryReadVersion(supported[1], out var high))
                {
                    return (low, high);
                }
            }

            if (TryReadVersion(pack, "pack_format", out var legacy))
            {
                return (legacy, legacy);
            }

            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool TryReadVersion(JsonElement element, string name, out PackFormatVersion version)
    {
        version = default;

        if (!element.TryGetProperty(name, out var value))
        {
            return false;
        }

        if (value.ValueKind == JsonValueKind.Number)
        {
            if (!value.TryGetInt32(out var major))
            {
                return false;
            }

            version = new PackFormatVersion(major, 0);
            return true;
        }

        return value.ValueKind == JsonValueKind.String && PackFormatVersion.TryParse(value.GetString(), out version);
    }

    private static bool TryReadVersion(JsonElement value, out PackFormatVersion version)
    {
        version = default;

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var major))
        {
            version = new PackFormatVersion(major, 0);
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
