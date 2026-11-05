// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Common.World;

/// <summary>
/// Indexes a 256x256 grass/foliage colour map from biome temperature and downfall, matching the reference
/// <c>ColorMapColorUtil.get</c> (docs/modules/weather-and-sky-color.md 6.1). Inputs are expected already clamped to
/// 0..1 (the reference clamps them before calling, in <c>Biome.getGrassColorFromTexture</c>).
/// </summary>
[Sharpcraft.Common.Reference.ReferenceClass("net.minecraft.world.level.ColorMapColorUtil", "ref://mc-26.3-analysis/src/net/minecraft/world/level/ColorMapColorUtil.java", Kind = Sharpcraft.Common.Reference.ReferenceKind.Mirror, Status = Sharpcraft.Common.Reference.ReferenceStatus.Partial, Note = "auto-resolved by name; verify")]
public static class ColorMapColorUtil
{
    /// <summary>
    /// Returns the colour at (<paramref name="temperature"/>, <paramref name="downfall"/>) in the map, or
    /// <paramref name="defaultColor"/> when the derived index is outside the map.
    /// </summary>
    /// <remarks>
    /// The reference multiplies downfall by temperature before indexing: <c>x = (1-temp)*255</c>,
    /// <c>y = (1-temp*downfall)*255</c>, <c>index = (y&lt;&lt;8) | x</c>. The extra negative-index guard is a safety net
    /// (the reference only checks <c>index &gt;= length</c>; callers clamp to 0..1 so the two agree on valid input).
    /// </remarks>
    public static int Get(double temperature, double downfall, ReadOnlySpan<int> pixels, int defaultColor)
    {
        var rain = downfall * temperature;
        var x = (int)((1.0 - temperature) * 255.0);
        var y = (int)((1.0 - rain) * 255.0);
        var index = (y << 8) | x;
        return index < 0 || index >= pixels.Length ? defaultColor : pixels[index];
    }
}
