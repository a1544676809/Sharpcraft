// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Assets;
using Sharpcraft.Common.Identifiers;
using Sharpcraft.Common.World;

namespace Sharpcraft.Client.Render;

/// <summary>Which tint a block takes.</summary>
public enum BlockTintKind
{
    /// <summary>No tint: the texture carries its own colour.</summary>
    None,

    /// <summary>Grass colour from the grass colour map.</summary>
    Grass,

    /// <summary>Foliage colour from the foliage colour map.</summary>
    Foliage,

    /// <summary>Fixed evergreen foliage colour.</summary>
    FoliageEvergreen,

    /// <summary>Fixed birch foliage colour.</summary>
    FoliageBirch,

    /// <summary>Water colour.</summary>
    Water
}

/// <summary>
/// Biome tinting: the colour a block's tinted faces are multiplied by.
/// </summary>
/// <remarks>
/// The reference resolves this per block position from the biome's temperature and rainfall through a colour map, averaged
/// over a small neighbourhood, and applies it to the faces whose model marks a tint index
/// (ref://mc-26.3-analysis/src/net/minecraft/client/renderer/BiomeColors.java,
/// ref://.../world/level/GrassColor.java, ref://.../world/level/FoliageColor.java).
/// Biome data does not reach us yet, so the default temperature and rainfall are used and that is recorded as a gap.
/// </remarks>
public sealed class BiomeTint
{
    /// <summary>Reference foliage fallback colour.</summary>
    public const int FoliageDefault = -12012264;

    /// <summary>Reference evergreen foliage colour.</summary>
    public const int FoliageEvergreen = -10380959;

    /// <summary>Reference birch foliage colour.</summary>
    public const int FoliageBirch = -8345771;

    /// <summary>Reference default water colour, used by most biomes.</summary>
    public const int WaterDefault = 4159204;

    private readonly Colormap? _grass;
    private readonly Colormap? _foliage;
    private readonly Func<int, int, int, int>? _biomeIdAt;
    private readonly BiomeRegistry? _registry;
    private readonly int _blendRadius;

    private BiomeTint(Colormap? grass, Colormap? foliage, Func<int, int, int, int>? biomeIdAt = null, BiomeRegistry? registry = null, int blendRadius = 0)
    {
        _grass = grass;
        _foliage = foliage;
        _biomeIdAt = biomeIdAt;
        _registry = registry;
        _blendRadius = blendRadius;
    }

    /// <summary>Biome temperature used until biome data reaches the client.</summary>
    public const double DefaultTemperature = 0.5;

    /// <summary>Biome rainfall used until biome data reaches the client.</summary>
    public const double DefaultRainfall = 1.0;

    /// <summary>Loads both colour maps from the resource stack.</summary>
    /// <param name="resources">Session resource manager.</param>
    public static BiomeTint Load(ResourceManager resources) =>
        new(Colormap.Load(resources, "grass"), Colormap.Load(resources, "foliage"));

    /// <summary>Creates a tint from explicit colour maps (null allowed), for callers without a pack or for tests.</summary>
    public static BiomeTint FromColormaps(Colormap? grass, Colormap? foliage) => new(grass, foliage);

    /// <summary>
    /// Colour for a tint kind at a position.
    /// </summary>
    /// <param name="kind">Tint kind of the block.</param>
    /// <param name="temperature">Biome temperature.</param>
    /// <param name="rainfall">Biome rainfall.</param>
    public int Colour(BlockTintKind kind, double temperature = DefaultTemperature, double rainfall = DefaultRainfall) =>
        kind switch
        {
            BlockTintKind.Grass => _grass?.Sample(temperature, rainfall, -65281) ?? -65281,
            BlockTintKind.Foliage => _foliage?.Sample(temperature, rainfall, FoliageDefault) ?? FoliageDefault,
            BlockTintKind.FoliageEvergreen => FoliageEvergreen,
            BlockTintKind.FoliageBirch => FoliageBirch,
            BlockTintKind.Water => WaterDefault,
            _ => -1
        };

    /// <summary>Returns a copy that resolves block tints per biome via <paramref name="biomeIdAt"/> and the registry.</summary>
    public BiomeTint WithBiomes(Func<int, int, int, int> biomeIdAt, BiomeRegistry registry, int blendRadius)
    {
        ArgumentNullException.ThrowIfNull(biomeIdAt);
        ArgumentNullException.ThrowIfNull(registry);
        return new BiomeTint(_grass, _foliage, biomeIdAt, registry, Math.Max(0, blendRadius));
    }

    /// <summary>
    /// Colour for a tint kind at a world block position. With biome data it averages the per-biome colour over the
    /// biome-blend square (reference ClientLevel.calculateBlockTint); otherwise it uses the default temperature/rainfall.
    /// </summary>
    public int ColourAt(BlockTintKind kind, int x, int y, int z)
    {
        if (_biomeIdAt is null || _registry is null)
        {
            return Colour(kind);
        }

        return BlockTint.Calculate(x, z, _blendRadius, (wx, wz) =>
        {
            var entry = _registry.ById(_biomeIdAt(wx, y, wz));
            return entry is null ? Colour(kind) : BiomeColour(kind, entry, wx, wz);
        });
    }

    private int BiomeColour(BlockTintKind kind, BiomeEntry entry, int x, int z)
    {
        var effects = entry.Effects;

        // Reference getGrassColorFromTexture clamps temperature and downfall to 0..1 before indexing the colour map;
        // without it a biome below 0 or above 1 indexes outside the map and returns the magenta fallback.
        var temperature = Math.Clamp(entry.Climate.Temperature, 0f, 1f);
        var downfall = Math.Clamp(entry.Climate.Downfall, 0f, 1f);

        return kind switch
        {
            BlockTintKind.Grass => ApplyGrassModifier(
                effects.GrassColor ?? (_grass?.Sample(temperature, downfall, -65281) ?? -65281),
                effects.GrassColorModifier,
                x,
                z),
            BlockTintKind.Foliage => effects.FoliageColor ?? (_foliage?.Sample(temperature, downfall, FoliageDefault) ?? FoliageDefault),
            BlockTintKind.FoliageEvergreen => FoliageEvergreen,
            BlockTintKind.FoliageBirch => FoliageBirch,
            BlockTintKind.Water => effects.WaterColor,
            _ => -1
        };
    }

    private static int ApplyGrassModifier(int baseColor, GrassColourModifier modifier, int x, int z) => modifier switch
    {
        // Reference DARK_FOREST: ARGB.opaque(((base & 0xFEFEFE) + 2634762) >> 1).
        GrassColourModifier.DarkForest => unchecked((int)0xFF000000) | ((((baseColor & 0x00FEFEFE) + 2634762) >> 1) & 0xFFFFFF),
        // Reference SWAMP uses Biome.BIOME_INFO_NOISE (not implemented); documented controlled deviation keeps the base.
        GrassColourModifier.Swamp => baseColor,
        _ => baseColor
    };

    /// <summary>
    /// Converts a packed colour to the three multipliers the mesher writes into a vertex.
    /// </summary>
    /// <param name="colour">Packed RGB colour, or -1 for no tint.</param>
    /// <remarks>
    /// Colours are packed ARGB, so a valid colour is usually negative because its alpha byte is 0xFF. Only the sentinel -1
    /// means "no tint"; treating every negative value as none silently dropped the foliage colour the reference supplies.
    /// </remarks>
    public static (float Red, float Green, float Blue) ToMultiplier(int colour) =>
        colour == -1
            ? (1f, 1f, 1f)
            : (((colour >> 16) & 0xFF) / 255f, ((colour >> 8) & 0xFF) / 255f, (colour & 0xFF) / 255f);
}
