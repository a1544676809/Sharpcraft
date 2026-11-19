// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Globalization;

namespace Sharpcraft.Client.Tests.Render;

/// <summary>
/// Test-only parser for the jar potion-colours sample. This was deliberately kept OUT of the production assembly:
/// a potion registry id is not carried on EntityRenderState yet, so there is no production consumer for an
/// effect-derived colour (R-046 blocker). When that id is threaded through, promote this to a production table with a
/// real consumer and a ref:// source mapping.
/// </summary>
public sealed class PotionColorTable
{
    private readonly Dictionary<string, int> _potionColors;
    private readonly Dictionary<string, int> _effectColors;

    private PotionColorTable(Dictionary<string, int> potionColors, Dictionary<string, int> effectColors)
    {
        _potionColors = potionColors;
        _effectColors = effectColors;
    }

    /// <summary>Parses a potion-colors reference sample (kind|id|color|effects rows, '#' comments).</summary>
    public static PotionColorTable Parse(IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var potions = new Dictionary<string, int>(StringComparer.Ordinal);
        var effects = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var raw in lines)
        {
            if (string.IsNullOrWhiteSpace(raw) || raw.StartsWith('#'))
            {
                continue;
            }

            var parts = raw.Split('\t');
            if (parts.Length < 3 || !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var color))
            {
                continue;
            }

            if (parts[0] == "potion")
            {
                potions[parts[1]] = color;
            }
            else if (parts[0] == "effect")
            {
                effects[parts[1]] = color;
            }
        }

        return new PotionColorTable(potions, effects);
    }

    /// <summary>The jar colour of a plain stack of this potion, or null when the id is unknown.</summary>
    public int? PlainColor(string potionPath) =>
        potionPath is not null && _potionColors.TryGetValue(potionPath, out var color) ? color : null;

    /// <summary>The base effect colour used by getColorOptional, or null when the effect id is unknown.</summary>
    public int? EffectColor(string effectPath) =>
        effectPath is not null && _effectColors.TryGetValue(effectPath, out var color) ? color : null;

    public int PotionCount => _potionColors.Count;
    public int EffectCount => _effectColors.Count;
}
