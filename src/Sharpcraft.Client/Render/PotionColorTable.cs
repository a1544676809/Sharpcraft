// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Network;

namespace Sharpcraft.Client.Render;

/// <summary>
/// Production potion/effect colour table generated from the 26.3 jar colour sample
/// (ref://mc-26.3-analysis/vectors/b-class-source/held-world/PotionColorProbe.java,
/// potion-colors.reference.txt). Potion ids resolve through the connection potion registry; visible custom effects
/// blend by amplifier exactly like PotionContents.getColorOptional. Unknown potion/effect ids return null so the
/// caller falls back to the item tint default (never guessed). Coverage is the jar 26.3 potion/effect list;
/// data-driven custom potions/effects from a server datapack are outside that table (R-046 coverage note).
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.world.item.alchemy.PotionContents", "ref://mc-26.3-analysis/src/net/minecraft/world/item/alchemy/PotionContents.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Adapter, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "getColor/getColorOr/getColorOptional colour table from the 26.3 jar sample; dynamic potion/effect ids via DynamicRegistries")]
public static class PotionColorTable
{
    private static readonly Dictionary<string, int> PotionColors = new(StringComparer.Ordinal)
    {
        ["water"] = -13083194,
        ["mundane"] = -13083194,
        ["thick"] = -13083194,
        ["awkward"] = -13083194,
        ["night_vision"] = -3997850,
        ["long_night_vision"] = -3997850,
        ["invisibility"] = -592138,
        ["long_invisibility"] = -592138,
        ["leaping"] = -131196,
        ["long_leaping"] = -131196,
        ["strong_leaping"] = -131196,
        ["fire_resistance"] = -26368,
        ["long_fire_resistance"] = -26368,
        ["swiftness"] = -13374465,
        ["long_swiftness"] = -13374465,
        ["strong_swiftness"] = -13374465,
        ["slowness"] = -7622688,
        ["long_slowness"] = -7622688,
        ["strong_slowness"] = -7622688,
        ["turtle_master"] = -7503130,
        ["long_turtle_master"] = -7503130,
        ["strong_turtle_master"] = -7502362,
        ["water_breathing"] = -6759744,
        ["long_water_breathing"] = -6759744,
        ["healing"] = -515037,
        ["strong_healing"] = -515037,
        ["harming"] = -5675670,
        ["strong_harming"] = -5675670,
        ["poison"] = -7888029,
        ["long_poison"] = -7888029,
        ["strong_poison"] = -7888029,
        ["regeneration"] = -3318613,
        ["long_regeneration"] = -3318613,
        ["strong_regeneration"] = -3318613,
        ["strength"] = -14592,
        ["long_strength"] = -14592,
        ["strong_strength"] = -14592,
        ["weakness"] = -12038840,
        ["long_weakness"] = -12038840,
        ["luck"] = -10895098,
        ["slow_falling"] = -798791,
        ["long_slow_falling"] = -798791,
        ["wind_charged"] = -4339201,
        ["weaving"] = -8885926,
        ["oozing"] = -6684765,
        ["infested"] = -7562356,
    };

    private static readonly Dictionary<string, int> EffectColors = new(StringComparer.Ordinal)
    {
        ["speed"] = 3402751,
        ["slowness"] = 9154528,
        ["haste"] = 14270531,
        ["mining_fatigue"] = 4866583,
        ["strength"] = 16762624,
        ["instant_health"] = 16262179,
        ["instant_damage"] = 11101546,
        ["jump_boost"] = 16646020,
        ["nausea"] = 5578058,
        ["regeneration"] = 13458603,
        ["resistance"] = 9520880,
        ["fire_resistance"] = 16750848,
        ["water_breathing"] = 10017472,
        ["invisibility"] = 16185078,
        ["blindness"] = 2039587,
        ["night_vision"] = 12779366,
        ["hunger"] = 5797459,
        ["weakness"] = 4738376,
        ["poison"] = 8889187,
        ["wither"] = 7561558,
        ["health_boost"] = 16284963,
        ["absorption"] = 2445989,
        ["saturation"] = 16262179,
        ["glowing"] = 9740385,
        ["levitation"] = 13565951,
        ["luck"] = 5882118,
        ["unluck"] = 12624973,
        ["slow_falling"] = 15978425,
        ["conduit_power"] = 1950417,
        ["dolphins_grace"] = 8954814,
        ["bad_omen"] = 745784,
        ["hero_of_the_village"] = 4521796,
        ["darkness"] = 2696993,
        ["trial_omen"] = 1484454,
        ["raid_omen"] = 14565464,
        ["wind_charged"] = 12438015,
        ["weaving"] = 7891290,
        ["oozing"] = 10092451,
        ["infested"] = 9214860,
        ["breath_of_the_nautilus"] = 65518,
    };

    private static readonly Dictionary<int, string> PotionNames = [];
    private static readonly Dictionary<int, string> EffectNames = [];

    /// <summary>Reference PotionContents.BASE_POTION_COLOR / potion tint default.</summary>
    public const int BasePotionColor = -13083194;

    /// <summary>Jar potion colour by registry path; null when the path is unknown.</summary>
    public static int? PlainColorByName(string? potionPath) =>
        potionPath is not null && PotionColors.TryGetValue(potionPath, out var color) ? color : null;

    /// <summary>Jar effect colour by registry path; null when the path is unknown.</summary>
    public static int? EffectColorByName(string? effectPath) =>
        effectPath is not null && EffectColors.TryGetValue(effectPath, out var color) ? color : null;

    /// <summary>Builds the positional id maps from the connection registries (potion, mob_effect).</summary>
    public static void Configure(DynamicRegistries? registries)
    {
        PotionNames.Clear();
        EffectNames.Clear();
        if (registries is null)
        {
            return;
        }

        ConfigureRegistry(registries, new Identifier("minecraft", "potion"), PotionNames);
        ConfigureRegistry(registries, new Identifier("minecraft", "mob_effect"), EffectNames);
    }

    private static void ConfigureRegistry(DynamicRegistries registries, Identifier registryId, Dictionary<int, string> target)
    {
        var entries = registries.Get(registryId);
        if (entries is null)
        {
            return;
        }

        for (var i = 0; i < entries.Count; i++)
        {
            target[i] = entries[i].Id.Path;
        }
    }

    /// <summary>
    /// Colour of a potion stack: visible custom effects blend by amplifier (PotionContents.getColorOptional), otherwise
    /// the potion registry colour. Returns null when the potion/effect cannot be resolved, so callers use the model
    /// default rather than a guessed colour.
    /// </summary>
    public static int? Resolve(int potionId, IReadOnlyList<PotionEffectInstance>? effects)
    {
        if (Blend(effects) is { } blended)
        {
            return blended;
        }

        return potionId >= 0 && PotionNames.TryGetValue(potionId, out var name) ? PlainColorByName(name) : null;
    }

    private static int? Blend(IReadOnlyList<PotionEffectInstance>? effects)
    {
        if (effects is null || effects.Count == 0)
        {
            return null;
        }

        var red = 0;
        var green = 0;
        var blue = 0;
        var totalWeight = 0;
        var any = false;
        foreach (var effect in effects)
        {
            if (!effect.Visible || !EffectNames.TryGetValue(effect.EffectId, out var path) || EffectColorByName(path) is not { } color)
            {
                continue;
            }

            var weight = effect.Amplifier + 1;
            red += weight * ((color >> 16) & 0xFF);
            green += weight * ((color >> 8) & 0xFF);
            blue += weight * (color & 0xFF);
            totalWeight += weight;
            any = true;
        }

        return any && totalWeight > 0 ? (red / totalWeight) << 16 | (green / totalWeight) << 8 | (blue / totalWeight) : null;
    }
}
