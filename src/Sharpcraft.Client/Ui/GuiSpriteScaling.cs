// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Text.Json;

namespace Sharpcraft.Client.Ui;

/// <summary>
/// The scaling a GUI sprite declares in its <c>.mcmeta</c> (docs/analysis/gui/tooltip-background.md).
/// </summary>
/// <remarks>
/// The reference blits these sprites through a nine slice, so the corners keep their pixels while the middle stretches.
/// A sprite without this block is drawn stretched whole, which is why the description is read rather than assumed.
/// </remarks>
public sealed record GuiSpriteScaling
{
    /// <summary>Scaling type, <c>nine_slice</c> being the one understood here.</summary>
    public string Type { get; init; } = "stretch";

    /// <summary>Width of one corner, in sprite pixels.</summary>
    public int Border { get; init; }

    /// <summary>True when the middle region stretches rather than tiles.</summary>
    public bool StretchInner { get; init; }

    /// <summary>Parses the <c>gui.scaling</c> block of an <c>.mcmeta</c> file.</summary>
    /// <param name="json">File contents.</param>
    /// <returns>The scaling, or null when the file carries none.</returns>
    public static GuiSpriteScaling? TryParse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);

            if (!document.RootElement.TryGetProperty("gui", out var gui) ||
                !gui.TryGetProperty("scaling", out var scaling))
            {
                return null;
            }

            var type = scaling.TryGetProperty("type", out var typeElement)
                ? typeElement.GetString() ?? "stretch"
                : "stretch";

            var border = scaling.TryGetProperty("border", out var borderElement) &&
                         borderElement.TryGetInt32(out var borderValue)
                ? borderValue
                : 0;

            var stretchInner = scaling.TryGetProperty("stretch_inner", out var stretchElement) &&
                               stretchElement.ValueKind == JsonValueKind.True;

            return new GuiSpriteScaling { Type = type, Border = border, StretchInner = stretchInner };
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
