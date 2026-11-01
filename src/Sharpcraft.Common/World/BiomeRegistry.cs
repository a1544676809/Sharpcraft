// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.Nbt;
using Sharpcraft.Common.Network;

namespace Sharpcraft.Common.World;

/// <summary>How a biome modifies its base grass colour (reference BiomeSpecialEffects.GrassColorModifier).</summary>
public enum GrassColourModifier
{
    /// <summary>No modification.</summary>
    None,

    /// <summary>Dark forest darkening.</summary>
    DarkForest,

    /// <summary>Swamp colouring.</summary>
    Swamp
}

/// <summary>Biome climate used by the colour maps (docs/analysis/common/biome-data.md 2).</summary>
public readonly record struct BiomeClimate(bool HasPrecipitation, float Temperature, float Downfall);

/// <summary>Biome colour effects (grass/foliage/water), colours packed as 0xFFRRGGBB.</summary>
public sealed record BiomeEffects(int WaterColor, int? FoliageColor, int? DryFoliageColor, int? GrassColor, GrassColourModifier GrassColorModifier);

/// <summary>One biome registry entry: identity, climate, and colour effects.</summary>
public sealed record BiomeEntry(Identifier Id, BiomeClimate Climate, BiomeEffects Effects);

/// <summary>
/// The client biome registry decoded from the <c>registry_data</c> payload (docs/analysis/common/biome-data.md 2). Entry
/// order is positional: a reference on the wire is an index into this list, exactly like block state ids.
/// </summary>
public sealed class BiomeRegistry
{
    /// <summary>Registry key the biome entries arrive under.</summary>
    public static readonly Identifier RegistryId = new("minecraft", "worldgen/biome");

    private readonly BiomeEntry[] _entries;

    private BiomeRegistry(BiomeEntry[] entries) => _entries = entries;

    /// <summary>Entries in wire order.</summary>
    public IReadOnlyList<BiomeEntry> Entries => _entries;

    /// <summary>Entry at a positional id, or null.</summary>
    public BiomeEntry? ById(int id) => id >= 0 && id < _entries.Length ? _entries[id] : null;

    /// <summary>
    /// Builds the biome registry from the synchronised registries, or null when the biome registry has not arrived.
    /// Entries whose payload cannot be decoded are skipped (their positional ids are preserved via an empty slot).
    /// </summary>
    public static BiomeRegistry? From(DynamicRegistries registries)
    {
        ArgumentNullException.ThrowIfNull(registries);
        var source = registries.Get(RegistryId);
        if (source is null)
        {
            return null;
        }

        var entries = new BiomeEntry[source.Count];
        for (var i = 0; i < source.Count; i++)
        {
            var entry = source[i];
            entries[i] = Parse(entry.Id, entry.Data) ?? new BiomeEntry(entry.Id, default, new BiomeEffects(0, null, null, null, GrassColourModifier.None));
        }

        return new BiomeRegistry(entries);
    }

    /// <summary>Decodes one biome entry, or null when its payload is missing.</summary>
    public static BiomeEntry? Parse(Identifier id, NbtCompound? data)
    {
        if (data is null)
        {
            return null;
        }

        var hasPrecipitation = data.GetBool("has_precipitation", true);
        var temperature = data.GetFloat("temperature");
        var downfall = data.GetFloat("downfall");

        var effects = data.GetCompound("effects");
        var modifier = effects?.GetString("grass_color_modifier") switch
        {
            "dark_forest" => GrassColourModifier.DarkForest,
            "swamp" => GrassColourModifier.Swamp,
            _ => GrassColourModifier.None
        };

        var biomeEffects = new BiomeEffects(
            WaterColor: ParseColour(effects?.GetString("water_color"), DefaultWaterColor),
            FoliageColor: ParseColourOrNull(effects?.GetString("foliage_color")),
            DryFoliageColor: ParseColourOrNull(effects?.GetString("dry_foliage_color")),
            GrassColor: ParseColourOrNull(effects?.GetString("grass_color")),
            GrassColorModifier: modifier);

        return new BiomeEntry(id, new BiomeClimate(hasPrecipitation, temperature, downfall), biomeEffects);
    }

    /// <summary>Default overworld water colour (ARGB 4159204) when a biome omits one (it is required on the wire).</summary>
    public const int DefaultWaterColor = 0x3F76E4;

    /// <summary>Parses a <c>"RRGGBB"</c> (optionally <c>#</c>-prefixed) colour into 0xFFRRGGBB, or the fallback.</summary>
    public static int ParseColour(string? hex, int fallback = 0) =>
        ParseColourOrNull(hex) ?? fallback;

    /// <summary>Parses a <c>"RRGGBB"</c> (optionally <c>#</c>-prefixed) colour, or null when absent/invalid.</summary>
    public static int? ParseColourOrNull(string? hex)
    {
        if (string.IsNullOrEmpty(hex))
        {
            return null;
        }

        var text = hex[0] == '#' ? hex[1..] : hex;
        if (text.Length != 6 || !int.TryParse(text, System.Globalization.NumberStyles.HexNumber, null, out var value))
        {
            return null;
        }

        return unchecked((int)0xFF000000) | value;
    }
}
