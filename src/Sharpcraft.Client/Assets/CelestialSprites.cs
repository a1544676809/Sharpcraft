// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using Sharpcraft.Common.Identifiers;

namespace Sharpcraft.Client.Assets;

/// <summary>A pixel rectangle of one celestial sprite inside its source image.</summary>
public readonly record struct CelestialSprite(int X, int Y, int Width, int Height);

/// <summary>
/// Celestial textures live under <c>textures/environment/</c>, separate from the block atlas
/// (docs/specs/render/sky-celestial.md 3). The moon texture is a 4x2 grid of the 8 phases.
/// </summary>
public static class CelestialSprites
{
    /// <summary>Resource location of the sun texture (<c>textures/environment/sun.png</c>).</summary>
    public static readonly Identifier Sun = new("minecraft", "environment/sun");

    /// <summary>Resource location of the moon phases texture (<c>textures/environment/moon_phases.png</c>).</summary>
    public static readonly Identifier MoonPhases = new("minecraft", "environment/moon_phases");

    /// <summary>Moon phase grid columns in the source image.</summary>
    public const int MoonPhaseColumns = 4;

    /// <summary>Moon phase grid rows in the source image.</summary>
    public const int MoonPhaseRows = 2;

    /// <summary>Number of moon phases.</summary>
    public const int MoonPhaseCount = MoonPhaseColumns * MoonPhaseRows;

    /// <summary>
    /// Slices the 4x2 moon-phase sheet into the 8 phase rectangles in row-major order (the reference lays the phases out
    /// in this grid and addresses them by MoonPhase order).
    /// </summary>
    public static CelestialSprite[] SliceMoonPhases(int width, int height)
    {
        if (width <= 0 || height <= 0 || width % MoonPhaseColumns != 0 || height % MoonPhaseRows != 0)
        {
            throw new ArgumentException($"moon phases image {width}x{height} does not tile a {MoonPhaseColumns}x{MoonPhaseRows} grid");
        }

        var cellWidth = width / MoonPhaseColumns;
        var cellHeight = height / MoonPhaseRows;
        var result = new CelestialSprite[MoonPhaseCount];

        for (var index = 0; index < MoonPhaseCount; index++)
        {
            var column = index % MoonPhaseColumns;
            var row = index / MoonPhaseColumns;
            result[index] = new CelestialSprite(column * cellWidth, row * cellHeight, cellWidth, cellHeight);
        }

        return result;
    }
}
