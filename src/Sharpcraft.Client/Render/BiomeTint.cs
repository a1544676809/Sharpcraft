// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Client.Assets;
using Sharpcraft.Common.Identifiers;

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

    private BiomeTint(Colormap? grass, Colormap? foliage)
    {
        _grass = grass;
        _foliage = foliage;
    }

    /// <summary>Biome temperature used until biome data reaches the client.</summary>
    public const double DefaultTemperature = 0.5;

    /// <summary>Biome rainfall used until biome data reaches the client.</summary>
    public const double DefaultRainfall = 1.0;

    /// <summary>Loads both colour maps from the resource stack.</summary>
    /// <param name="resources">Session resource manager.</param>
    public static BiomeTint Load(ResourceManager resources) =>
        new(Colormap.Load(resources, "grass"), Colormap.Load(resources, "foliage"));

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

    /// <summary>
    /// Converts a packed colour to the three multipliers the mesher writes into a vertex.
    /// </summary>
    /// <param name="colour">Packed RGB colour, or -1 for no tint.</param>
    public static (float Red, float Green, float Blue) ToMultiplier(int colour) =>
        colour < 0
            ? (1f, 1f, 1f)
            : (((colour >> 16) & 0xFF) / 255f, ((colour >> 8) & 0xFF) / 255f, (colour & 0xFF) / 255f);
}
