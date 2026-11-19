// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Globalization;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Source-derived potion tint table: maps a potion's registry path to the ARGB colour of a plain stack of that potion.
/// Values are the jar golden vectors in <c>potion-colors.reference.txt</c> (PotionContents.getColorOr / getColorOptional).
/// A stack's custom colour (ItemComponentInfo.PotionColor) takes precedence; the potion id supplies the
/// effect-derived colour when no custom colour is present; the model's default is the final fallback.
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.world.item.alchemy.PotionContents", "ref://mc-26.3-analysis/src/net/minecraft/world/item/alchemy/PotionContents.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Adapter, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "plain-stack colours from jar probe; custom effects (stack-specific effect lists) are not resolved")]
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
